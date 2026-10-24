using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DvPhotoValidator.App.ViewModels;

namespace DvPhotoValidator.App.Views;

/// <summary>Translates mouse and keyboard input into view-model operations; holds no state of its own beyond the drag.</summary>
public partial class CropEditorView : UserControl
{
    private const double WheelZoomStep = 1.1;
    private const double KeyZoomStep = 1.05;

    private enum DragMode
    {
        None,
        Pan,
        Guide,
    }

    private DragMode _mode;
    private GuideKind _guide;
    private Point _last;

    public CropEditorView()
    {
        InitializeComponent();
    }

    private CropEditorViewModel? ViewModel => DataContext as CropEditorViewModel;

    private void Guide_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse(tag, out GuideKind kind))
        {
            _mode = DragMode.Guide;
            _guide = kind;
            Surface.Focus();
            Surface.CaptureMouse();
            ViewModel?.MoveGuide(kind, e.GetPosition(Surface).Y);
            e.Handled = true;
        }
    }

    private void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mode = DragMode.Pan;
        _last = e.GetPosition(Surface);
        Surface.Focus();
        Surface.CaptureMouse();
        e.Handled = true;
    }

    private void Surface_MouseMove(object sender, MouseEventArgs e)
    {
        if (ViewModel is not { } vm || _mode == DragMode.None)
        {
            return;
        }

        var position = e.GetPosition(Surface);
        if (_mode == DragMode.Pan)
        {
            vm.Pan(position.X - _last.X, position.Y - _last.Y);
            _last = position;
        }
        else
        {
            vm.MoveGuide(_guide, position.Y);
        }
    }

    private void Surface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _mode = DragMode.None;
        Surface.ReleaseMouseCapture();
    }

    private void Surface_LostMouseCapture(object sender, MouseEventArgs e) => _mode = DragMode.None;

    private void Surface_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var position = e.GetPosition(Surface);
        ViewModel?.Zoom(e.Delta > 0 ? WheelZoomStep : 1 / WheelZoomStep, position.X, position.Y);
        e.Handled = true;
    }

    private void Surface_KeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 20 : 2;
        switch (e.Key)
        {
            // Arrows move the photo in the arrow direction; the frame stays in place.
            case Key.Left:
                vm.Pan(-step, 0);
                break;
            case Key.Right:
                vm.Pan(step, 0);
                break;
            case Key.Up:
                vm.Pan(0, -step);
                break;
            case Key.Down:
                vm.Pan(0, step);
                break;
            case Key.Add or Key.OemPlus:
                vm.ZoomAtCenter(KeyZoomStep);
                break;
            case Key.Subtract or Key.OemMinus:
                vm.ZoomAtCenter(1 / KeyZoomStep);
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}
