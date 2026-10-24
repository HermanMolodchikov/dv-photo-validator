namespace DvPhotoValidator.Core.Models;

public enum LandmarkSource
{
    Manual,
    Automatic,
}

/// <summary>
/// Vertical landmarks in source-image pixel coordinates (after EXIF orientation).
/// Null means the landmark has not been placed.
/// </summary>
public sealed record HeadLandmarks(double? HeadTopY, double? ChinY, double? EyeLineY)
{
    public LandmarkSource Source { get; init; } = LandmarkSource.Manual;

    /// <summary>Model confidence 0..1 for automatic landmarks; null for manual ones.</summary>
    public double? Confidence { get; init; }

    public static HeadLandmarks None { get; } = new(null, null, null);
}
