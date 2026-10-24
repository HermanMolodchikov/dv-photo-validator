using DvPhotoValidator.Core.Geometry;
using DvPhotoValidator.Core.Imaging;
using DvPhotoValidator.Core.Interfaces;
using DvPhotoValidator.Core.Models;

namespace DvPhotoValidator.Imaging;

/// <summary>Cuts the square crop out of the source and resamples it (see <see cref="Resampler"/>).</summary>
public sealed class CropService : ICropService
{
    public RgbImage Render(RgbImage source, CropRect crop, int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        var clamped = CropGeometry.Clamp(crop, source.Width, source.Height);
        return Resampler.Resample(source, clamped.X, clamped.Y, clamped.Size, clamped.Size, size, size);
    }
}
