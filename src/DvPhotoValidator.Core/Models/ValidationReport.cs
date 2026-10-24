using System.Globalization;
using System.Text;

namespace DvPhotoValidator.Core.Models;

public sealed class ValidationReport
{
    public static ValidationReport Empty { get; } = new([]);

    public ValidationReport(IEnumerable<ValidationIssue> issues)
    {
        Issues = issues.ToList();
    }

    public IReadOnlyList<ValidationIssue> Issues { get; }

    public IEnumerable<ValidationIssue> Technical => Issues.Where(i => i.Category == ValidationCategory.Technical);

    public IEnumerable<ValidationIssue> Visual => Issues.Where(i => i.Category == ValidationCategory.Visual);

    public ValidationStatus? TechnicalStatus => Summarize(Technical);

    public ValidationStatus? VisualStatus => Summarize(Visual);

    public bool HasFailures => Issues.Any(i => i.Status == ValidationStatus.Fail);

    public ValidationReport Concat(IEnumerable<ValidationIssue> more) => new(Issues.Concat(more));

    /// <summary>Worst status of the group, or null when the group is empty.</summary>
    public static ValidationStatus? Summarize(IEnumerable<ValidationIssue> issues)
    {
        ValidationStatus? worst = null;
        foreach (var issue in issues)
        {
            if (worst is null || issue.Status.Severity() > worst.Value.Severity())
            {
                worst = issue.Status;
            }
        }

        return worst;
    }

    /// <summary>Plain-text report that can be copied or saved. Contains no image data or face coordinates.</summary>
    public string ToText(string heading)
    {
        var sb = new StringBuilder();
        sb.AppendLine(heading);
        sb.AppendLine(DateTime.Now.ToString("g", CultureInfo.CurrentCulture));
        AppendGroup(sb, "Технические требования к файлу", Technical.ToList());
        AppendGroup(sb, "Визуальные оценки", Visual.ToList());
        sb.AppendLine();
        sb.AppendLine("Автоматическая проверка не гарантирует официального принятия фотографии.");
        return sb.ToString();
    }

    private static void AppendGroup(StringBuilder sb, string title, IReadOnlyList<ValidationIssue> issues)
    {
        if (issues.Count == 0)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine($"{title}: {Summarize(issues)!.Value.ToDisplayText()}");
        foreach (var issue in issues)
        {
            var measured = issue.MeasuredText is { } m ? $" [{m}]" : "";
            sb.AppendLine($"  [{issue.Status.ToDisplayText()}] {issue.Title}{measured} — {issue.Description} ({issue.RuleId})");
        }
    }
}
