using DvPhotoValidator.Core.Geometry;
using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;

namespace DvPhotoValidator.Core.Interfaces;

public interface IImageLoader
{
    /// <summary>Decodes the file without modifying it. Throws <see cref="ImageProcessingException"/> on expected failures.</summary>
    LoadedImage Load(string path);
}

public interface IImageInspector
{
    /// <summary>Re-reads a file from disk and measures what is actually stored in it.</summary>
    ImageFileFacts Inspect(string path);
}

public interface ICropService
{
    /// <summary>Cuts the square region out of the source and resamples it to size × size.</summary>
    RgbImage Render(RgbImage source, CropRect crop, int size);
}

public interface IJpegExporter
{
    /// <summary>Encodes with the highest quality that fits the profile's file size limit.</summary>
    JpegEncodeResult EncodeToFit(RgbImage image, RuleProfile profile);

    /// <summary>Encodes, writes to a temporary file, re-verifies it and only then moves it to <paramref name="path"/>.</summary>
    ExportResult Export(RgbImage image, string path, RuleProfile profile);
}
