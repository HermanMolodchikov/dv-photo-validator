using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DvPhotoValidator.Core.Models;

namespace DvPhotoValidator.App.Resources;

public sealed class StatusToBrushConverter : IValueConverter
{
    private static readonly Brush PassBrush = Make(0x2E, 0x7D, 0x32);
    private static readonly Brush WarningBrush = Make(0xB2, 0x6A, 0x00);
    private static readonly Brush FailBrush = Make(0xC6, 0x28, 0x28);
    private static readonly Brush ManualBrush = Make(0x15, 0x65, 0xC0);
    private static readonly Brush NoneBrush = Make(0x75, 0x75, 0x75);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ValidationStatus.Pass => PassBrush,
        ValidationStatus.Warning => WarningBrush,
        ValidationStatus.Fail => FailBrush,
        ValidationStatus.ManualReview => ManualBrush,
        _ => NoneBrush,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static SolidColorBrush Make(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>Status glyph, so the status is not conveyed by color alone.</summary>
public sealed class StatusToGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ValidationStatus.Pass => "✓",
        ValidationStatus.Warning => "!",
        ValidationStatus.Fail => "✕",
        ValidationStatus.ManualReview => "?",
        _ => "–",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StatusToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ValidationStatus status ? status.ToDisplayText() : "нет данных";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is true) ^ Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null || (value is string s && s.Length == 0) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
