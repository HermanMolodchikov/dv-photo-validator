using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DvPhotoValidator.App.Services;
using DvPhotoValidator.App.ViewModels;
using DvPhotoValidator.App.Views;
using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;
using DvPhotoValidator.Core.Validation;
using DvPhotoValidator.Imaging;
using DvPhotoValidator.Tests.TestImages;

namespace DvPhotoValidator.Tests.IntegrationTests;

/// <summary>
/// Builds the real main window off-screen, loads a photo and renders it. Catches XAML, resource and
/// binding errors that unit tests cannot see. Set DVPV_UI_SNAPSHOT to a .png path to keep the rendering.
/// </summary>
public sealed class UiSmokeTests
{
    [Fact]
    public void MainWindow_LoadsPhoto_UpdatesChecks_AndRenders()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Run();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException("UI smoke test failed: " + failure);
        }
    }

    private static void Run()
    {
        using var dir = new TempDirectory();
        var photo = dir.File("portrait.jpg");
        TestImageFactory.SaveJpeg(TestImageFactory.Portrait(1200, 1500), photo, exifDate: DateTime.Now.AddDays(-20).ToString("yyyy:MM:dd HH:mm:ss"));

        var app = new DvPhotoValidator.App.App();
        app.InitializeComponent();

        var loader = new ImageLoader();
        var engine = ValidationEngine.CreateDefault();
        var vm = new MainViewModel(loader, new CropService(), new JpegExporter(loader, engine), engine,
            new SilentDialogs(), new RuleProfileLoadResult([RuleProfile.Baseline], []));
        var window = new MainWindow { DataContext = vm };

        var frame = new DispatcherFrame();
        Exception? asyncError = null;
        Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
        {
            try
            {
                await vm.LoadAsync(photo);

                // Place guides on the schematic head: hair top, eyes, chin (canvas coordinates).
                var e = vm.Editor;
                e.Zoom(1.25, 280, 260);
                var top = Display(e, 1500 * 0.42 - 1500 * 0.2);
                var chin = Display(e, 1500 * 0.42 + 1500 * 0.2);
                e.MoveGuide(GuideKind.HeadTop, top);
                e.MoveGuide(GuideKind.Chin, chin);
                e.MoveGuide(GuideKind.Eyes, top + (chin - top) * 0.45);
            }
            catch (Exception ex)
            {
                asyncError = ex;
            }
            finally
            {
                frame.Continue = false;
                // Wake the dispatcher loop so PushFrame notices the exit immediately.
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { }));
            }
        });
        Dispatcher.PushFrame(frame);
        if (asyncError is not null)
        {
            throw asyncError;
        }

        Assert.True(vm.HasImage, vm.StatusText);
        Assert.Contains(vm.SourceGroup.Items, i => i.RuleId == "FILE_FORMAT");
        Assert.Contains(vm.FrameGroup.Items, i => i.RuleId == HeadGeometryValidator.HeadHeightId && i.Status != ValidationStatus.ManualReview);
        // A capture date within the allowed age needs no attention, so it is not listed.
        Assert.DoesNotContain(vm.FrameGroup.Items, i => i.RuleId == "PHOTO_AGE");

        var root = (FrameworkElement)window.Content;
        var size = new Size(1280, 820);
        root.Measure(size);
        root.Arrange(new Rect(size));
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1280, 820, 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var dc = background.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(size));
        }

        bitmap.Render(background);
        bitmap.Render(root);

        if (Environment.GetEnvironmentVariable("DVPV_UI_SNAPSHOT") is { Length: > 0 } snapshot)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(snapshot);
            encoder.Save(file);
        }

        window.Close();
        app.Shutdown();
    }

    private static double Display(CropEditorViewModel e, double sourceY) =>
        CropEditorViewModel.FrameOffset + (sourceY - e.Crop.Y) * e.Scale;

    private sealed class SilentDialogs : IDialogService
    {
        public string? PickImageFile() => null;

        public string? PickSavePath(string? initialDirectory, string fileName) => null;

        public void ShowError(string message) => throw new InvalidOperationException(message);

        public void CopyToClipboard(string text)
        {
        }
    }
}
