using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;

namespace DvPhotoValidator.Core.Validation;

/// <summary>Reports the outcome of automatic face detection; runs only when analysis was performed.</summary>
public sealed class FaceDetectionRule : IValidationRule
{
    public string Id => "FACE_DETECTION";

    public IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile)
    {
        if (context.Crop is null || context.Face is not { } face)
        {
            yield break;
        }

        var issue = face.Faces.Count switch
        {
            0 => new ValidationIssue(Id, ValidationStatus.ManualReview, "Лицо",
                "Лицо не найдено автоматически. Отметьте ориентиры вручную или выберите другое фото (анфас, хорошее освещение)."),
            1 => new ValidationIssue(Id, ValidationStatus.Pass, "Лицо",
                $"Найдено одно лицо, уверенность детектора {face.Faces[0].Score:P0}.", Math.Round(face.Faces[0].Score * 100), "%"),
            var n => new ValidationIssue(Id, ValidationStatus.Warning, "Лицо",
                $"Найдено лиц: {n}. На фото должен быть только заявитель — рекомендуется выбрать другое изображение. Для расчётов взято самое крупное лицо.",
                n),
        };
        yield return issue with { Category = ValidationCategory.Visual };
    }
}

/// <summary>Horizontal centering of the face in the frame (heuristic threshold, not an official norm).</summary>
public sealed class FaceCenterRule : IValidationRule
{
    public const double MaxOffsetPercent = 5;

    public string Id => "FACE_CENTER";

    public IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile)
    {
        if (context.Crop is not { } crop || context.Face?.Primary is not { } face)
        {
            yield break;
        }

        var center = face.EyeCenter;
        if (center.X < crop.X || center.X > crop.Right || center.Y < crop.Y || center.Y > crop.Bottom)
        {
            yield return new ValidationIssue(Id, ValidationStatus.Fail, "Лицо по центру",
                "Глаза находятся за пределами кадра.")
            { Category = ValidationCategory.Visual };
            yield break;
        }

        var offset = (center.X - crop.CenterX) / crop.Size * 100;
        var side = offset > 0 ? "вправо" : "влево";
        yield return Math.Abs(offset) <= MaxOffsetPercent
            ? new ValidationIssue(Id, ValidationStatus.Pass, "Лицо по центру",
                $"Смещение от вертикальной оси {Math.Abs(offset):0.#}% ширины кадра.", Math.Round(Math.Abs(offset), 1), "%")
            { Category = ValidationCategory.Visual }
            : new ValidationIssue(Id, ValidationStatus.Warning, "Лицо по центру",
                $"Лицо смещено {side} на {Math.Abs(offset):0.#}% ширины кадра (ориентир — не более {MaxOffsetPercent:0}%). Нажмите «Автоматически выровнять» или сдвиньте фото.",
                Math.Round(Math.Abs(offset), 1), "%")
            { Category = ValidationCategory.Visual };
    }
}
