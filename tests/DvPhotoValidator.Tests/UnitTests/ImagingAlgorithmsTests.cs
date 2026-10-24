using DvPhotoValidator.Core.Geometry;
using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Imaging;
using DvPhotoValidator.Tests.TestImages;

namespace DvPhotoValidator.Tests.UnitTests;

public class ImagingAlgorithmsTests
{
    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, ImageFileFormat.Jpeg)]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, ImageFileFormat.Png)]
    [InlineData(new byte[] { (byte)'B', (byte)'M', 0, 0 }, ImageFileFormat.Bmp)]
    [InlineData(new byte[] { (byte)'I', (byte)'I', 42, 0 }, ImageFileFormat.Tiff)]
    [InlineData(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' }, ImageFileFormat.WebP)]
    [InlineData(new byte[] { 0, 0, 0, 24, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'h', (byte)'e', (byte)'i', (byte)'c' }, ImageFileFormat.Heif)]
    [InlineData(new byte[] { 1, 2, 3, 4 }, ImageFileFormat.Unknown)]
    public void FormatDetector_UsesSignatures(byte[] header, ImageFileFormat expected)
    {
        Assert.Equal(expected, ImageFormatDetector.Detect(header));
    }

    [Theory]
    // Expected colors at (top-left, top-right, bottom-left, bottom-right) after orientation is applied
    // to a 4×2 quadrant image: R G / B Y.
    [InlineData(1, "RGBY")]
    [InlineData(2, "GRYB")]
    [InlineData(3, "YBGR")]
    [InlineData(4, "BYRG")]
    [InlineData(5, "RBGY")]
    [InlineData(6, "BRYG")]
    [InlineData(7, "YGBR")]
    [InlineData(8, "GYRB")]
    public void Orientation_AllEightExifValues(ushort orientation, string expectedCorners)
    {
        var result = ImageNormalizer.ApplyOrientation(TestImageFactory.Quadrants(4, 2), orientation);

        var swap = orientation >= 5;
        Assert.Equal(swap ? 2 : 4, result.Width);
        Assert.Equal(swap ? 4 : 2, result.Height);
        var corners = new string(
        [
            Name(result.GetPixel(0, 0)),
            Name(result.GetPixel(result.Width - 1, 0)),
            Name(result.GetPixel(0, result.Height - 1)),
            Name(result.GetPixel(result.Width - 1, result.Height - 1)),
        ]);
        Assert.Equal(expectedCorners, corners);
    }

    [Fact]
    public void Resample_ProducesRequestedSize_AndKeepsUniformColor()
    {
        var source = TestImageFactory.Solid(1500, 1000, 250, 250, 245);

        var result = new CropService().Render(source, new CropRect(300, 100, 900), 600);

        Assert.Equal(600, result.Width);
        Assert.Equal(600, result.Height);
        Assert.All(Enumerable.Range(0, 600), x => Assert.Equal(((byte)250, (byte)250, (byte)245), result.GetPixel(x, 300)));
    }

    [Fact]
    public void Resample_Upscale_PreservesGradientDirection()
    {
        var source = TestImageFactory.Gradient(200, 200);

        var result = new CropService().Render(source, new CropRect(0, 0, 200), 600);

        Assert.True(result.GetPixel(50, 300).R < result.GetPixel(300, 300).R);
        Assert.True(result.GetPixel(300, 300).R < result.GetPixel(550, 300).R);
        Assert.True(result.GetPixel(300, 50).G < result.GetPixel(300, 550).G);
    }

    [Fact]
    public void Resample_SelectsTheRequestedRegion()
    {
        var source = TestImageFactory.Quadrants(1000, 1000);

        // Bottom-right quadrant only → yellow everywhere.
        var result = new CropService().Render(source, new CropRect(500, 500, 500), 100);

        Assert.Equal('Y', Name(result.GetPixel(50, 50)));
        Assert.Equal('Y', Name(result.GetPixel(2, 2)));
    }

    [Fact]
    public void Grayscale_DetectsColorlessImages()
    {
        Assert.True(TestImageFactory.Solid(100, 100, 128, 130, 127).IsGrayscale());
        Assert.False(TestImageFactory.Gradient(100, 100).IsGrayscale());
    }

    private static char Name((byte R, byte G, byte B) p) => p switch
    {
        ( > 200, < 50, < 50) => 'R',
        ( < 50, > 200, < 50) => 'G',
        ( < 50, < 50, > 200) => 'B',
        ( > 200, > 200, < 50) => 'Y',
        _ => '?',
    };
}
