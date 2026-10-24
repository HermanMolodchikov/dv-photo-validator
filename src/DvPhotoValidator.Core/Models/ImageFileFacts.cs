namespace DvPhotoValidator.Core.Models;

public enum ImageFileFormat
{
    Unknown,
    Jpeg,
    Png,
    Bmp,
    Tiff,
    WebP,
    Heif,
}

public enum ColorSpaceKind
{
    /// <summary>No embedded ICC profile; by convention interpreted as sRGB.</summary>
    Untagged,
    Srgb,
    Other,
}

/// <summary>Measured properties of an image file on disk, as actually decoded (after EXIF orientation).</summary>
public sealed record ImageFileFacts(
    ImageFileFormat Format,
    int Width,
    int Height,
    long FileBytes,
    bool IsGrayscale,
    ColorSpaceKind ColorSpace,
    int BitsPerPixel)
{
    public DateTime? DateTaken { get; init; }

    public ushort Orientation { get; init; } = 1;
}
