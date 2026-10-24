namespace DvPhotoValidator.Core.Models;

public enum ValidationStatus
{
    Pass,
    Warning,
    Fail,
    ManualReview,
}

/// <summary>
/// Technical checks describe the file (format, size, color); visual checks describe the photo itself
/// (geometry, background, quality). The UI shows the two groups separately and never merges them into a score.
/// </summary>
public enum ValidationCategory
{
    Technical,
    Visual,
}

public static class ValidationStatusExtensions
{
    /// <summary>Severity used to summarize a group: Fail &gt; ManualReview &gt; Warning &gt; Pass.</summary>
    public static int Severity(this ValidationStatus status) => status switch
    {
        ValidationStatus.Pass => 0,
        ValidationStatus.Warning => 1,
        ValidationStatus.ManualReview => 2,
        ValidationStatus.Fail => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static string ToDisplayText(this ValidationStatus status) => status switch
    {
        ValidationStatus.Pass => "Пройдено",
        ValidationStatus.Warning => "Внимание",
        ValidationStatus.Fail => "Не соответствует",
        ValidationStatus.ManualReview => "Проверьте вручную",
        _ => status.ToString(),
    };
}
