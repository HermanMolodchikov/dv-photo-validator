using System.Text;
using DvPhotoValidator.Core.Models;

namespace DvPhotoValidator.Imaging;

/// <summary>Detects the real format by file signature; the file extension is not trusted.</summary>
public static class ImageFormatDetector
{
    public static ImageFileFormat Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ImageFileFormat.Jpeg;
        }

        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ImageFileFormat.Png;
        }

        if (header.Length >= 2 && header[0] == (byte)'B' && header[1] == (byte)'M')
        {
            return ImageFileFormat.Bmp;
        }

        if (header.Length >= 4 &&
            ((header[0] == (byte)'I' && header[1] == (byte)'I' && header[2] == 42 && header[3] == 0) ||
             (header[0] == (byte)'M' && header[1] == (byte)'M' && header[2] == 0 && header[3] == 42)))
        {
            return ImageFileFormat.Tiff;
        }

        if (header.Length >= 12 && Ascii(header[..4]) == "RIFF" && Ascii(header[8..12]) == "WEBP")
        {
            return ImageFileFormat.WebP;
        }

        if (header.Length >= 12 && Ascii(header[4..8]) == "ftyp")
        {
            var brand = Ascii(header[8..12]);
            if (brand is "heic" or "heix" or "hevc" or "hevx" or "heim" or "heis" or "mif1" or "msf1")
            {
                return ImageFileFormat.Heif;
            }
        }

        return ImageFileFormat.Unknown;
    }

    private static string Ascii(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes);
}
