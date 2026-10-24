using DvPhotoValidator.Core.Geometry;
using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;
using DvPhotoValidator.Core.Validation;

namespace DvPhotoValidator.Tests.UnitTests;

public class HeadGeometryValidatorTests
{
    private static readonly RuleProfile Profile = RuleProfile.Baseline;
    private static readonly CropRect Crop = new(100, 200, 600);

    private static List<ValidationIssue> Evaluate(HeadLandmarks landmarks) =>
        new HeadGeometryValidator().Evaluate(new ValidationContext { Crop = Crop, Landmarks = landmarks }, Profile).ToList();

    [Fact]
    public void Measure_UsesTopOfHeadToChinRelativeToCropHeight()
    {
        // Head from y=260 to y=620 inside a crop starting at y=200: 360 / 600 = 60%.
        // Eyes at y=420 → (800 − 420) / 600 = 63.3% from the bottom.
        var m = HeadGeometryValidator.Measure(Crop, new HeadLandmarks(260, 620, 420));

        Assert.Equal(60, m.HeadHeightPercent!.Value, 3);
        Assert.Equal(63.333, m.EyeHeightPercent!.Value, 3);
    }

    [Theory]
    [InlineData(300, true)]   // 50% — lower bound inclusive
    [InlineData(414, true)]   // 69% — upper bound inclusive
    [InlineData(360, true)]
    [InlineData(299, false)]
    [InlineData(415, false)]
    public void HeadHeight_BoundariesMatchSpecification(double headPixels, bool pass)
    {
        var issue = Evaluate(new HeadLandmarks(250, 250 + headPixels, 400))
            .Single(i => i.RuleId == HeadGeometryValidator.HeadHeightId);

        Assert.Equal(pass ? ValidationStatus.Pass : ValidationStatus.Fail, issue.Status);
        Assert.Equal(ValidationCategory.Visual, issue.Category);
        Assert.Equal("%", issue.Unit);
    }

    [Fact]
    public void MissingLandmarks_RequireManualReviewInsteadOfGuessing()
    {
        var issues = Evaluate(HeadLandmarks.None);

        Assert.All(issues, i => Assert.Equal(ValidationStatus.ManualReview, i.Status));
        Assert.Contains(issues, i => i.RuleId == HeadGeometryValidator.HeadHeightId);
        Assert.Contains(issues, i => i.RuleId == HeadGeometryValidator.EyeHeightId);
    }

    [Theory]
    [InlineData(800 - 0.56 * 600, ValidationStatus.Pass)]
    [InlineData(800 - 0.69 * 600, ValidationStatus.Pass)]
    [InlineData(800 - 0.50 * 600, ValidationStatus.Fail)]
    [InlineData(800 - 0.75 * 600, ValidationStatus.Fail)]
    public void EyeHeight_IsMeasuredFromBottomEdge(double eyeY, ValidationStatus expected)
    {
        var issue = Evaluate(new HeadLandmarks(250, 610, eyeY)).Single(i => i.RuleId == HeadGeometryValidator.EyeHeightId);

        Assert.Equal(expected, issue.Status);
    }

    [Fact]
    public void HeadOutsideFrame_Fails()
    {
        var issues = Evaluate(new HeadLandmarks(150, 560, 400));

        Assert.Contains(issues, i => i.RuleId == HeadGeometryValidator.HeadInFrameId && i.Status == ValidationStatus.Fail);
    }

    [Fact]
    public void ChinAboveTop_RequiresManualReview()
    {
        var issue = Evaluate(new HeadLandmarks(600, 300, 400)).Single(i => i.RuleId == HeadGeometryValidator.HeadHeightId);

        Assert.Equal(ValidationStatus.ManualReview, issue.Status);
    }

    [Fact]
    public void NoCropInContext_ProducesNothing()
    {
        Assert.Empty(new HeadGeometryValidator().Evaluate(new ValidationContext(), Profile));
    }
}
