using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;

namespace DvPhotoValidator.Core.Validation;

/// <summary>
/// Visual requirements that a person has to confirm. When automatic quality measurements are available,
/// only the items that are not measured remain; otherwise (no face found) the full list is shown.
/// They are reported as ManualReview so the visual status never looks "all clear" before someone looks;
/// the user can confirm each item in the UI.
/// </summary>
public sealed class ManualChecklistRule : IValidationRule
{
    public string Id => "MANUAL_CHECKLIST";

    private static readonly (string Id, string Title, string Description, bool Automated)[] Items =
    [
        ("BACKGROUND", "Фон", "Однородный белый или почти белый фон без теней, узоров и посторонних предметов.", true),
        ("POSE", "Положение головы", "Анфас, взгляд в камеру, голова не наклонена и не повёрнута; лицо по центру кадра.", true),
        ("EYES", "Глаза", "Оба глаза открыты и не закрыты волосами.", true),
        ("GLASSES", "Очки", "Без очков, в том числе без оправы, и без бликов от линз.", true),
        ("LIGHTING_SHARPNESS", "Освещение и резкость", "Равномерное освещение без резких теней и пересветов; изображение резкое, без заметного шума и ретуши.", true),
        ("EXPRESSION", "Выражение лица", "Нейтральное выражение лица, рот закрыт, без улыбки.", false),
        ("HEADWEAR", "Головной убор и предметы", "Без головного убора (допускается только религиозный, при этом лицо полностью видно), без наушников и посторонних предметов.", false),
    ];

    public IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile)
    {
        if (context.Crop is null)
        {
            return [];
        }

        var automated = context.Quality is not null;
        return Items
            .Where(item => !(automated && item.Automated))
            .Select(item => new ValidationIssue(
                $"{item.Id}_MANUAL",
                ValidationStatus.ManualReview,
                item.Title,
                item.Description + (item.Automated ? " Автоматическая проверка недоступна без найденного лица — проверьте сами." : " Это проверяется только человеком."))
            {
                Category = ValidationCategory.Visual,
            });
    }
}
