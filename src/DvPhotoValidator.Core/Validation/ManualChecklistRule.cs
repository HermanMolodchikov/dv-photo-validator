using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;

namespace DvPhotoValidator.Core.Validation;

/// <summary>
/// Visual requirements that are not yet checked automatically (stage 3 of the roadmap).
/// They are reported honestly as ManualReview so the overall visual status never looks "all clear"
/// before a person has looked at them.
/// </summary>
public sealed class ManualChecklistRule : IValidationRule
{
    public string Id => "MANUAL_CHECKLIST";

    private static readonly (string Id, string Title, string Description)[] Items =
    [
        ("BACKGROUND", "Фон", "Однородный белый или почти белый фон без теней, узоров и посторонних предметов."),
        ("POSE", "Положение головы", "Анфас, взгляд в камеру, голова не наклонена и не повёрнута; лицо по центру кадра."),
        ("EYES_EXPRESSION", "Глаза и выражение лица", "Оба глаза открыты и не закрыты волосами; нейтральное выражение лица."),
        ("GLASSES_HEADWEAR", "Очки и головные уборы", "Без очков. Головной убор — только религиозный, при этом лицо должно быть полностью видно. Без наушников и посторонних предметов."),
        ("LIGHTING_SHARPNESS", "Освещение и резкость", "Равномерное освещение без резких теней и пересветов; изображение резкое, без заметного шума и ретуши."),
    ];

    public IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile)
    {
        if (context.Crop is null)
        {
            return [];
        }

        return Items.Select(item => new ValidationIssue(
            $"{item.Id}_MANUAL",
            ValidationStatus.ManualReview,
            item.Title,
            item.Description + " Автоматическая проверка появится в следующих версиях — сейчас проверьте сами.")
        {
            Category = ValidationCategory.Visual,
        });
    }
}
