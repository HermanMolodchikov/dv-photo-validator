using DvPhotoValidator.Core.Geometry;
using DvPhotoValidator.Core.Imaging;
using DvPhotoValidator.Core.Interfaces;
using DvPhotoValidator.Core.Models;

namespace DvPhotoValidator.Vision;

/// <summary>
/// Measures the frame exactly as it will be exported: the crop is rendered at the export size first,
/// so every threshold works on the same scale regardless of the camera resolution.
/// Only relative measures are used on the face (clipping, left/right balance) — never the absolute
/// brightness of the skin, so the result does not depend on skin tone.
/// </summary>
public sealed class PhotoQualityAnalyzer : IQualityAnalyzer
{
    public QualityMeasurements Analyze(RgbImage source, CropRect crop, DetectedFace face, HeadLandmarks landmarks, int frameSize)
    {
        var clamped = CropGeometry.Clamp(crop, source.Width, source.Height);
        var frame = new Frame(Resampler.Resample(source, clamped.X, clamped.Y, clamped.Size, clamped.Size, frameSize, frameSize));
        var k = frameSize / clamped.Size;
        PointD T(PointD p) => new((p.X - clamped.X) * k, (p.Y - clamped.Y) * k);
        var f = new DetectedFace((face.X - clamped.X) * k, (face.Y - clamped.Y) * k, face.Width * k, face.Height * k, face.Score,
            T(face.RightEye), T(face.LeftEye), T(face.Nose), T(face.MouthRight), T(face.MouthLeft));
        var eyeY = f.EyeCenter.Y;
        var top = landmarks.HeadTopY is { } t ? (t - clamped.Y) * k : f.Y - 0.6 * f.Height;
        var chin = landmarks.ChinY is { } c ? (c - clamped.Y) * k : f.Bottom;

        var (leftEye, rightEye) = f.RightEye.X <= f.LeftEye.X ? (f.RightEye, f.LeftEye) : (f.LeftEye, f.RightEye);
        var eyeDistance = Math.Max(1, Math.Sqrt(Math.Pow(rightEye.X - leftEye.X, 2) + Math.Pow(rightEye.Y - leftEye.Y, 2)));

        return new QualityMeasurements(
            MeasureBackground(frame, f, top, chin),
            MeasureSharpness(frame, leftEye, rightEye, eyeDistance),
            MeasureLighting(frame, f),
            MeasurePose(f, leftEye, rightEye, eyeDistance),
            MeasureEyes(frame, leftEye, rightEye, eyeDistance),
            MeasureGlasses(frame, leftEye, rightEye, eyeDistance, eyeY));
    }

    /// <summary>
    /// Background = everything above the chin outside an ellipse around the head (with a margin for hair).
    /// Below the chin the shoulders usually reach the frame edges, so that area is not used.
    /// </summary>
    private static BackgroundMeasurement? MeasureBackground(Frame frame, DetectedFace f, double top, double chin)
    {
        var cx = f.EyeCenter.X;
        var a = 0.85 * f.Width;
        var cy = (top + chin) / 2;
        var b = Math.Max(1, (chin - top) / 2 * 1.12);
        var lightness = new List<double>();
        var chroma = new List<double>();
        var zones = new[] { new List<double>(), new List<double>(), new List<double>() };
        var residuals = new List<double>();
        var edges = 0;
        var maxY = (int)Math.Clamp(chin, 0, frame.Size - 2);
        for (var y = 1; y < maxY; y += 2)
        {
            for (var x = 1; x < frame.Size - 1; x += 2)
            {
                var dx = (x - cx) / a;
                var dy = (y - cy) / b;
                if (dx * dx + dy * dy <= 1)
                {
                    continue;
                }

                var (l, ca, cb) = frame.Lab(x, y);
                lightness.Add(l);
                chroma.Add(Math.Sqrt(ca * ca + cb * cb));
                zones[x < cx - a ? 0 : x > cx + a ? 1 : 2].Add(l);
                var gradient = (Math.Abs(frame.L(x + 1, y) - frame.L(x - 1, y)) + Math.Abs(frame.L(x, y + 1) - frame.L(x, y - 1))) / 2;
                if (gradient > 6)
                {
                    edges++;
                }

                residuals.Add(frame.Y(x, y) - frame.MeanY3(x, y));
            }
        }

        var total = (double)(frame.Size * frame.Size) / 4;
        if (lightness.Count < total * 0.04)
        {
            return null;
        }

        var medianL = Median(lightness);
        var zoneMedians = zones.Where(z => z.Count >= 100).Select(Median).ToList();
        var spread = zoneMedians.Count >= 2 ? zoneMedians.Max() - zoneMedians.Min() : 0;
        var shadow = lightness.Count(l => l < medianL - 10) / (double)lightness.Count;
        var noise = 1.4826 * Median(residuals.Select(Math.Abs).ToList());
        return new BackgroundMeasurement(
            lightness.Count / total,
            medianL,
            Median(chroma),
            spread,
            shadow,
            edges / (double)lightness.Count,
            noise);
    }

