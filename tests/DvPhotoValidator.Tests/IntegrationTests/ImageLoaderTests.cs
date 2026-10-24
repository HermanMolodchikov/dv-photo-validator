using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Imaging;
using DvPhotoValidator.Tests.TestImages;

namespace DvPhotoValidator.Tests.IntegrationTests;

public sealed class ImageLoaderTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly ImageLoader _loader = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Jpeg_WithExifOrientation6_IsRotatedUpright()
    {
        var path = _dir.File("rotated.jpg");
        TestImageFactory.SaveJpeg(TestImageFactory.Quadrants(400, 200), path, orientation: 6);

        var loaded = _loader.Load(path);

        Assert.Equal(200, loaded.Image.Width);
        Assert.Equal(400, loaded.Image.Height);
        Assert.Equal(6, loaded.SourceFacts.Orientation);
        Assert.Equal(200, loaded.SourceFacts.Width);
        // Rotated 90° clockwise: the stored top-left (red) quadrant ends up top-right.
        var (r, g, b) = loaded.Image.GetPixel(190, 10);
        Assert.True(r > 200 && g < 60 && b < 60, $"Expected red, got {r},{g},{b}");
    }

    [Fact]
    public void Jpeg_WithoutMetadata_LoadsAsIs()
    {
        var path = _dir.File("plain.jpg");
        TestImageFactory.SaveJpeg(TestImageFactory.Gradient(320, 240), path);

        var loaded = _loader.Load(path);

        Assert.Equal(ImageFileFormat.Jpeg, loaded.SourceFacts.Format);
        Assert.Equal((320, 240), (loaded.Image.Width, loaded.Image.Height));
        Assert.Equal(1, loaded.SourceFacts.Orientation);
        Assert.Null(loaded.SourceFacts.DateTaken);
        Assert.Equal(ColorSpaceKind.Untagged, loaded.SourceFacts.ColorSpace);
        Assert.False(loaded.SourceFacts.IsGrayscale);
    }

    [Fact]
    public void Jpeg_ExifDateTaken_IsRead()
    {
        var path = _dir.File("dated.jpg");
        TestImageFactory.SaveJpeg(TestImageFactory.Gradient(64, 64), path, exifDate: "2026:05:17 14:30:00");

        Assert.Equal(new DateTime(2026, 5, 17, 14, 30, 0), _loader.Load(path).SourceFacts.DateTaken);
    }

    [Fact]
    public void Jpeg_WithEmbeddedSrgbProfile_IsClassifiedAsSrgb()
    {
        var profilePath = Path.Combine(Environment.SystemDirectory, @"spool\drivers\color\sRGB Color Space Profile.icm");
        if (!File.Exists(profilePath))
        {
            return; // Profile ships with every Windows edition; absent only on stripped-down images.
        }

        var contexts = new ReadOnlyCollection<ColorContext>([new ColorContext(new Uri(profilePath))]);
        var path = _dir.File("srgb.jpg");
        TestImageFactory.SaveJpeg(TestImageFactory.Gradient(64, 64), path, colorContexts: contexts);

        Assert.Equal(ColorSpaceKind.Srgb, _loader.Load(path).SourceFacts.ColorSpace);
    }

    [Fact]
    public void Png_Transparency_IsFlattenedOntoWhite()
    {
        var path = _dir.File("alpha.png");
        TestImageFactory.SaveHalfTransparentPng(100, 50, path);

        var loaded = _loader.Load(path);

        Assert.True(loaded.HadTransparency);
        Assert.Equal(ImageFileFormat.Png, loaded.SourceFacts.Format);
        Assert.Equal(((byte)255, (byte)255, (byte)255), loaded.Image.GetPixel(10, 10));
        Assert.Equal(((byte)255, (byte)0, (byte)0), loaded.Image.GetPixel(90, 10));
    }

    [Fact]
    public void Bmp_And_Tiff_AreSupported()
    {
        var bitmap = TestImageFactory.ToBitmap(TestImageFactory.Gradient(80, 60));
        TestImageFactory.Save(new BmpBitmapEncoder(), bitmap, _dir.File("a.bmp"));
        TestImageFactory.Save(new TiffBitmapEncoder(), bitmap, _dir.File("a.tif"));

        Assert.Equal(ImageFileFormat.Bmp, _loader.Load(_dir.File("a.bmp")).SourceFacts.Format);
        Assert.Equal(ImageFileFormat.Tiff, _loader.Load(_dir.File("a.tif")).SourceFacts.Format);
    }

    [Fact]
    public void GrayscaleJpeg_IsDetected()
    {
        var path = _dir.File("gray.jpg");
        TestImageFactory.SaveGrayJpeg(120, 120, path);

        Assert.True(_loader.Load(path).SourceFacts.IsGrayscale);
    }

    [Fact]
    public void Extension_IsNotTrusted()
    {
        var path = _dir.File("actually-png.jpg");
        TestImageFactory.Save(new PngBitmapEncoder(), TestImageFactory.ToBitmap(TestImageFactory.Gradient(40, 40)), path);

        Assert.Equal(ImageFileFormat.Png, _loader.Load(path).SourceFacts.Format);
    }

    [Fact]
    public void SourceFile_IsNotModified()
    {
        var path = _dir.File("source.jpg");
        TestImageFactory.SaveJpeg(TestImageFactory.Gradient(300, 300), path, orientation: 3);
        var before = SHA256.HashData(File.ReadAllBytes(path));
        var writeTime = File.GetLastWriteTimeUtc(path);

        _loader.Load(path);

        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(path)));
        Assert.Equal(writeTime, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void CorruptFile_GivesReadableError()
    {
        var path = _dir.File("corrupt.jpg");
        var good = _dir.File("good.jpg");
        TestImageFactory.SaveJpeg(TestImageFactory.Noise(200, 200), good);
        File.WriteAllBytes(path, File.ReadAllBytes(good).Take(300).ToArray());

        var ex = Assert.Throws<ImageProcessingException>(() => _loader.Load(path));
        Assert.Contains("повреждён", ex.Message);
    }

    [Fact]
    public void UnknownFormat_EmptyFile_MissingFile_GiveReadableErrors()
    {
        File.WriteAllText(_dir.File("text.jpg"), "hello, this is not an image");
        File.WriteAllBytes(_dir.File("empty.jpg"), []);

        Assert.Contains("Неподдерживаемый формат", Assert.Throws<ImageProcessingException>(() => _loader.Load(_dir.File("text.jpg"))).Message);
        Assert.Contains("пуст", Assert.Throws<ImageProcessingException>(() => _loader.Load(_dir.File("empty.jpg"))).Message);
        Assert.Contains("не найден", Assert.Throws<ImageProcessingException>(() => _loader.Load(_dir.File("missing.jpg"))).Message);
    }

    [Fact]
    public void Limits_FileSizeAndMegapixels_AreEnforced()
    {
        var path = _dir.File("big.jpg");
        TestImageFactory.SaveJpeg(TestImageFactory.Noise(1000, 1000), path);

        var bySize = new ImageLoader(new ImageLoadOptions { MaxFileBytes = 1000 });
        var byPixels = new ImageLoader(new ImageLoadOptions { MaxPixels = 500_000 });

        Assert.Contains("слишком большой", Assert.Throws<ImageProcessingException>(() => bySize.Load(path)).Message);
        Assert.Contains("Мп", Assert.Throws<ImageProcessingException>(() => byPixels.Load(path)).Message);
    }
}
