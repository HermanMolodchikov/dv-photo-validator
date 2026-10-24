using DvPhotoValidator.Core.Models;

namespace DvPhotoValidator.Vision;

/// <summary>
/// Turns a detected face into the three vertical landmarks used for DV geometry.
/// <list type="bullet">
/// <item>Eye line — mean of the two eye keypoints (most reliable).</item>
/// <item>Chin — mean of two independent estimates: the bottom of the detector box and
/// mouth + 0.75 × (mouth − eyes), from typical facial proportions. Their disagreement lowers confidence.</item>
/// <item>Top of head — <see cref="HeadTopEstimator"/>.</item>
/// </list>
/// Confidence is the minimum of the detector score and the chin and head-top confidences.
/// </summary>
public static class LandmarkDetector
{
    public const double ChinFromMouthFactor = 0.75;

    public static (HeadLandmarks Landmarks, IReadOnlyList<string> Notes) Estimate(RgbImage image, DetectedFace face)
    {
        var notes = new List<string>();
        var eyes = face.EyeCenter;
        var mouth = face.MouthCenter;
        var eyeToMouth = mouth.Y - eyes.Y;

        double chin, chinConfidence;
        if (eyeToMouth <= 0)
        {
            chin = face.Bottom;
            chinConfidence = 0.3;
            notes.Add("Не удалось согласовать положение глаз и рта — подбородок оценён по рамке лица.");
        }
        else
        {
            var fromBox = face.Bottom;
            var fromProportions = mouth.Y + ChinFromMouthFactor * eyeToMouth;
            chin = (fromBox + fromProportions) / 2;
            chinConfidence = Math.Clamp(1 - Math.Abs(fromBox - fromProportions) / (0.25 * face.Height), 0, 1);
            if (chinConfidence < 0.6)
            {
                notes.Add("Оценки положения подбородка расходятся — проверьте нижнюю линию.");
            }
        }

        chin = Math.Min(chin, image.Height);
        var top = HeadTopEstimator.Estimate(image, face, chin);
        if (top.Note is not null)
        {
            notes.Add(top.Note);
        }

        var confidence = Math.Min(face.Score, Math.Min(chinConfidence, top.Confidence));
        var landmarks = new HeadLandmarks(top.Y, chin, eyes.Y)
        {
            Source = LandmarkSource.Automatic,
            Confidence = Math.Round(confidence, 2),
        };
        return (landmarks, notes);
    }
}
