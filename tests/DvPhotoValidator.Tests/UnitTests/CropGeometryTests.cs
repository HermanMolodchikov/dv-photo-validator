using DvPhotoValidator.Core.Geometry;

namespace DvPhotoValidator.Tests.UnitTests;

public class CropGeometryTests
{
    [Fact]
    public void Default_IsLargestCenteredSquare()
    {
        Assert.Equal(new CropRect(500, 0, 3000), CropGeometry.Default(4000, 3000));
        Assert.Equal(new CropRect(0, 500, 3000), CropGeometry.Default(3000, 4000));
    }

    [Fact]
    public void Clamp_KeepsCropInsideImage()
    {
        var r = CropGeometry.Clamp(new CropRect(-50, 3500, 800), 1000, 4000);

        Assert.Equal(new CropRect(0, 3200, 800), r);
    }

    [Fact]
    public void Clamp_LimitsSizeToSmallerSide()
    {
        Assert.Equal(1000, CropGeometry.Clamp(new CropRect(0, 0, 5000), 1000, 4000).Size);
        Assert.Equal(CropGeometry.MinCropSize, CropGeometry.Clamp(new CropRect(0, 0, 1), 1000, 4000).Size);
    }

    [Fact]
    public void Pan_StopsAtEdges()
    {
        var r = CropGeometry.Pan(new CropRect(100, 100, 500), 10_000, -10_000, 1000, 1000);

        Assert.Equal(new CropRect(500, 0, 500), r);
    }

    [Fact]
    public void Zoom_KeepsAnchorAtSameRelativePosition()
    {
        var start = new CropRect(1000, 1000, 1000);
        var zoomed = CropGeometry.Zoom(start, 2, anchorX: 1250, anchorY: 1500, 4000, 4000);

        Assert.Equal(500, zoomed.Size);
        Assert.Equal(0.25, (1250 - zoomed.X) / zoomed.Size, 6);
        Assert.Equal(0.5, (1500 - zoomed.Y) / zoomed.Size, 6);
    }

    [Fact]
    public void Zoom_OutIsLimitedByImage()
    {
        var zoomed = CropGeometry.Zoom(new CropRect(0, 0, 500), 0.01, 250, 250, 800, 600);

        Assert.Equal(new CropRect(0, 0, 600), zoomed);
    }
}
