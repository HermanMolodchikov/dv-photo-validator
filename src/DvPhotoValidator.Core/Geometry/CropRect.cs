namespace DvPhotoValidator.Core.Geometry;

/// <summary>Square crop region in source-image pixel coordinates.</summary>
public readonly record struct CropRect(double X, double Y, double Size)
{
    public double Right => X + Size;

    public double Bottom => Y + Size;

    public double CenterX => X + Size / 2;

    public double CenterY => Y + Size / 2;
}
