using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DvPhotoValidator.Core.Models;

namespace DvPhotoValidator.Imaging;

/// <summary>Classifies embedded ICC profiles and converts decoded pixels to 8-bit sRGB.</summary>
public static class ColorSpaceConverter
{
    public sealed record ConversionResult(RgbImage Image, bool HadTransparency, bool WasConverted);

    public static (ColorContext? Profile, ColorSpaceKind Kind) ReadProfile(BitmapFrame frame)
    {
        ReadOnlyCollection<ColorContext>? contexts;
        try
        {
            contexts = frame.ColorContexts;
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return (null, ColorSpaceKind.Untagged);
        }

        if (contexts is null || contexts.Count == 0)
        {
            return (null, ColorSpaceKind.Untagged);
        }

        var context = contexts[0];
        try
        {
            using var stream = context.OpenProfileStream();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return (context, ClassifyProfile(buffer.ToArray()));
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return (context, ColorSpaceKind.Other);
        }
    }

    /// <summary>
    /// An RGB profile whose description mentions sRGB is treated as sRGB; anything else (Adobe RGB,
    /// Display P3, CMYK, gray) is "Other". Heuristic, but sufficient to decide whether conversion is needed.
    /// </summary>
    public static ColorSpaceKind ClassifyProfile(byte[] icc)
    {
        if (icc.Length < 128)
        {
            return ColorSpaceKind.Other;
        }

        var dataColorSpace = Encoding.ASCII.GetString(icc, 16, 4);
        if (dataColorSpace != "RGB ")
        {
            return ColorSpaceKind.Other;
        }

        var ascii = Encoding.ASCII.GetBytes("sRGB");
        var utf16 = Encoding.BigEndianUnicode.GetBytes("sRGB");
        var span = icc.AsSpan();
        return span.IndexOf(ascii) >= 0 || span.IndexOf(utf16) >= 0 ? ColorSpaceKind.Srgb : ColorSpaceKind.Other;
    }

    /// <summary>
    /// Converts the frame to sRGB (when it carries a non-sRGB profile), then to Bgr24.
    /// Transparent pixels are composited onto white; this only flattens alpha and does not alter the photo.
    /// </summary>
    public static ConversionResult ToSrgb(BitmapSource frame, ColorContext? profile, ColorSpaceKind kind)
    {
        BitmapSource source = frame;
        var converted = false;
        if (profile is not null && kind != ColorSpaceKind.Srgb)
        {
            try
            {
                source = new ColorConvertedBitmap(frame, profile, new ColorContext(PixelFormats.Bgra32), PixelFormats.Bgra32);
                converted = true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException or System.Runtime.InteropServices.COMException)
            {
                // Keep the unconverted pixels; the COLOR_SPACE rule on the source reports the problem.
            }
        }

        if (source.Format != PixelFormats.Bgra32)
        {
            source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        }

        int width = source.PixelWidth, height = source.PixelHeight;
        var stride = checked(width * 4);
        var bgra = new byte[checked(stride * height)];
        source.CopyPixels(bgra, stride, 0);

        var image = new RgbImage(width, height);
        var dst = image.Pixels;
        var hadAlpha = false;
        for (int s = 0, d = 0; s < bgra.Length; s += 4, d += 3)
        {
            int a = bgra[s + 3];
            if (a == 255)
            {
                dst[d] = bgra[s];
                dst[d + 1] = bgra[s + 1];
                dst[d + 2] = bgra[s + 2];
            }
            else
            {
                hadAlpha = true;
                var white = 255 * (255 - a);
                dst[d] = (byte)((bgra[s] * a + white + 127) / 255);
                dst[d + 1] = (byte)((bgra[s + 1] * a + white + 127) / 255);
                dst[d + 2] = (byte)((bgra[s + 2] * a + white + 127) / 255);
            }
        }

        return new ConversionResult(image, hadAlpha, converted);
    }
}