    private static SharpnessMeasurement MeasureSharpness(Frame frame, PointD leftEye, PointD rightEye, double d)
    {
        var x0 = (int)Math.Clamp(leftEye.X - 0.35 * d, 1, frame.Size - 2);
        var x1 = (int)Math.Clamp(rightEye.X + 0.35 * d, 1, frame.Size - 2);
        var eyeY = (leftEye.Y + rightEye.Y) / 2;
        var y0 = (int)Math.Clamp(eyeY - 0.2 * d, 1, frame.Size - 2);
        var y1 = (int)Math.Clamp(eyeY + 0.2 * d, 1, frame.Size - 2);
        var values = new List<double>();
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                values.Add(4 * frame.Y(x, y) - frame.Y(x - 1, y) - frame.Y(x + 1, y) - frame.Y(x, y - 1) - frame.Y(x, y + 1));
            }
        }

        return new SharpnessMeasurement(Variance(values));
    }

    private static LightingMeasurement MeasureLighting(Frame frame, DetectedFace f)
    {
        var x0 = (int)Math.Clamp(f.X + 0.15 * f.Width, 0, frame.Size - 1);
        var x1 = (int)Math.Clamp(f.X + 0.85 * f.Width, 0, frame.Size - 1);
        var y0 = (int)Math.Clamp(f.Y + 0.15 * f.Height, 0, frame.Size - 1);
        var y1 = (int)Math.Clamp(f.Y + 0.9 * f.Height, 0, frame.Size - 1);
        int count = 0, highlights = 0, darks = 0;
        var left = new List<double>();
        var right = new List<double>();
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var luma = frame.Y(x, y);
                count++;
                if (luma >= 250)
                {
                    highlights++;
                }

                if (luma <= 20)
                {
                    darks++;
                }

                (x < f.Nose.X ? left : right).Add(frame.L(x, y));
            }
        }

        var side = left.Count > 0 && right.Count > 0 ? Math.Abs(Median(left) - Median(right)) : 0;
        return new LightingMeasurement(highlights / (double)Math.Max(1, count), darks / (double)Math.Max(1, count), side);
    }

    private static PoseMeasurement MeasurePose(DetectedFace f, PointD leftEye, PointD rightEye, double d)
    {
        var roll = Math.Atan2(rightEye.Y - leftEye.Y, rightEye.X - leftEye.X) * 180 / Math.PI;
        var mid = f.EyeCenter;
        var yaw = (f.Nose.X - mid.X) / d;
        var eyeToMouth = f.MouthCenter.Y - mid.Y;
        var pitch = eyeToMouth > 0 ? (f.Nose.Y - mid.Y) / eyeToMouth : double.NaN;
        return new PoseMeasurement(roll, yaw, pitch);
    }

    /// <summary>
    /// Openness: in a narrow column through the eye keypoint, the longest run of rows that contain dark
    /// (iris/pupil) pixels, relative to the eye distance. An open eye shows the iris (≈ 0.12–0.2),
    /// a closed one only the lash line.
    /// </summary>
    private static EyesMeasurement MeasureEyes(Frame frame, PointD leftEye, PointD rightEye, double d)
    {
        var (lo, lc) = Openness(frame, leftEye, d);
        var (ro, rc) = Openness(frame, rightEye, d);
        return new EyesMeasurement(lo, ro, lc, rc);
    }

    private static (double Openness, double Contrast) Openness(Frame frame, PointD eye, double d)
    {
        var hw = 0.22 * d;
        var hh = 0.18 * d;
        var x0 = (int)Math.Clamp(eye.X - hw, 0, frame.Size - 1);
        var x1 = (int)Math.Clamp(eye.X + hw, 0, frame.Size - 1);
        var y0 = (int)Math.Clamp(eye.Y - hh, 0, frame.Size - 1);
        var y1 = (int)Math.Clamp(eye.Y + hh, 0, frame.Size - 1);
        var patch = new List<double>();
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                patch.Add(frame.Y(x, y));
            }
        }

        var median = Median(patch);
        var contrast = Math.Sqrt(Variance(patch));
        var darkLimit = Math.Min(median * 0.65, median - 25);
        var cx0 = (int)Math.Clamp(eye.X - 0.1 * d, 0, frame.Size - 1);
        var cx1 = (int)Math.Clamp(eye.X + 0.1 * d, 0, frame.Size - 1);
        int run = 0, best = 0;
        for (var y = y0; y <= y1; y++)
        {
            var dark = 0;
            for (var x = cx0; x <= cx1; x++)
            {
                if (frame.Y(x, y) < darkLimit)
                {
                    dark++;
                }
            }

            run = dark >= (cx1 - cx0 + 1) * 0.3 ? run + 1 : 0;
            best = Math.Max(best, run);
        }

        return (best / d, contrast);
    }

    /// <summary>Glasses frames add edges under the eyes (rims) and across the nose bridge; cheeks are the reference.</summary>
    private static GlassesMeasurement MeasureGlasses(Frame frame, PointD leftEye, PointD rightEye, double d, double eyeY)
    {
        double EdgeDensity(double cx, double halfWidth, double fromY, double toY)
        {
            var x0 = (int)Math.Clamp(cx - halfWidth, 1, frame.Size - 2);
            var x1 = (int)Math.Clamp(cx + halfWidth, 1, frame.Size - 2);
            var y0 = (int)Math.Clamp(fromY, 1, frame.Size - 2);
            var y1 = (int)Math.Clamp(toY, 1, frame.Size - 2);
            int count = 0, edges = 0;
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    count++;
                    if (Math.Abs(frame.Y(x, y + 1) - frame.Y(x, y - 1)) > 30)
                    {
                        edges++;
                    }
                }
            }

            return count == 0 ? 0 : edges / (double)count;
        }

        var rim = (EdgeDensity(leftEye.X, 0.25 * d, eyeY + 0.2 * d, eyeY + 0.35 * d)
                   + EdgeDensity(rightEye.X, 0.25 * d, eyeY + 0.2 * d, eyeY + 0.35 * d)) / 2;
        var cheek = (EdgeDensity(leftEye.X, 0.25 * d, eyeY + 0.45 * d, eyeY + 0.6 * d)
                     + EdgeDensity(rightEye.X, 0.25 * d, eyeY + 0.45 * d, eyeY + 0.6 * d)) / 2;
        var bridge = EdgeDensity((leftEye.X + rightEye.X) / 2, 0.1 * d, eyeY - 0.12 * d, eyeY + 0.05 * d);
        return new GlassesMeasurement(rim - cheek, bridge);
    }

    private static double Median(List<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.Order().ToArray();
        return sorted[sorted.Length / 2];
    }

    private static double Variance(List<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var mean = values.Average();
        return values.Sum(v => (v - mean) * (v - mean)) / values.Count;
    }

    /// <summary>Rendered frame with cached luma and L*a*b* planes.</summary>
    private sealed class Frame
    {
        private readonly double[] _y;
        private readonly double[] _l;
        private readonly double[] _a;
        private readonly double[] _b;

        public Frame(RgbImage image)
        {
            Size = image.Width;
            var n = image.Width * image.Height;
            _y = new double[n];
            _l = new double[n];
            _a = new double[n];
            _b = new double[n];
            var px = image.Pixels;
            for (var i = 0; i < n; i++)
            {
                byte b = px[i * 3], g = px[i * 3 + 1], r = px[i * 3 + 2];
                _y[i] = ColorMath.Luma(r, g, b);
                (_l[i], _a[i], _b[i]) = ColorMath.ToLab(r, g, b);
            }
        }

        public int Size { get; }

        public double Y(int x, int y) => _y[y * Size + x];

        public double L(int x, int y) => _l[y * Size + x];

        public (double L, double A, double B) Lab(int x, int y)
        {
            var i = y * Size + x;
            return (_l[i], _a[i], _b[i]);
        }

        public double MeanY3(int x, int y)
        {
            double sum = 0;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    sum += Y(x + dx, y + dy);
                }
            }

            return sum / 9;
        }
    }
}
