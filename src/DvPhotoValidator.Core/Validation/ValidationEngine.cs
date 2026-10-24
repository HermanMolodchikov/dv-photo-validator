using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;

namespace DvPhotoValidator.Core.Validation;

/// <summary>Runs all rules and combines their results. A crashing rule never breaks the whole report.</summary>
public sealed class ValidationEngine
{
    private readonly IReadOnlyList<IValidationRule> _rules;

    public ValidationEngine(IEnumerable<IValidationRule> rules)
    {
        _rules = rules.ToList();
    }

    public static ValidationEngine CreateDefault() => new(
    [
        new FileFormatRule(),
        new FileSizeRule(),
        new SquareRule(),
        new DimensionsRule(),
        new ColorModeRule(),
        new ColorSpaceRule(),
        new CropResolutionRule(),
        new FaceDetectionRule(),
        new HeadGeometryValidator(),
        new FaceCenterRule(),
        new PhotoAgeRule(),
        new ManualChecklistRule(),
    ]);

    public IReadOnlyList<IValidationRule> Rules => _rules;

    public ValidationReport Run(ValidationContext context, RuleProfile profile)
    {
        var issues = new List<ValidationIssue>();
        foreach (var rule in _rules)
        {
            try
            {
                issues.AddRange(rule.Evaluate(context, profile));
            }
            catch (Exception ex)
            {
                issues.Add(new ValidationIssue(
                    rule.Id,
                    ValidationStatus.ManualReview,
                    "Проверка не выполнена",
                    $"Внутренняя ошибка проверки ({ex.GetType().Name}). Проверьте этот параметр вручную."));
            }
        }

        return new ValidationReport(issues);
    }
}
