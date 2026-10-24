using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DvPhotoValidator.Core.Models;

namespace DvPhotoValidator.Tests.TestImages;

/// <summary>Generates synthetic test images in code, so the repository does not need real photos of people.</summary>
public static class TestImageFactory
{
    public static RgbImage Solid(int width, int height, byte r, byte g, byte b)
    {
        var image = new RgbImage(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                image.SetPixel(x, y, r, g, b);
            }
        }

        return image;
    }

    /// <summary>Smooth color gradient — compresses well, like a real portrait.</summary>
    public static RgbImage Gradient(int width, int height)
    {
        var image = new RgbImage(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                image.SetPixel(x, y, (byte)(x * 255 / Math.Max(1, width - 1)), (byte)(y * 255 / Math.Max(1, height - 1)), 128);
            }
        }

        return image;
    }

    /// <summary>Schematic portrait: near-white background, head ellipse with dark "hair" cap and shoulders.</summary>
    public static RgbImage Portrait(int width, int height)
    {
        var image = Solid(width, height, 246, 246, 244);
        double cx = width / 2.0, cy = height * 0.42, rx = width * 0.17, ry = height * 0.2;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var dx = (x - cx) / rx;
                var dy = (y - cy) / ry;
                if (dx * dx + dy * dy <= 1)
                {
                    if (y < cy - ry * 0.55)
                    {
                        image.SetPixel(x, y, 70, 50, 40);
                    }
                    else
                    {
                        image.SetPixel(x, y, 224, 180, 150);
                    }
                }
                else if (y > height * 0.7 && Math.Abs(x - cx) < width * 0.42 * ((y - height * 0.7) / (height * 0.3) * 0.5 + 0.5))
                {
                    image.SetPixel(x, y, 40, 60, 110);
                }
            }
        }

        return image;
    }

    /// <summary>Random color noise — the worst case for JPEG size.</summary>
    public static RgbImage Noise(int width, int height, int seed = 42)
    {
        var image = new RgbImage(width, height);
        new Random(seed).NextBytes(image.Pixels);
        return image;
    }

    /// <summary>Four distinct quadrant colors to verify orientation handling.</summary>
    public static RgbImage Quadrants(int width, int height)
    {
        var image = new RgbImage(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var left = x < width / 2;
                var top = y < height / 2;
                var (r, g, b) = (left, top) switch
                {
                    (true, true) => ((byte)255, (byte)0, (byte)0),     // red: top-left
                    (false, true) => ((byte)0, (byte)255, (byte)0),    // green: top-right
                    (true, false) => ((byte)0, (byte)0, (byte)255),    // blue: bottom-left
                    _ => ((byte)255, (byte)255, (byte)0),              // yellow: bottom-right
                };
                image.SetPixel(x, y, r, g, b);
            }
        }

        return image;
    }

    public static BitmapSource ToBitmap(RgbImage image) =>
        BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgr24, null, image.Pixels, image.Stride);

    public static void SaveJpeg(RgbImage image, string path, int quality = 90, ushort? orientation = null,
        string? exifDate = null, ReadOnlyCollection<ColorContext>? colorContexts = null)
    {
        BitmapMetadata? metadata = null;
        if (orientation is not null || exifDate is not null)
        {
            metadata = new BitmapMetadata("jpg");
            if (orientation is { } o)
            {
                metadata.SetQuery("/app1/ifd/{ushort=274}", o);
            }

            if (exifDate is not null)
            {
                metadata.SetQuery("/app1/ifd/exif/{ushort=36867}", exifDate);
            }
        }

        var encoder = new JpegBitmapEncoder { QualityLevel = quality };
        encoder.Frames.Add(BitmapFrame.Create(ToBitmap(image), null, metadata, colorContexts));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    public static void SaveGrayJpeg(int width, int height, string path)
    {
        var gray = new FormatConvertedBitmap(ToBitmap(Gradient(width, height)), PixelFormats.Gray8, null, 0);
        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
        encoder.Frames.Add(BitmapFrame.Create(gray));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    public static void Save(BitmapEncoder encoder, BitmapSource source, string path)
    {
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    /// <summary>PNG whose left half is fully transparent and right half opaque red.</summary>
    public static void SaveHalfTransparentPng(int width, int height, string path)
    {
        var stride = width * 4;
        var bgra = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = width / 2; x < width; x++)
            {
                var i = y * stride + x * 4;
                bgra[i + 2] = 255;
                bgra[i + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgra, stride);
        Save(new PngBitmapEncoder(), bitmap, path);
    }
}

public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dvpv-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
