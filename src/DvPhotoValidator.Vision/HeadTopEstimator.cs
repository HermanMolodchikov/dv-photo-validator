using DvPhotoValidator.Core.Imaging;
using DvPhotoValidator.Core.Models;

namespace DvPhotoValidator.Vision;

public sealed record HeadTopResult(double Y, double Confidence, string? Note);

/// <summary>
/// Face detectors do not see the top of the head (hair, volume), so it is estimated from the image.
/// Rows above the eyes are scanned upwards in a band over the face. Each row is compared with the
/// background of the same row, sampled to the left and right of the head — so lighting gradients and
/// textured walls do not shift the result. A row is part of the head only if it has a solid run of
/// non-background pixels, so stray hairs are ignored. The scan stops after several background rows. Confidence drops on noisy backgrounds, when the head reaches the image edge, or
/// when the result disagrees with facial proportions.
/// </summary>
public static class HeadTopEstimator
{
    /// <summary>
    /// A row belongs to the head when it contains a continuous non-background run at least this wide
    /// (fraction of the face width). Thin stray hairs above the head do not form such runs.
    /// </summary>
    private const double MinHeadRunFraction = 0.12;

    /// <summary>Typical ratio (eye → top of hair) / (eye → chin); used for the fallback and the sanity check.</summary>
    private const double TypicalTopRatio = 1.1;

    /// <summary>
    /// The analysis runs at a fixed scale (face ≈ 200 px wide) so results do not depend on camera resolution:
    /// at full resolution, single stray hairs are wide and contrasty enough to look like the head.
    /// </summary>
    private const double AnalysisFaceWidth = 200;

    public static HeadTopResult Estimate(RgbImage image, DetectedFace face, double chinY)
    {
        var k = AnalysisFaceWidth / face.Width;
        if (k >= 0.8)
        {
            return EstimateAtScale(image, face, chinY);
        }

        var small = Resampler.Resample(image, 0, 0, image.Width, image.Height,
            Math.Max(1, (int)Math.Round(image.Width * k)), Math.Max(1, (int)Math.Round(image.Height * k)));
        var result = EstimateAtScale(small, YuNetDecoder.Scale(face, k), chinY * k);
        return result with { Y = result.Y / k };
    }

    private static HeadTopResult EstimateAtScale(RgbImage image, DetectedFace face, double chinY)
    {
        var eyeY = face.EyeCenter.Y;
        var eyeToChin = Math.Max(1, chinY - eyeY);
        var fallback = Math.Max(0, eyeY - TypicalTopRatio * eyeToChin);
        var cx = face.EyeCenter.X;
        var w = face.Width;

        var bandLeft = (int)Math.Clamp(cx - 0.3 * w, 0, image.Width - 1);
        var bandRight = (int)Math.Clamp(cx + 0.3 * w, 0, image.Width - 1);
        var sides = SideRanges(image.Width, cx, w);
        if (sides.Count == 0)
        {
            return new HeadTopResult(fallback, 0.3, "Голова занимает всю ширину фото — верх головы оценён по пропорциям лица.");
        }

        var bandStep = Math.Max(1, (bandRight - bandLeft) / 200);
        var run = Math.Max(3, (int)(image.Height * 0.005));
        var streak = 0;
        double? top = null;
        var noise = new List<double>();
        for (var y = (int)Math.Min(eyeY, image.Height - 1); y >= 0; y--)
        {
            var (reference, spread) = RowBackground(image, y, sides);
            noise.Add(spread);
            var threshold = Math.Clamp(spread * 3, 35, 60);

            int currentRun = 0, longestRun = 0;
            for (var x = bandLeft; x <= bandRight; x += bandStep)
            {
                currentRun = Distance(image.GetPixel(x, y), reference) >= threshold ? currentRun + 1 : 0;
                longestRun = Math.Max(longestRun, currentRun);
            }

            if (longestRun * bandStep < MinHeadRunFraction * w)
            {
                if (++streak >= run)
                {
                    top = y + streak;
                    break;
                }
            }
            else
            {
                streak = 0;
            }
        }

        if (top is null)
        {
            return new HeadTopResult(0, 0.4, "Голова доходит до верхнего края фото — верх головы может быть обрезан.");
        }

        // Background noise (median spread of the side samples): ≤ 6 → confidence 1.0, ≥ 25 → 0.5.
        noise.Sort();
        var typicalNoise = noise[noise.Count / 2];
        var confidence = Math.Clamp(1 - (typicalNoise - 6) / 19 * 0.5, 0.5, 1);
        var note = confidence < 0.6 ? "Фон неоднородный — проверьте линию верха головы." : null;

        var ratio = (eyeY - top.Value) / eyeToChin;
        if (ratio is < 0.6 or > 1.8)
        {
            return new HeadTopResult(top.Value, Math.Min(confidence, 0.4),
                "Найденный верх головы не согласуется с пропорциями лица (светлые волосы на светлом фоне, головной убор?) — проверьте линию.");
        }

        return new HeadTopResult(top.Value, confidence, note);
    }

    /// <summary>Horizontal ranges left and right of the head used as the per-row background reference.</summary>
    private static List<(int From, int To)> SideRanges(int width, double cx, double faceWidth)
    {
        var ranges = new List<(int, int)>();
        void Add(double from, double to)
        {
            var a = (int)Math.Clamp(from, 0, width - 1);
            var b = (int)Math.Clamp(to, 0, width - 1);
            if (b - a >= 2)
            {
                ranges.Add((a, b));
            }
        }

        Add(cx - 1.25 * faceWidth, cx - 0.85 * faceWidth);
        Add(cx + 0.85 * faceWidth, cx + 1.25 * faceWidth);
        return ranges;
    }

    /// <summary>Median background color of the row and its spread (median distance to that color).</summary>
    private static ((byte R, byte G, byte B) Color, double Spread) RowBackground(RgbImage image, int y, List<(int From, int To)> sides)
    {
        var samples = new List<(byte R, byte G, byte B)>(48);
        foreach (var (from, to) in sides)
        {
            var step = Math.Max(1, (to - from) / 24);
            for (var x = from; x <= to; x += step)
            {
                samples.Add(image.GetPixel(x, y));
            }
        }

        var median = (Median(samples.Select(s => s.R)), Median(samples.Select(s => s.G)), Median(samples.Select(s => s.B)));
        var distances = samples.Select(s => Distance(s, median)).Order().ToArray();
        return (median, distances[distances.Length / 2]);
    }

    private static byte Median(IEnumerable<byte> values)
    {
        var sorted = values.Order().ToArray();
        return sorted[sorted.Length / 2];
    }

    private static double Distance((byte R, byte G, byte B) a, (byte R, byte G, byte B) b)
    {
        double dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }
}
