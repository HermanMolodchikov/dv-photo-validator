using DvPhotoValidator.Core.Models;

namespace DvPhotoValidator.Imaging;

public static class ImageNormalizer
{
    /// <summary>
    /// Applies an EXIF Orientation value (1–8) so that the result is stored upright.
    /// Unknown values are treated as 1 (no change).
    /// </summary>
    public static RgbImage ApplyOrientation(RgbImage source, ushort orientation)
    {
        if (orientation is < 2 or > 8)
        {
            return source;
        }

        int w = source.Width, h = source.Height;
        var swap = orientation >= 5;
        var result = new RgbImage(swap ? h : w, swap ? w : h);
        var src = source.Pixels;
        var dst = result.Pixels;
        for (var y = 0; y < result.Height; y++)
        {
            for (var x = 0; x < result.Width; x++)
            {
                // Map each destination pixel back to the stored (source) pixel.
                var (sx, sy) = orientation switch
                {
                    2 => (w - 1 - x, y),          // mirror horizontal
                    3 => (w - 1 - x, h - 1 - y),  // rotate 180
                    4 => (x, h - 1 - y),          // mirror vertical
                    5 => (y, x),                  // transpose
                    6 => (y, h - 1 - x),          // rotate 90 CW
                    7 => (w - 1 - y, h - 1 - x),  // transverse
                    _ => (w - 1 - y, x),          // 8: rotate 90 CCW
                };
                var si = (sy * w + sx) * 3;
                var di = (y * result.Width + x) * 3;
                dst[di] = src[si];
                dst[di + 1] = src[si + 1];
                dst[di + 2] = src[si + 2];
            }
        }

        return result;
    }
}
