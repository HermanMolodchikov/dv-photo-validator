using DvPhotoValidator.Core.Geometry;
using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;
using DvPhotoValidator.Core.Validation;
using DvPhotoValidator.Tests.TestImages;
using DvPhotoValidator.Vision;

namespace DvPhotoValidator.Tests.UnitTests;

public class VisionTests
{
    // Geometry of TestImageFactory.Portrait(1200, 1500): head ellipse centered (600, 630), radii 204 × 300,
    // so the top of the "hair" is at y = 330 and the bottom of the head at y = 930.
    private static readonly DetectedFace PortraitFace = new(
        420, 430, 360, 500, 0.95,
        RightEye: new PointD(530, 600), LeftEye: new PointD(670, 600), Nose: new PointD(600, 690),
        MouthRight: new PointD(550, 780), MouthLeft: new PointD(650, 780));

    [Fact]
    public void YuNetDecoder_MapsGridCellToPixels()
    {
        var outputs = EmptyOutputs();
        // Stride 32, row 2, column 3 → cell index 2 * 20 + 3 = 43.
        const int i = 43;
        outputs["cls_32"][i] = 0.81f;
        outputs["obj_32"][i] = 1f;
        outputs["bbox_32"][i * 4] = 0.5f;
        outputs["bbox_32"][i * 4 + 1] = 0.5f;
        outputs["bbox_32"][i * 4 + 2] = (float)Math.Log(2);
        outputs["bbox_32"][i * 4 + 3] = (float)Math.Log(3);
        outputs["kps_32"][i * 10] = 0.25f;
        outputs["kps_32"][i * 10 + 1] = 0.75f;

        var face = Assert.Single(YuNetDecoder.Decode(outputs, 640, 0.5f));

        Assert.Equal(0.9, face.Score, 3);
        Assert.Equal(112 - 32, face.X, 3);
        Assert.Equal(80 - 48, face.Y, 3);
        Assert.Equal(64, face.Width, 3);
        Assert.Equal(96, face.Height, 3);
        Assert.Equal(new PointD(104, 88), face.RightEye);
    }

    [Fact]
    public void YuNetDecoder_IgnoresLowScores()
    {
        var outputs = EmptyOutputs();
        outputs["cls_8"][0] = 0.2f;
        outputs["obj_8"][0] = 0.2f;

        Assert.Empty(YuNetDecoder.Decode(outputs, 640, 0.6f));
    }

    [Fact]
    public void Nms_RemovesOverlappingDuplicates_KeepsSeparateFaces()
    {
        DetectedFace Box(double x, double score) => PortraitFace with { X = x, Y = 0, Width = 100, Height = 100, Score = score };

        var kept = YuNetDecoder.Suppress([Box(0, 0.8), Box(5, 0.9), Box(500, 0.7)], 0.3);

        Assert.Equal(2, kept.Count);
        Assert.Equal(0.9, kept[0].Score);
        Assert.Equal(500, kept[1].X);
    }

    [Fact]
    public void HeadTop_OnUniformBackground_FindsTopOfHair()
    {
        var image = TestImageFactory.Portrait(1200, 1500);

        var result = HeadTopEstimator.Estimate(image, PortraitFace, chinY: 930);

        Assert.InRange(result.Y, 322, 338);
        Assert.True(result.Confidence >= 0.6, $"confidence {result.Confidence}");
        Assert.Null(result.Note);
    }

    [Fact]
    public void HeadTop_OnBusyBackground_LowersConfidence()
    {
        var image = TestImageFactory.Noise(1200, 1500);

        var result = HeadTopEstimator.Estimate(image, PortraitFace, chinY: 930);

        Assert.True(result.Confidence < HeadGeometryValidator.MinAutomaticConfidence);
        Assert.NotNull(result.Note);
    }

    [Fact]
    public void Landmarks_AreAutomatic_WithEyeLineFromKeypoints()
    {
        var (landmarks, _) = LandmarkDetector.Estimate(TestImageFactory.Portrait(1200, 1500), PortraitFace);

        Assert.Equal(LandmarkSource.Automatic, landmarks.Source);
        Assert.Equal(600, landmarks.EyeLineY);
        // Chin: mean of box bottom (930) and 780 + 0.75 × 180 = 915.
        Assert.Equal(922.5, landmarks.ChinY!.Value, 3);
        Assert.InRange(landmarks.HeadTopY!.Value, 322, 338);
        Assert.InRange(landmarks.Confidence!.Value, 0.6, 1);
    }

