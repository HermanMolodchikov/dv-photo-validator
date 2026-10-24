namespace DvPhotoValidator.Core.Rules;

/// <summary>
/// Versioned set of DV photo parameters. Values must be re-checked against the official
/// instructions for the specific DV program year before each release (see docs/rules-update.md).
/// </summary>
public sealed record RuleProfile
{
    public string Id { get; init; } = "dv-baseline";

    public string Name { get; init; } = "DV Lottery — базовый профиль";

    public string RulesVersion { get; init; } = "1.0";

    /// <summary>DV program year the values were checked for, e.g. "DV-2028". Null if not checked.</summary>
    public string? ProgramYear { get; init; }

    /// <summary>Date (ISO yyyy-MM-dd) of the last check against official sources. Null if not checked.</summary>
    public string? VerifiedOn { get; init; }

    /// <summary>Side of the exported square JPEG, in pixels.</summary>
    public int ExportSize { get; init; } = 600;

    /// <summary>Minimum accepted side of a finished square photo.</summary>
    public int MinSize { get; init; } = 600;

    /// <summary>Maximum accepted side of a finished square photo.</summary>
    public int MaxSize { get; init; } = 1200;

    /// <summary>File size limit; 240 KiB = 245 760 bytes by default.</summary>
    public long MaxFileBytes { get; init; } = 245_760;

    public double HeadMinRatio { get; init; } = 0.50;

    public double HeadMaxRatio { get; init; } = 0.69;

    /// <summary>Eye line height measured from the bottom edge, as a fraction of image height.</summary>
    public double EyeMinRatio { get; init; } = 0.56;

    public double EyeMaxRatio { get; init; } = 0.69;

    public int MaxPhotoAgeMonths { get; init; } = 6;

    public int MaxJpegQuality { get; init; } = 95;

    /// <summary>Below this JPEG quality the export succeeds but shows a quality warning.</summary>
    public int MinAcceptableJpegQuality { get; init; } = 70;

    public IReadOnlyList<string> Sources { get; init; } = [];

    public static RuleProfile Baseline { get; } = new();

    public string DisplayName => VerifiedOn is null
        ? $"{Name} (v{RulesVersion}, не сверено с инструкцией)"
        : $"{Name} (v{RulesVersion}, {ProgramYear}, сверено {VerifiedOn})";

    /// <summary>Returns human-readable problems; an empty sequence means the profile is usable.</summary>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            yield return "Не задан id профиля.";
        }

        if (MinSize < 1 || MaxSize < MinSize)
        {
            yield return "Некорректный диапазон размеров (minSize/maxSize).";
        }

        if (ExportSize < MinSize || ExportSize > MaxSize)
        {
            yield return "exportSize должен лежать в диапазоне minSize..maxSize.";
        }

        if (MaxFileBytes < 1024)
        {
            yield return "Слишком маленький лимит maxFileBytes.";
        }

        if (!IsRange(HeadMinRatio, HeadMaxRatio))
        {
            yield return "Некорректный диапазон высоты головы.";
        }

        if (!IsRange(EyeMinRatio, EyeMaxRatio))
        {
            yield return "Некорректный диапазон высоты глаз.";
        }

        if (MaxJpegQuality is < 1 or > 100 || MinAcceptableJpegQuality is < 1 or > 100)
        {
            yield return "Качество JPEG должно быть в диапазоне 1..100.";
        }
    }

    private static bool IsRange(double min, double max) => min > 0 && max <= 1 && min < max;
}
