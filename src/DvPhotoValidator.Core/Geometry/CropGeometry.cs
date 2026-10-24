namespace DvPhotoValidator.Core.Geometry;

/// <summary>
/// Pure math for the square crop frame. The crop always stays inside the image:
/// the program never pads or invents pixels outside the original photo.
/// </summary>
public static class CropGeometry
{
    public const double MinCropSize = 64;

    /// <summary>Largest centered square that fits the image.</summary>
    public static CropRect Default(int imageWidth, int imageHeight)
    {
        double size = Math.Min(imageWidth, imageHeight);
        return new CropRect((imageWidth - size) / 2, (imageHeight - size) / 2, size);
    }

    public static CropRect Clamp(CropRect rect, int imageWidth, int imageHeight)
    {
        double maxSize = Math.Min(imageWidth, imageHeight);
        var minSize = Math.Min(MinCropSize, maxSize);
        var size = Math.Clamp(rect.Size, minSize, maxSize);
        var x = Math.Clamp(rect.X, 0, imageWidth - size);
        var y = Math.Clamp(rect.Y, 0, imageHeight - size);
        return new CropRect(x, y, size);
    }

    /// <summary>Moves the frame by (dx, dy) source pixels.</summary>
    public static CropRect Pan(CropRect rect, double dx, double dy, int imageWidth, int imageHeight) =>
        Clamp(rect with { X = rect.X + dx, Y = rect.Y + dy }, imageWidth, imageHeight);

    /// <summary>
    /// Zooms around an anchor point given in source pixels. factor &gt; 1 zooms in (smaller crop).
    /// The anchor keeps its relative position inside the frame unless clamping prevents it.
    /// </summary>
    public static CropRect Zoom(CropRect rect, double factor, double anchorX, double anchorY, int imageWidth, int imageHeight)
    {
        if (factor <= 0 || double.IsNaN(factor) || double.IsInfinity(factor))
        {
            throw new ArgumentOutOfRangeException(nameof(factor));
        }

        double maxSize = Math.Min(imageWidth, imageHeight);
        var newSize = Math.Clamp(rect.Size / factor, Math.Min(MinCropSize, maxSize), maxSize);
        var relX = (anchorX - rect.X) / rect.Size;
        var relY = (anchorY - rect.Y) / rect.Size;
        return Clamp(new CropRect(anchorX - relX * newSize, anchorY - relY * newSize, newSize), imageWidth, imageHeight);
    }
}
