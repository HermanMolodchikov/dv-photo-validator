using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;

namespace DvPhotoValidator.Core.Validation;

/// <summary>Warns when the crop has fewer source pixels than the export size, i.e. the photo will be upscaled.</summary>
public sealed class CropResolutionRule : IValidationRule
{
    public string Id => "CROP_RESOLUTION";

    public IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile)
    {
        if (context.Crop is not { } crop)
        {
            yield break;
        }

        var side = Math.Round(crop.Size);
        yield return side >= profile.ExportSize
            ? new ValidationIssue(Id, ValidationStatus.Pass, "Детализация кадра",
                $"В рамке {side:0}×{side:0} пикселей исходника; итоговое изображение будет уменьшено до {profile.ExportSize}×{profile.ExportSize}.",
                side, "px")
            { Category = ValidationCategory.Visual }
            : new ValidationIssue(Id, ValidationStatus.Warning, "Детализация кадра",
                $"В рамке только {side:0}×{side:0} пикселей исходника — изображение будет растянуто до {profile.ExportSize}×{profile.ExportSize} и может выглядеть размытым. Используйте фото с более высоким разрешением.",
                side, "px")
            { Category = ValidationCategory.Visual };
    }
}
