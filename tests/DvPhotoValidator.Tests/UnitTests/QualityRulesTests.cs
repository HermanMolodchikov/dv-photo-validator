using DvPhotoValidator.Core.Geometry;
using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;
using DvPhotoValidator.Core.Validation;
using DvPhotoValidator.Tests.TestImages;
using DvPhotoValidator.Vision;

namespace DvPhotoValidator.Tests.UnitTests;

public class QualityRulesTests
{
    // Values measured on real compliant photos during calibration (docs/algorithms.md).
    private static readonly QualityMeasurements Good = new(
        new BackgroundMeasurement(0.35, 84.5, 2.6, 2.7, 0.05, 0.002, 0.9),
        new SharpnessMeasurement(160),
        new LightingMeasurement(0, 0.004, 1.5),
        new PoseMeasurement(-3.5, 0.02, 0.57),
        new EyesMeasurement(0.16, 0.15, 38, 38),
        new GlassesMeasurement(0.001, 0));

    private static List<ValidationIssue> Run(QualityMeasurements q, ISet<string>? confirmed = null) =>
        ValidationEngine.CreateDefault().Run(new ValidationContext
        {
            Crop = new CropRect(0, 0, 1000),
            Quality = q,
            ConfirmedByUser = (IReadOnlySet<string>?)confirmed ?? new HashSet<string>(),
        }, RuleProfile.Baseline).Issues.ToList();

    private static ValidationStatus StatusOf(List<ValidationIssue> issues, string id) => issues.Single(i => i.RuleId == id).Status;

    [Fact]
    public void CompliantMeasurements_PassAutomatedChecks()
    {
        var issues = Run(Good);

        foreach (var id in new[] { "BACKGROUND", "SHARPNESS", "NOISE", "LIGHTING", "POSE", "EYES_OPEN" })
        {
            Assert.Equal(ValidationStatus.Pass, StatusOf(issues, id));
        }

        // Glasses absence cannot be proven by the heuristic.
        Assert.Equal(ValidationStatus.ManualReview, StatusOf(issues, "GLASSES"));
        Assert.All(issues, i => Assert.Equal(ValidationCategory.Visual, i.Category));
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("tint")]
    [InlineData("uneven")]
    [InlineData("shadow")]
    [InlineData("objects")]
    public void BackgroundProblems_AreWarnings(string kind)
    {
        var bg = Good.Background!;
        var changed = kind switch
        {
            "dark" => bg with { MedianLightness = 62 },
            "tint" => bg with { MedianChroma = 16 },
            "uneven" => bg with { ZoneSpread = 14 },
            "shadow" => bg with { ShadowFraction = 0.2 },
            _ => bg with { TextureFraction = 0.07 },
        };

        var issue = Run(Good with { Background = changed }).Single(i => i.RuleId == "BACKGROUND");

        Assert.Equal(ValidationStatus.Warning, issue.Status);
        Assert.Contains("Возможные проблемы", issue.Description);
    }

    [Fact]
    public void Blur_Noise_SideLight_Pose_ClosedEyes_Glasses_AreWarnings()
    {
        var issues = Run(Good with
        {
            Background = Good.Background! with { NoiseSigma = 6.7 },
            Sharpness = new SharpnessMeasurement(5),
            Lighting = new LightingMeasurement(0.1, 0, 16),
            Pose = new PoseMeasurement(9, 0.3, 0.8),
            Eyes = new EyesMeasurement(0.02, 0.15, 30, 30),
            Glasses = new GlassesMeasurement(0.15, 0.1),
        });

        foreach (var id in new[] { "SHARPNESS", "NOISE", "LIGHTING", "POSE", "EYES_OPEN", "GLASSES" })
        {
            Assert.Equal(ValidationStatus.Warning, StatusOf(issues, id));
        }

        Assert.Contains("Низкая резкость: мало выраженных деталей в области глаз", issues.Single(i => i.RuleId == "SHARPNESS").Description);
        Assert.DoesNotContain(issues, i => i.Status == ValidationStatus.Fail);
    }

