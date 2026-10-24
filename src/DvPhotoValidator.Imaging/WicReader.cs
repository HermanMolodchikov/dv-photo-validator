using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DvPhotoValidator.Core.Models;

namespace DvPhotoValidator.Imaging;

/// <summary>Shared decoding through Windows Imaging Component (WIC).</summary>
internal static class WicReader
{
    private static readonly string[] OrientationQueries =
        ["System.Photo.Orientation", "/app1/ifd/{ushort=274}", "/ifd/{ushort=274}"];

    private static readonly string[] DateQueries =
        ["/app1/ifd/exif/{ushort=36867}", "/ifd/exif/{ushort=36867}", "/app1/ifd/{ushort=306}"];

    internal sealed record FrameInfo(
        BitmapFrame Frame,
        ushort Orientation,
        DateTime? DateTaken,
        ColorContext? Profile,
        ColorSpaceKind ColorSpace)
    {
        public bool SwapsAxes => Orientation >= 5 && Orientation <= 8;

        public int OrientedWidth => SwapsAxes ? Frame.PixelHeight : Frame.PixelWidth;

        public int OrientedHeight => SwapsAxes ? Frame.PixelWidth : Frame.PixelHeight;

        public bool IsGrayFormat =>
            Frame.Format == PixelFormats.Gray2 || Frame.Format == PixelFormats.Gray4 ||
            Frame.Format == PixelFormats.Gray8 || Frame.Format == PixelFormats.Gray16 ||
            Frame.Format == PixelFormats.Gray32Float || Frame.Format == PixelFormats.BlackWhite;
    }

    /// <summary>Reads the header and metadata only; pixels are decoded later on demand.</summary>
    public static FrameInfo ReadFirstFrame(Stream stream, ImageFileFormat format)
    {
        BitmapFrame frame;
        try
        {
            // IgnoreColorProfile: color management is done explicitly in ColorSpaceConverter.
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.None);
            if (decoder.Frames.Count == 0)
            {
                throw new ImageProcessingException("Файл не содержит изображения.");
            }

            frame = decoder.Frames[0];
            _ = frame.PixelWidth;
        }
        catch (ImageProcessingException)
        {
            throw;
        }
        catch (NotSupportedException ex) when (format is ImageFileFormat.WebP or ImageFileFormat.Heif)
        {
            throw new ImageProcessingException(
                $"Для открытия {format.ToString().ToUpperInvariant()} в Windows не установлен кодек. Установите из Microsoft Store «Расширения для изображений {(format == ImageFileFormat.WebP ? "WebP" : "HEIF")}» или сохраните фото в JPEG.",
                ex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new ImageProcessingException("Файл повреждён или не может быть декодирован.", ex);
        }

        var metadata = TryGetMetadata(frame);
        var (profile, kind) = ColorSpaceConverter.ReadProfile(frame);
        return new FrameInfo(frame, ReadOrientation(metadata), ReadDateTaken(metadata), profile, kind);
    }

    /// <summary>Decodes pixels, converts to sRGB Bgr24 and applies EXIF orientation.</summary>
    public static ColorSpaceConverter.ConversionResult DecodeUpright(FrameInfo info)
    {
        ColorSpaceConverter.ConversionResult converted;
        try
        {
            converted = ColorSpaceConverter.ToSrgb(info.Frame, info.Profile, info.ColorSpace);
        }
        catch (OutOfMemoryException ex)
        {
            throw new ImageProcessingException("Недостаточно памяти для обработки изображения.", ex);
        }
        catch (Exception ex)
        {
            throw new ImageProcessingException("Файл повреждён: не удалось декодировать данные изображения.", ex);
        }

        return converted with { Image = ImageNormalizer.ApplyOrientation(converted.Image, info.Orientation) };
    }

    private static BitmapMetadata? TryGetMetadata(BitmapFrame frame)
    {
        try
        {
            return frame.Metadata as BitmapMetadata;
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private static object? TryQuery(BitmapMetadata metadata, string query)
    {
        try
        {
            return metadata.GetQuery(query);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private static ushort ReadOrientation(BitmapMetadata? metadata)
    {
        if (metadata is null)
        {
            return 1;
        }

        foreach (var query in OrientationQueries)
        {
            var value = TryQuery(metadata, query);
            if (value is ushort or short or uint or int or byte)
            {
                var orientation = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                if (orientation is >= 1 and <= 8)
                {
                    return (ushort)orientation;
                }
            }
        }

        return 1;
    }

    private static DateTime? ReadDateTaken(BitmapMetadata? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        foreach (var query in DateQueries)
        {
            if (TryQuery(metadata, query) is string text &&
                DateTime.TryParseExact(text.Trim('\0', ' '), "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        try
        {
            if (metadata.DateTaken is { } text &&
                DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // No usable date in metadata.
        }

        return null;
    }
}
