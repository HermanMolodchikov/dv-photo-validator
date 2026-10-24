using DvPhotoValidator.Core.Geometry;
using DvPhotoValidator.Core.Models;

namespace DvPhotoValidator.Core.Validation;

/// <summary>Capture date read from the source photo metadata; null TakenAt means it was not found.</summary>
public sealed record CaptureDateInfo(DateTime? TakenAt);

/// <summary>
/// Everything a rule may look at. Rules ignore a context that lacks their inputs, so the same engine
/// checks a file on disk (File set) or the current crop in the editor (Crop set).
/// </summary>
public sealed record ValidationContext
{
    /// <summary>Facts about a file on disk: the source file or the exported JPEG.</summary>
    public ImageFileFacts? File { get; init; }

    /// <summary>Current crop frame in source pixels.</summary>
    public CropRect? Crop { get; init; }

    public int SourceWidth { get; init; }

    public int SourceHeight { get; init; }

    public HeadLandmarks? Landmarks { get; init; }

    public CaptureDateInfo? Capture { get; init; }

    /// <summary>Automatic face analysis; null when it was not run (e.g. the model is unavailable).</summary>
    public FaceAnalysis? Face { get; init; }

    public DateTime Now { get; init; } = DateTime.Now;

    public static ValidationContext ForFile(ImageFileFacts facts) => new() { File = facts };
}
