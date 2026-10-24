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

    /// <summary>The user confirmed this item by hand (it was ManualReview).</summary>
    public bool ConfirmedByUser { get; init; }

    /// <summary>False for ManualReview items that need an action (e.g. placing landmarks), not just a look.</summary>
    public bool Confirmable { get; init; } = true;

    /// <summary>The user may confirm this item: it needs a human look, or was already confirmed.</summary>
    public bool CanConfirm => (Status == ValidationStatus.ManualReview && Confirmable) || ConfirmedByUser;

    public string? MeasuredText => MeasuredValue is { } value
        ? value.ToString(value % 1 == 0 ? "0" : "0.#", CultureInfo.CurrentCulture) + (Unit is null ? "" : " " + Unit)
        : null;
}
