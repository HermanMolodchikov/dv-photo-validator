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
        new BackgroundRule(),
        new SharpnessRule(),
        new NoiseRule(),
        new LightingRule(),
        new PoseRule(),
        new EyesOpenRule(),
        new GlassesRule(),
        new PhotoAgeRule(),
        new ManualChecklistRule(),
    ]);

    public IReadOnlyList<IValidationRule> Rules => _rules;

    /// <summary>
    /// A ManualReview item the user has explicitly confirmed becomes Pass, marked as confirmed by the user.
    /// Fails and Warnings are never overridden: a confirmation only answers "please check this yourself".
    /// </summary>
    public static ValidationIssue ApplyConfirmation(ValidationIssue issue, ValidationContext context) =>
        issue.Status == ValidationStatus.ManualReview && issue.Confirmable && context.ConfirmedByUser.Contains(issue.RuleId)
            ? issue with { Status = ValidationStatus.Pass, Description = "Подтверждено вами вручную. " + issue.Description, ConfirmedByUser = true }
            : issue;

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

        return new ValidationReport(issues.Select(i => ApplyConfirmation(i, context)));
    }
}