    [Fact]
    public void AutoCrop_PlacesHeadAndEyesInsideAllowedRanges()
    {
        var landmarks = new HeadLandmarks(330, 930, 600) { Source = LandmarkSource.Automatic, Confidence = 0.9 };
        var profile = RuleProfile.Baseline;

        var result = AutoCrop.Compute(landmarks, 600, 1200, 1500, profile)!;
        var report = ValidationEngine.CreateDefault().Run(new ValidationContext { Crop = result.Crop, Landmarks = landmarks }, profile);

        Assert.False(result.WasConstrained);
        Assert.Equal(600, result.Crop.CenterX, 3);
        Assert.Equal(ValidationStatus.Pass, report.Issues.Single(i => i.RuleId == HeadGeometryValidator.HeadHeightId).Status);
        Assert.Equal(ValidationStatus.Pass, report.Issues.Single(i => i.RuleId == HeadGeometryValidator.EyeHeightId).Status);
        Assert.DoesNotContain(report.Issues, i => i.RuleId == HeadGeometryValidator.HeadInFrameId);
    }

    [Fact]
    public void AutoCrop_FaceAtImageEdge_IsConstrainedButInside()
    {
        var landmarks = new HeadLandmarks(330, 930, 600);

        var result = AutoCrop.Compute(landmarks, 100, 1200, 1500, RuleProfile.Baseline)!;

        Assert.True(result.WasConstrained);
        Assert.Equal(0, result.Crop.X);
    }

    [Fact]
    public void AutoCrop_IncompleteLandmarks_ReturnsNull()
    {
        Assert.Null(AutoCrop.Compute(new HeadLandmarks(330, null, 600), 600, 1200, 1500, RuleProfile.Baseline));
    }

    [Fact]
    public void LowConfidenceAutomaticLandmarks_RequireManualReview_ButShowMeasurement()
    {
        var landmarks = new HeadLandmarks(330, 930, 600) { Source = LandmarkSource.Automatic, Confidence = 0.4 };
        var context = new ValidationContext { Crop = new CropRect(96, 222, 1008), Landmarks = landmarks };

        var issue = new HeadGeometryValidator().Evaluate(context, RuleProfile.Baseline).Single(i => i.RuleId == HeadGeometryValidator.HeadHeightId);

        Assert.Equal(ValidationStatus.ManualReview, issue.Status);
        Assert.NotNull(issue.MeasuredValue);
        Assert.Contains("ручная проверка", issue.Description);
    }

    [Theory]
    [InlineData(0, ValidationStatus.ManualReview)]
    [InlineData(1, ValidationStatus.Pass)]
    [InlineData(2, ValidationStatus.Warning)]
    public void FaceDetectionRule_ReportsFaceCount(int count, ValidationStatus expected)
    {
        var faces = Enumerable.Repeat(PortraitFace, count).ToList();
        var context = new ValidationContext { Crop = new CropRect(0, 0, 1200), Face = new FaceAnalysis(faces, null) };

        Assert.Equal(expected, new FaceDetectionRule().Evaluate(context, RuleProfile.Baseline).Single().Status);
    }

    [Theory]
    [InlineData(100, ValidationStatus.Pass)]     // eyes at x = 600, crop center 600
    [InlineData(200, ValidationStatus.Warning)]  // crop center 700 → 10% offset
    public void FaceCenterRule_ChecksHorizontalOffset(double cropX, ValidationStatus expected)
    {
        var context = new ValidationContext { Crop = new CropRect(cropX, 200, 1000), Face = new FaceAnalysis([PortraitFace], null) };

        Assert.Equal(expected, new FaceCenterRule().Evaluate(context, RuleProfile.Baseline).Single().Status);
    }

    private static Dictionary<string, float[]> EmptyOutputs()
    {
        var outputs = new Dictionary<string, float[]>();
        foreach (var s in YuNetDecoder.Strides)
        {
            var cells = (640 / s) * (640 / s);
            outputs[$"cls_{s}"] = new float[cells];
            outputs[$"obj_{s}"] = new float[cells];
            outputs[$"bbox_{s}"] = new float[cells * 4];
            outputs[$"kps_{s}"] = new float[cells * 10];
        }

        return outputs;
    }
}
