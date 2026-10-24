using DvPhotoValidator.Core.Models;

namespace DvPhotoValidator.Vision;

/// <summary>
/// Decodes raw YuNet (face_detection_yunet_2023mar) outputs, following OpenCV's FaceDetectorYN:
/// for each stride s ∈ {8, 16, 32} and grid cell (row, col), score = √(cls·obj),
/// center = (col + dx, row + dy)·s, size = exp(dw, dh)·s, keypoint = (col + kx, row + ky)·s.
/// Coordinates are in model-input pixels.
/// </summary>
public static class YuNetDecoder
{
    public static readonly int[] Strides = [8, 16, 32];

    /// <param name="outputs">Arrays keyed by output name: cls_8, obj_8, bbox_8, kps_8, … for each stride.</param>
    public static List<DetectedFace> Decode(IReadOnlyDictionary<string, float[]> outputs, int inputSize, float scoreThreshold)
    {
        var faces = new List<DetectedFace>();
        foreach (var stride in Strides)
        {
            var cls = outputs[$"cls_{stride}"];
            var obj = outputs[$"obj_{stride}"];
            var bbox = outputs[$"bbox_{stride}"];
            var kps = outputs[$"kps_{stride}"];
            var cols = inputSize / stride;
            var rows = inputSize / stride;
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var i = r * cols + c;
                    var score = Math.Sqrt(Math.Clamp(cls[i], 0f, 1f) * Math.Clamp(obj[i], 0f, 1f));
                    if (score < scoreThreshold)
                    {
                        continue;
                    }

                    var cx = (c + bbox[i * 4]) * stride;
                    var cy = (r + bbox[i * 4 + 1]) * stride;
                    var w = Math.Exp(bbox[i * 4 + 2]) * stride;
                    var h = Math.Exp(bbox[i * 4 + 3]) * stride;
                    PointD Kp(int n) => new((c + kps[i * 10 + 2 * n]) * stride, (r + kps[i * 10 + 2 * n + 1]) * stride);
                    faces.Add(new DetectedFace(cx - w / 2, cy - h / 2, w, h, score, Kp(0), Kp(1), Kp(2), Kp(3), Kp(4)));
                }
            }
        }

        return faces;
    }

    /// <summary>Greedy non-maximum suppression by IoU; returns survivors ordered by score.</summary>
    public static List<DetectedFace> Suppress(IEnumerable<DetectedFace> faces, double iouThreshold)
    {
        var kept = new List<DetectedFace>();
        foreach (var face in faces.OrderByDescending(f => f.Score))
        {
            if (kept.All(k => IoU(k, face) <= iouThreshold))
            {
                kept.Add(face);
            }
        }

        return kept;
    }

    public static double IoU(DetectedFace a, DetectedFace b)
    {
        var x1 = Math.Max(a.X, b.X);
        var y1 = Math.Max(a.Y, b.Y);
        var x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        var y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);
        var intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var union = a.Area + b.Area - intersection;
        return union <= 0 ? 0 : intersection / union;
    }

    public static DetectedFace Scale(DetectedFace f, double k)
    {
        PointD S(PointD p) => new(p.X * k, p.Y * k);
        return new DetectedFace(f.X * k, f.Y * k, f.Width * k, f.Height * k, f.Score,
            S(f.RightEye), S(f.LeftEye), S(f.Nose), S(f.MouthRight), S(f.MouthLeft));
    }
}
