using System.Windows;
using System.Windows.Threading;
using DvPhotoValidator.App.Services;
using DvPhotoValidator.App.ViewModels;
using DvPhotoValidator.App.Views;
using DvPhotoValidator.Core.Rules;
using DvPhotoValidator.Core.Validation;
using DvPhotoValidator.Imaging;

namespace DvPhotoValidator.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        var profiles = RuleProfileLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "Rules"));
        var loader = new ImageLoader();
        var engine = ValidationEngine.CreateDefault();
        var viewModel = new MainViewModel(loader, new CropService(), new JpegExporter(loader, engine), engine, new DialogService(), profiles);

        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        window.Show();

        // Open a photo passed on the command line ("Open with", drag onto the exe).
        // Launchers may add options such as "--port 1234"; anything that is not an existing file is ignored.
        if (e.Args.FirstOrDefault(arg => !arg.StartsWith('-') && File.Exists(arg)) is { } photo)
        {
            _ = viewModel.LoadAsync(photo);
        }
    }

    // A processing error must never take the whole application down. No image data is logged.
    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Непредвиденная ошибка: {e.Exception.Message}\n\nПриложение продолжит работу.",
            "DV Photo Validator",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
