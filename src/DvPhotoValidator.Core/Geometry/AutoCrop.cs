using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;

namespace DvPhotoValidator.Core.Geometry;

/// <param name="Crop">Proposed frame, always inside the image.</param>
/// <param name="WasConstrained">The ideal frame did not fit the image and had to be moved or shrunk.</param>
public sealed record AutoCropResult(CropRect Crop, bool WasConstrained);

/// <summary>
/// Proposes a square frame from head landmarks: head height and eye line are placed in the middle
/// of the allowed ranges, and the face is centered horizontally.
/// </summary>
public static class AutoCrop
{
    public static AutoCropResult? Compute(HeadLandmarks landmarks, double faceCenterX, int imageWidth, int imageHeight, RuleProfile profile)
    {
        if (landmarks is not { HeadTopY: { } top, ChinY: { } chin, EyeLineY: { } eyes } || chin <= top)
        {
            return null;
        }

        var headRatio = (profile.HeadMinRatio + profile.HeadMaxRatio) / 2;
        var eyeRatio = (profile.EyeMinRatio + profile.EyeMaxRatio) / 2;
        var size = (chin - top) / headRatio;
        var ideal = new CropRect(faceCenterX - size / 2, eyes - (1 - eyeRatio) * size, size);
        var crop = CropGeometry.Clamp(ideal, imageWidth, imageHeight);
        var constrained = Math.Abs(crop.X - ideal.X) > 0.5 || Math.Abs(crop.Y - ideal.Y) > 0.5 || Math.Abs(crop.Size - ideal.Size) > 0.5;
        return new AutoCropResult(crop, constrained);
    }
}