    [Fact]
    public void UncertainEyes_RequireManualReview()
    {
        Assert.Equal(ValidationStatus.ManualReview, StatusOf(Run(Good with { Eyes = new EyesMeasurement(0.07, 0.15, 30, 30) }), "EYES_OPEN"));
        Assert.Equal(ValidationStatus.ManualReview, StatusOf(Run(Good with { Eyes = new EyesMeasurement(0.2, 0.2, 5, 30) }), "EYES_OPEN"));
    }

    [Fact]
    public void ManualChecklist_KeepsOnlyNonAutomatedItems_WhenQualityIsKnown()
    {
        var withQuality = Run(Good).Where(i => i.RuleId.EndsWith("_MANUAL")).Select(i => i.RuleId).ToList();
        var withoutQuality = ValidationEngine.CreateDefault()
            .Run(new ValidationContext { Crop = new CropRect(0, 0, 1000) }, RuleProfile.Baseline)
            .Issues.Where(i => i.RuleId.EndsWith("_MANUAL")).ToList();

        Assert.Equal(["EXPRESSION_MANUAL", "HEADWEAR_MANUAL"], withQuality);
        Assert.Equal(7, withoutQuality.Count);
    }

    [Fact]
    public void Confirmation_TurnsManualReviewIntoPass_ButNeverOverridesWarningsOrFails()
    {
        var bad = Good with { Sharpness = new SharpnessMeasurement(5) };
        var issues = Run(bad, new HashSet<string> { "GLASSES", "EXPRESSION_MANUAL", "SHARPNESS" });

        var glasses = issues.Single(i => i.RuleId == "GLASSES");
        Assert.Equal(ValidationStatus.Pass, glasses.Status);
        Assert.True(glasses.ConfirmedByUser);
        Assert.True(glasses.CanConfirm);
        Assert.StartsWith("Подтверждено вами", glasses.Description);
        Assert.Equal(ValidationStatus.Pass, StatusOf(issues, "EXPRESSION_MANUAL"));
        Assert.Equal(ValidationStatus.Warning, StatusOf(issues, "SHARPNESS"));
    }

    [Fact]
    public void MissingLandmarks_CannotBeConfirmed_OnlyMeasured()
    {
        var issues = ValidationEngine.CreateDefault().Run(new ValidationContext
        {
            Crop = new CropRect(0, 0, 1000),
            ConfirmedByUser = new HashSet<string> { HeadGeometryValidator.HeadHeightId },
        }, RuleProfile.Baseline).Issues;

        var head = issues.Single(i => i.RuleId == HeadGeometryValidator.HeadHeightId);
        Assert.Equal(ValidationStatus.ManualReview, head.Status);
        Assert.False(head.CanConfirm);
    }

    [Fact]
    public void Analyzer_OnSyntheticPortrait_MeasuresBackgroundAndDetectsTint()
    {
        var face = new DetectedFace(420, 430, 360, 500, 0.95,
            new PointD(530, 600), new PointD(670, 600), new PointD(600, 690), new PointD(550, 780), new PointD(650, 780));
        var landmarks = new HeadLandmarks(330, 930, 600);
        var crop = AutoCrop.Compute(landmarks, 600, 1200, 1500, RuleProfile.Baseline)!.Crop;
        var analyzer = new PhotoQualityAnalyzer();
        var white = TestImageFactory.Portrait(1200, 1500);
        var tinted = Recolor(white, (246, 246, 244), (200, 225, 255));

        var good = analyzer.Analyze(white, crop, face, landmarks, 600).Background!;
        var blue = analyzer.Analyze(tinted, crop, face, landmarks, 600).Background!;

        Assert.True(good.MedianLightness > 90, $"L = {good.MedianLightness}");
        Assert.True(good.MedianChroma < 3);
        Assert.True(good.TextureFraction < 0.01);
        Assert.True(blue.MedianChroma > QualityThresholds.MaxBackgroundChroma);
    }

    private static RgbImage Recolor(RgbImage source, (byte R, byte G, byte B) from, (byte R, byte G, byte B) to)
    {
        var result = new RgbImage(source.Width, source.Height, (byte[])source.Pixels.Clone());
        for (var y = 0; y < result.Height; y++)
        {
            for (var x = 0; x < result.Width; x++)
            {
                if (result.GetPixel(x, y) == from)
                {
                    result.SetPixel(x, y, to.R, to.G, to.B);
                }
            }
        }

        return result;
    }
}
