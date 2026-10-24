using DvPhotoValidator.Core.Geometry;
using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;

namespace DvPhotoValidator.Core.Validation;

/// <summary>Head and eye measurements relative to the crop, in percent of the crop height.</summary>
public sealed record HeadMeasurement(double? HeadHeightPercent, double? EyeHeightPercent);

/// <summary>
/// Head size is measured from the top of the head (including hair) to the bottom of the chin,
/// not from a face-detector box: R = (Yc − Yt) / H × 100%.
/// Eye height is measured from the bottom edge of the photo to the eye line.
/// </summary>
public sealed class HeadGeometryValidator : IValidationRule
{
    public const string HeadHeightId = "HEAD_HEIGHT";
    public const string EyeHeightId = "EYE_HEIGHT";
    public const string HeadInFrameId = "HEAD_IN_FRAME";

    public string Id => HeadHeightId;

    public static HeadMeasurement Measure(CropRect crop, HeadLandmarks landmarks)
    {
        double? head = landmarks is { HeadTopY: { } top, ChinY: { } chin }
            ? (chin - top) / crop.Size * 100
            : null;
        double? eye = landmarks.EyeLineY is { } eyeY
            ? (crop.Bottom - eyeY) / crop.Size * 100
            : null;
        return new HeadMeasurement(head, eye);
    }

    public IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile)
    {
        if (context.Crop is not { } crop)
        {
            yield break;
        }

        var landmarks = context.Landmarks ?? HeadLandmarks.None;
        var method = landmarks.Source == LandmarkSource.Manual
            ? "Метод: ручные ориентиры."
            : $"Метод: автоматические ориентиры, уверенность {landmarks.Confidence:P0}.";
        var measured = Measure(crop, landmarks);
        var headRange = $"{profile.HeadMinRatio * 100:0}–{profile.HeadMaxRatio * 100:0}%";
        var eyeRange = $"{profile.EyeMinRatio * 100:0}–{profile.EyeMaxRatio * 100:0}%";

        if (landmarks is { HeadTopY: { } top, ChinY: { } chin })
        {
            if (chin <= top)
            {
                yield return Visual(HeadHeightId, ValidationStatus.ManualReview, "Высота головы",
                    "Линия подбородка расположена выше верха головы. Поправьте ориентиры.");
            }
            else
            {
                if (top < crop.Y || chin > crop.Bottom)
                {
                    yield return Visual(HeadInFrameId, ValidationStatus.Fail, "Голова в кадре",
                        "Верх головы или подбородок выходят за границы кадра. Уменьшите масштаб или сместите рамку.");
                }

                var r = measured.HeadHeightPercent!.Value;
                var pixels = r / 100 * profile.ExportSize;
                var description = $"{r:0.#}% высоты кадра (≈{pixels:0} px при {profile.ExportSize} px); норма {headRange}. {method}";
                var status = r >= profile.HeadMinRatio * 100 && r <= profile.HeadMaxRatio * 100
                    ? ValidationStatus.Pass
                    : ValidationStatus.Fail;
                yield return Visual(HeadHeightId, status, "Высота головы",
                    status == ValidationStatus.Pass ? description : $"{description} {(r < profile.HeadMinRatio * 100 ? "Голова слишком маленькая — приблизьте кадр." : "Голова слишком большая — отдалите кадр.")}",
                    Math.Round(r, 1), "%");
            }
        }
        else
        {
            yield return Visual(HeadHeightId, ValidationStatus.ManualReview, "Высота головы",
                $"Отметьте верх головы (включая волосы) и низ подбородка, чтобы измерить высоту головы. Норма {headRange}.");
        }

        if (measured.EyeHeightPercent is { } e)
        {
            var status = e >= profile.EyeMinRatio * 100 && e <= profile.EyeMaxRatio * 100
                ? ValidationStatus.Pass
                : ValidationStatus.Fail;
            yield return Visual(EyeHeightId, status, "Высота линии глаз",
                $"{e:0.#}% высоты кадра от нижнего края; норма {eyeRange}. {method}", Math.Round(e, 1), "%");
        }
        else
        {
            yield return Visual(EyeHeightId, ValidationStatus.ManualReview, "Высота линии глаз",
                $"Отметьте линию глаз. Норма — {eyeRange} высоты кадра от нижнего края.");
        }
    }

    private static ValidationIssue Visual(string id, ValidationStatus status, string title, string description,
        double? value = null, string? unit = null) =>
        new(id, status, title, description, value, unit) { Category = ValidationCategory.Visual };
}
