using System.Globalization;

namespace DvPhotoValidator.Core.Models;

public sealed record ValidationIssue(
    string RuleId,
    ValidationStatus Status,
    string Title,
    string Description,
    double? MeasuredValue = null,
    string? Unit = null)
{
    public ValidationCategory Category { get; init; } = ValidationCategory.Technical;

    public string? MeasuredText => MeasuredValue is { } value
        ? value.ToString(value % 1 == 0 ? "0" : "0.#", CultureInfo.CurrentCulture) + (Unit is null ? "" : " " + Unit)
        : null;
}
