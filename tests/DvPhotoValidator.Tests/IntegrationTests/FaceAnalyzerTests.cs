using DvPhotoValidator.Core.Geometry;
using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;
using DvPhotoValidator.Imaging;
using DvPhotoValidator.Tests.TestImages;
using DvPhotoValidator.Vision;

namespace DvPhotoValidator.Tests.IntegrationTests;

/// <summary>Runs the real YuNet model through ONNX Runtime.</summary>
public sealed class FaceAnalyzerTests : IDisposable
{
    private readonly FaceAnalyzer _analyzer = FaceAnalyzer.TryCreateDefault(out _)
        ?? throw new InvalidOperationException("Bundled face model not found in test output.");

    public void Dispose() => _analyzer.Dispose();

    [Fact]
    public void ImageWithoutFace_ReturnsNoFaces_AndNoInventedLandmarks()
    {
        var result = _analyzer.Analyze(TestImageFactory.Gradient(800, 1000));

        Assert.Empty(result.Faces);
        Assert.Null(result.Landmarks);
    }

    [Fact]
    public void TinyAndWideImages_DoNotCrash()
    {
        _analyzer.Analyze(TestImageFactory.Solid(10, 10, 255, 255, 255));
        _analyzer.Analyze(TestImageFactory.Solid(3000, 200, 240, 240, 240));
    }

    /// <summary>
    /// Real-photo check. Set DVPV_FACE_SAMPLE to the path of a frontal portrait; the test is a no-op otherwise,
    /// so no photos of people are stored in the repository.
    /// </summary>
    [Fact]
    public void RealPortrait_IsDetected_AndAutoCropFitsRanges()
    {
        if (Environment.GetEnvironmentVariable("DVPV_FACE_SAMPLE") is not { Length: > 0 } path)
        {
            return;
        }

        var image = new ImageLoader().Load(path).Image;
        var result = _analyzer.Analyze(image);

        var face = Assert.Single(result.Faces);
        Assert.True(face.Score > 0.8);
        var landmarks = Assert.IsType<HeadLandmarks>(result.Landmarks);
        Assert.True(landmarks.HeadTopY < landmarks.EyeLineY && landmarks.EyeLineY < landmarks.ChinY);
        var crop = AutoCrop.Compute(landmarks, face.EyeCenter.X, image.Width, image.Height, RuleProfile.Baseline);
        Assert.NotNull(crop);
    }
}
