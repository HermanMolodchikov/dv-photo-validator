using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;

namespace DvPhotoValidator.Core.Validation;

public interface IValidationRule
{
    /// <summary>Stable identifier, also used as RuleId of the issues produced.</summary>
    string Id { get; }

    /// <summary>Returns no issues when the context does not contain the rule's inputs.</summary>
    IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile);
}
