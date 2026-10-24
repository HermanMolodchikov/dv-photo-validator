using DvPhotoValidator.Core.Geometry;
using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;
using DvPhotoValidator.Core.Validation;

namespace DvPhotoValidator.Tests.UnitTests;

public class ValidationEngineTests
{
    private static readonly ImageFileFacts Compliant =
        new(ImageFileFormat.Jpeg, 600, 600, 200_000, IsGrayscale: false, ColorSpaceKind.Untagged, BitsPerPixel: 24);

    private static ValidationReport Run(ImageFileFacts facts) =>
        ValidationEngine.CreateDefault().Run(ValidationContext.ForFile(facts), RuleProfile.Baseline);

    [Fact]
    public void CompliantFile_PassesAllTechnicalRules()
    {
        var report = Run(Compliant);

        Assert.NotEmpty(report.Issues);
        Assert.All(report.Issues, i => Assert.Equal(ValidationStatus.Pass, i.Status));
        Assert.All(report.Issues, i => Assert.Equal(ValidationCategory.Technical, i.Category));
        Assert.Equal(ValidationStatus.Pass, report.TechnicalStatus);
        Assert.Null(report.VisualStatus);
    }

    [Theory]
    [InlineData(245_760, ValidationStatus.Pass)]
    [InlineData(245_761, ValidationStatus.Fail)]
    public void FileSize_LimitIs240KiB(long bytes, ValidationStatus expected)
    {
        var issue = Run(Compliant with { FileBytes = bytes }).Issues.Single(i => i.RuleId == "FILE_SIZE");

        Assert.Equal(expected, issue.Status);
    }

    [Theory]
    [InlineData(600, 601, "DIMENSIONS_SQUARE")]
    [InlineData(599, 599, "DIMENSIONS_RANGE")]
    [InlineData(1201, 1201, "DIMENSIONS_RANGE")]
    public void Dimensions_OutOfSpec_Fail(int width, int height, string ruleId)
    {
        var report = Run(Compliant with { Width = width, Height = height });

        Assert.Equal(ValidationStatus.Fail, report.Issues.Single(i => i.RuleId == ruleId).Status);
        Assert.Equal(ValidationStatus.Fail, report.TechnicalStatus);
    }

    [Fact]
    public void Dimensions_1200_IsAccepted()
    {
        var report = Run(Compliant with { Width = 1200, Height = 1200 });

        Assert.False(report.HasFailures);
    }

    [Fact]
    public void Png_Grayscale_AdobeRgb_AllFail()
    {
        var report = Run(Compliant with { Format = ImageFileFormat.Png, IsGrayscale = true, ColorSpace = ColorSpaceKind.Other });

        Assert.Equal(ValidationStatus.Fail, report.Issues.Single(i => i.RuleId == "FILE_FORMAT").Status);
        Assert.Equal(ValidationStatus.Fail, report.Issues.Single(i => i.RuleId == "COLOR_MODE").Status);
        Assert.Equal(ValidationStatus.Fail, report.Issues.Single(i => i.RuleId == "COLOR_SPACE").Status);
    }

    [Fact]
    public void CropContext_ProducesOnlyVisualIssues_AndTechnicalStatusIsSeparate()
    {
        var context = new ValidationContext
        {
            Crop = new CropRect(0, 0, 800),
            Landmarks = new HeadLandmarks(150, 630, 300),
            Capture = new CaptureDateInfo(DateTime.Now.AddDays(-10)),
        };

        var report = ValidationEngine.CreateDefault().Run(context, RuleProfile.Baseline);

        Assert.All(report.Issues, i => Assert.Equal(ValidationCategory.Visual, i.Category));
        Assert.Null(report.TechnicalStatus);
        Assert.False(report.HasFailures);
        Assert.Equal(ValidationStatus.Pass, report.Issues.Single(i => i.RuleId == HeadGeometryValidator.HeadHeightId).Status);
        Assert.Equal(ValidationStatus.Pass, report.Issues.Single(i => i.RuleId == HeadGeometryValidator.EyeHeightId).Status);
        Assert.Equal(ValidationStatus.Pass, report.VisualStatus);
    }

    [Fact]
    public void Summary_UsesWorstStatus()
    {
        ValidationIssue Issue(ValidationStatus s) => new("X", s, "t", "d");

        Assert.Equal(ValidationStatus.Fail, ValidationReport.Summarize([Issue(ValidationStatus.Pass), Issue(ValidationStatus.Fail), Issue(ValidationStatus.ManualReview)]));
        Assert.Equal(ValidationStatus.ManualReview, ValidationReport.Summarize([Issue(ValidationStatus.Warning), Issue(ValidationStatus.ManualReview)]));
        Assert.Equal(ValidationStatus.Warning, ValidationReport.Summarize([Issue(ValidationStatus.Pass), Issue(ValidationStatus.Warning)]));
        Assert.Null(ValidationReport.Summarize([]));
    }

    [Fact]
    public void CrashingRule_BecomesManualReview_OtherRulesStillRun()
    {
        var engine = new ValidationEngine([new ThrowingRule(), new FileFormatRule()]);

        var report = engine.Run(ValidationContext.ForFile(Compliant), RuleProfile.Baseline);

        Assert.Equal(ValidationStatus.ManualReview, report.Issues.Single(i => i.RuleId == "BOOM").Status);
        Assert.Equal(ValidationStatus.Pass, report.Issues.Single(i => i.RuleId == "FILE_FORMAT").Status);
    }

    [Fact]
    public void PhotoAge_SuitableDate_IsNotReported()
    {
        var now = new DateTime(2026, 10, 24);
        var context = new ValidationContext { Capture = new CaptureDateInfo(now.AddDays(-30)), Now = now };

        Assert.Empty(new PhotoAgeRule().Evaluate(context, RuleProfile.Baseline));
    }

    [Theory]
    [InlineData(-200, ValidationStatus.Warning)]
    [InlineData(30, ValidationStatus.ManualReview)]
    public void PhotoAge_UsesCaptureDate(int days, ValidationStatus expected)
    {
        var now = new DateTime(2026, 10, 24);
        var context = new ValidationContext { Capture = new CaptureDateInfo(now.AddDays(days)), Now = now };

        var issue = new PhotoAgeRule().Evaluate(context, RuleProfile.Baseline).Single();

        Assert.Equal(expected, issue.Status);
    }

    [Fact]
    public void PhotoAge_UnknownDate_IsNotReported()
    {
        Assert.Empty(new PhotoAgeRule().Evaluate(new ValidationContext { Capture = new CaptureDateInfo(null) }, RuleProfile.Baseline));
    }

    [Fact]
    public void Report_ToText_ListsGroupsAndDisclaimer()
    {
        var text = Run(Compliant with { FileBytes = 300_000 }).ToText("Отчёт");

        Assert.Contains("Технические требования к файлу: Не соответствует", text);
        Assert.Contains("FILE_SIZE", text);
        Assert.Contains("не гарантирует", text);
    }

    private sealed class ThrowingRule : IValidationRule
    {
        public string Id => "BOOM";

        public IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile) =>
            throw new InvalidOperationException("boom");
    }
}
