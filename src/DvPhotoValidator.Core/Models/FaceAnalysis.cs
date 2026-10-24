namespace DvPhotoValidator.Core.Models;

public readonly record struct PointD(double X, double Y);

/// <summary>
/// Face found by the detector, in source-image pixels. The box covers roughly eyebrows to chin,
/// not the hair, so it is not used directly as the head height.
/// </summary>
public sealed record DetectedFace(
    double X,
    double Y,
    double Width,
    double Height,
    double Score,
    PointD RightEye,
    PointD LeftEye,
    PointD Nose,
    PointD MouthRight,
    PointD MouthLeft)
{
    public double Bottom => Y + Height;

    public double Area => Width * Height;

    public PointD EyeCenter => new((RightEye.X + LeftEye.X) / 2, (RightEye.Y + LeftEye.Y) / 2);

    public PointD MouthCenter => new((MouthRight.X + MouthLeft.X) / 2, (MouthRight.Y + MouthLeft.Y) / 2);
}

/// <summary>Result of automatic face analysis. Faces are ordered by size, the largest first.</summary>
public sealed record FaceAnalysis(IReadOnlyList<DetectedFace> Faces, HeadLandmarks? Landmarks)
{
    public DetectedFace? Primary => Faces.Count > 0 ? Faces[0] : null;

    /// <summary>Human-readable remarks about how landmarks were estimated.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}
