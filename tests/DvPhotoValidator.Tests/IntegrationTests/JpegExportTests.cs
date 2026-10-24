using DvPhotoValidator.Core.Geometry;
using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;
using DvPhotoValidator.Core.Validation;
using DvPhotoValidator.Imaging;
using DvPhotoValidator.Tests.TestImages;

namespace DvPhotoValidator.Tests.IntegrationTests;

public sealed class JpegExportTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly JpegExporter _exporter = new();
    private readonly RuleProfile _profile = RuleProfile.Baseline;

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Export_SmoothImage_UsesMaxQuality_AndPassesReverification()
    {
        var path = _dir.File("dv_photo.jpg");

        var result = _exporter.Export(TestImageFactory.Gradient(600, 600), path, _profile);

        Assert.Equal(_profile.MaxJpegQuality, result.Quality);
        Assert.True(File.Exists(path));
        Assert.Equal(new FileInfo(path).Length, result.FileBytes);
        Assert.False(result.Report.HasFailures);
        Assert.Equal(ImageFileFormat.Jpeg, result.Facts.Format);
        Assert.Equal((600, 600), (result.Facts.Width, result.Facts.Height));
        Assert.Equal(ColorSpaceKind.Untagged, result.Facts.ColorSpace);
        Assert.Equal(24, result.Facts.BitsPerPixel);
    }

    [Fact]
    public void Export_NoisyImage_ReducesQualityToFitLimit()
    {
        var path = _dir.File("noisy.jpg");

        var result = _exporter.Export(TestImageFactory.Noise(600, 600), path, _profile);

        Assert.True(result.FileBytes <= _profile.MaxFileBytes);
        Assert.True(result.Quality < _profile.MaxJpegQuality);
        // Binary search must find the highest quality that fits: one step higher must not fit.
        Assert.True(JpegExporter.Encode(TestImageFactory.Noise(600, 600), result.Quality + 1).Length > _profile.MaxFileBytes);
        Assert.Contains(result.Report.Issues, i => i.RuleId == "EXPORT_QUALITY");
    }

    [Fact]
    public void Export_ImpossibleLimit_ThrowsAndLeavesNoFiles()
    {
        var tiny = _profile with { MaxFileBytes = 2048 };
        var path = _dir.File("never.jpg");

        Assert.Throws<ImageProcessingException>(() => _exporter.Export(TestImageFactory.Noise(600, 600), path, tiny));

        Assert.Empty(Directory.GetFiles(_dir.Path));
    }

    [Fact]
    public void Export_LeavesNoTemporaryFiles_AndOverwritesTarget()
    {
        var path = _dir.File("dv_photo.jpg");
        File.WriteAllText(path, "old content");

        _exporter.Export(TestImageFactory.Gradient(600, 600), path, _profile);

        Assert.Equal([path], Directory.GetFiles(_dir.Path));
        Assert.Equal(ImageFileFormat.Jpeg, ImageFormatDetector.Detect(File.ReadAllBytes(path)));
    }

    [Fact]
    public void Export_FailedVerification_DoesNotTouchExistingTarget()
    {
        var path = _dir.File("dv_photo.jpg");
        File.WriteAllText(path, "keep me");
        var rejectAll = new ValidationEngine([new AlwaysFailRule()]);
        var exporter = new JpegExporter(engine: rejectAll);

        var ex = Assert.Throws<ExportVerificationException>(() => exporter.Export(TestImageFactory.Gradient(600, 600), path, _profile));

        Assert.True(ex.Report.HasFailures);
        Assert.Equal("keep me", File.ReadAllText(path));
        Assert.Equal([path], Directory.GetFiles(_dir.Path));
    }

    [Fact]
    public void Export_ReadOnlyTarget_GivesReadableError()
    {
        var path = _dir.File("readonly.jpg");
        File.WriteAllText(path, "x");
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            var ex = Assert.Throws<ImageProcessingException>(() => _exporter.Export(TestImageFactory.Gradient(600, 600), path, _profile));
            Assert.False(string.IsNullOrWhiteSpace(ex.Message));
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
    }

    [Fact]
    public void Export_WrongSize_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => _exporter.Export(TestImageFactory.Gradient(500, 500), _dir.File("x.jpg"), _profile));
    }

    [Fact]
    public void FullPipeline_LargePortraitPng_ToCompliantJpeg()
    {
        // 3000×4000 "photo" with EXIF-free PNG source, cropped and exported.
        var sourcePath = _dir.File("source.png");
        var source = TestImageFactory.Noise(3000, 4000, seed: 7);
        TestImageFactory.Save(new System.Windows.Media.Imaging.PngBitmapEncoder(), TestImageFactory.ToBitmap(source), sourcePath);
        var loader = new ImageLoader();

        var loaded = loader.Load(sourcePath);
        var sourceReport = ValidationEngine.CreateDefault().Run(ValidationContext.ForFile(loaded.SourceFacts), _profile);
        var crop = CropGeometry.Zoom(CropGeometry.Default(3000, 4000), 1.5, 1500, 1800, 3000, 4000);
        var rendered = new CropService().Render(loaded.Image, crop, _profile.ExportSize);
        var result = _exporter.Export(rendered, _dir.File("dv_photo.jpg"), _profile);
        var reopened = loader.Inspect(result.FilePath);

        Assert.True(sourceReport.HasFailures); // PNG, wrong size, too big
        Assert.False(result.Report.HasFailures);
        Assert.Equal(ImageFileFormat.Jpeg, reopened.Format);
        Assert.Equal((600, 600), (reopened.Width, reopened.Height));
        Assert.True(reopened.FileBytes <= _profile.MaxFileBytes);
    }

    private sealed class AlwaysFailRule : IValidationRule
    {
        public string Id => "ALWAYS_FAIL";

        public IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile)
        {
            yield return new ValidationIssue(Id, ValidationStatus.Fail, "fail", "fail");
        }
    }
}
