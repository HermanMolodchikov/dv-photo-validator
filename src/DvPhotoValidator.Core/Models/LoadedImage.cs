namespace DvPhotoValidator.Core.Models;

/// <summary>Source photo decoded into memory, orientation applied and converted to sRGB.</summary>
public sealed record LoadedImage(string FilePath, RgbImage Image, ImageFileFacts SourceFacts)
{
    /// <summary>Transparent pixels were composited onto white during decoding.</summary>
    public bool HadTransparency { get; init; }

    /// <summary>An embedded color profile was used to convert pixels to sRGB.</summary>
    public bool WasColorConverted { get; init; }
}
