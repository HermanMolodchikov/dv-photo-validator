namespace DvPhotoValidator.Core.Models;

/// <summary>
/// Decoded 8-bit sRGB image without alpha, stored as BGR bytes (Bgr24, stride = Width * 3).
/// Independent from any imaging library so that geometry and validation code stays testable.
/// </summary>
public sealed class RgbImage
{
    public RgbImage(int width, int height, byte[]? pixels = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        var length = checked(width * height * 3);
        if (pixels is not null && pixels.Length != length)
        {
            throw new ArgumentException($"Expected {length} bytes, got {pixels.Length}.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels ?? new byte[length];
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride => Width * 3;

    public byte[] Pixels { get; }

    public (byte R, byte G, byte B) GetPixel(int x, int y)
    {
        var i = (y * Width + x) * 3;
        return (Pixels[i + 2], Pixels[i + 1], Pixels[i]);
    }

    public void SetPixel(int x, int y, byte r, byte g, byte b)
    {
        var i = (y * Width + x) * 3;
        Pixels[i] = b;
        Pixels[i + 1] = g;
        Pixels[i + 2] = r;
    }

    /// <summary>
    /// True when practically every pixel has R≈G≈B (the tolerance covers JPEG chroma noise).
    /// Large images are sampled on a grid to keep the check fast.
    /// </summary>
    public bool IsGrayscale(int tolerance = 6, double maxColoredFraction = 0.005)
    {
        var step = Math.Max(1, (int)Math.Sqrt((double)Width * Height / 250_000));
        long sampled = 0, colored = 0;
        for (var y = 0; y < Height; y += step)
        {
            var row = y * Stride;
            for (var x = 0; x < Width; x += step)
            {
                var i = row + x * 3;
                int b = Pixels[i], g = Pixels[i + 1], r = Pixels[i + 2];
                var spread = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
                sampled++;
                if (spread > tolerance)
                {
                    colored++;
                }
            }
        }

        return colored <= sampled * maxColoredFraction;
    }
}
