using DvPhotoValidator.Core.Geometry;
using DvPhotoValidator.Core.Interfaces;
using DvPhotoValidator.Core.Models;

namespace DvPhotoValidator.Imaging;

/// <summary>
/// Crops and resamples in one pass with a separable Mitchell–Netravali filter (B = C = 1/3).
/// The filter is widened when downscaling, so it behaves as a proper low-pass filter
/// and does not produce aliasing or strong halos around the face outline.
/// </summary>
public sealed class CropService : ICropService
{
    private const double FilterRadius = 2.0;

    public RgbImage Render(RgbImage source, CropRect crop, int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        var clamped = CropGeometry.Clamp(crop, source.Width, source.Height);
        return Resample(source, clamped.X, clamped.Y, clamped.Size, clamped.Size, size, size);
    }

    public static RgbImage Resample(RgbImage src, double x0, double y0, double width, double height, int outWidth, int outHeight)
    {
        var columns = Contributions(x0, width, src.Width, outWidth);
        var rows = Contributions(y0, height, src.Height, outHeight);

        var firstRow = rows.Min(r => r.Indices.Min());
        var lastRow = rows.Max(r => r.Indices.Max());
        var rowCount = lastRow - firstRow + 1;

        // Horizontal pass over the rows that the vertical pass needs.
        var temp = new float[rowCount * outWidth * 3];
        var pixels = src.Pixels;
        var stride = src.Stride;
        Parallel.For(0, rowCount, r =>
        {
            var srcRow = (firstRow + r) * stride;
            var tempRow = r * outWidth * 3;
            for (var ox = 0; ox < outWidth; ox++)
            {
                var c = columns[ox];
                float b = 0, g = 0, rr = 0;
                for (var k = 0; k < c.Indices.Length; k++)
                {
                    var i = srcRow + c.Indices[k] * 3;
                    var w = c.Weights[k];
                    b += pixels[i] * w;
                    g += pixels[i + 1] * w;
                    rr += pixels[i + 2] * w;
                }

                var t = tempRow + ox * 3;
                temp[t] = b;
                temp[t + 1] = g;
                temp[t + 2] = rr;
            }
        });

        // Vertical pass.
        var result = new RgbImage(outWidth, outHeight);
        var dst = result.Pixels;
        Parallel.For(0, outHeight, oy =>
        {
            var c = rows[oy];
            var dstRow = oy * outWidth * 3;
            for (var ox = 0; ox < outWidth * 3; ox++)
            {
                float sum = 0;
                for (var k = 0; k < c.Indices.Length; k++)
                {
                    sum += temp[(c.Indices[k] - firstRow) * outWidth * 3 + ox] * c.Weights[k];
                }

                dst[dstRow + ox] = (byte)Math.Clamp((int)MathF.Round(sum), 0, 255);
            }
        });

        return result;
    }

    private sealed record Contribution(int[] Indices, float[] Weights);

    private static Contribution[] Contributions(double origin, double length, int sourceLength, int outLength)
    {
        var scale = length / outLength;
        var filterScale = Math.Max(1.0, scale);
        var support = FilterRadius * filterScale;
        var result = new Contribution[outLength];
        for (var i = 0; i < outLength; i++)
        {
            // Pixel j covers [j, j + 1); its center is j + 0.5.
            var center = origin + (i + 0.5) * scale;
            var start = (int)Math.Floor(center - support);
            var end = (int)Math.Ceiling(center + support);
            var indices = new List<int>(end - start + 1);
            var weights = new List<float>(end - start + 1);
            double total = 0;
            for (var j = start; j <= end; j++)
            {
                var w = Mitchell((j + 0.5 - center) / filterScale);
                if (w == 0)
                {
                    continue;
                }

                indices.Add(Math.Clamp(j, 0, sourceLength - 1));
                weights.Add((float)w);
                total += w;
            }

            if (total == 0)
            {
                indices.Clear();
                weights.Clear();
                indices.Add(Math.Clamp((int)center, 0, sourceLength - 1));
                weights.Add(1f);
                total = 1;
            }

            result[i] = new Contribution([.. indices], weights.Select(w => (float)(w / total)).ToArray());
        }

        return result;
    }

    private static double Mitchell(double x)
    {
        const double b = 1.0 / 3, c = 1.0 / 3;
        x = Math.Abs(x);
        if (x < 1)
        {
            return ((12 - 9 * b - 6 * c) * x * x * x + (-18 + 12 * b + 6 * c) * x * x + (6 - 2 * b)) / 6;
        }

        if (x < 2)
        {
            return ((-b - 6 * c) * x * x * x + (6 * b + 30 * c) * x * x + (-12 * b - 48 * c) * x + (8 * b + 24 * c)) / 6;
        }

        return 0;
    }
}
