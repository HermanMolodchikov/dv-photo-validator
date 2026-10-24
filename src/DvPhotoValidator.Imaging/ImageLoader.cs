using DvPhotoValidator.Core.Interfaces;
using DvPhotoValidator.Core.Models;

namespace DvPhotoValidator.Imaging;

public sealed record ImageLoadOptions
{
    public long MaxFileBytes { get; init; } = 100L * 1024 * 1024;

    public long MaxPixels { get; init; } = 100_000_000;

    public static ImageLoadOptions Default { get; } = new();
}

/// <summary>
/// Reads the source file once into memory (the original is never written to), detects the real format,
/// enforces memory limits, applies EXIF orientation and converts colors to sRGB.
/// </summary>
public sealed class ImageLoader : IImageLoader, IImageInspector
{
    private readonly ImageLoadOptions _options;

    public ImageLoader(ImageLoadOptions? options = null)
    {
        _options = options ?? ImageLoadOptions.Default;
    }

    public LoadedImage Load(string path)
    {
        var data = ReadFile(path);
        var format = ImageFormatDetector.Detect(data);
        if (format == ImageFileFormat.Unknown)
        {
            throw new ImageProcessingException("Неподдерживаемый формат файла. Поддерживаются JPEG, PNG, WebP, BMP и TIFF.");
        }

        using var stream = new MemoryStream(data, writable: false);
        var info = WicReader.ReadFirstFrame(stream, format);
        var pixels = (long)info.Frame.PixelWidth * info.Frame.PixelHeight;
        if (pixels > _options.MaxPixels)
        {
            throw new ImageProcessingException(
                $"Изображение слишком большое: {pixels / 1_000_000.0:0.#} Мп, допустимо до {_options.MaxPixels / 1_000_000.0:0.#} Мп.");
        }

        var decoded = WicReader.DecodeUpright(info);
        var facts = new ImageFileFacts(
            format,
            decoded.Image.Width,
            decoded.Image.Height,
            data.LongLength,
            info.IsGrayFormat || decoded.Image.IsGrayscale(),
            info.ColorSpace,
            info.Frame.Format.BitsPerPixel)
        {
            DateTaken = info.DateTaken,
            Orientation = info.Orientation,
        };

        return new LoadedImage(Path.GetFullPath(path), decoded.Image, facts)
        {
            HadTransparency = decoded.HadTransparency,
            WasColorConverted = decoded.WasConverted,
        };
    }

    /// <summary>Re-reads a file from disk and returns its measured properties.</summary>
    public ImageFileFacts Inspect(string path) => Load(path).SourceFacts;

    private byte[] ReadFile(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                throw new ImageProcessingException($"Файл не найден: {path}");
            }

            if (file.Length == 0)
            {
                throw new ImageProcessingException("Файл пуст.");
            }

            if (file.Length > _options.MaxFileBytes)
            {
                throw new ImageProcessingException(
                    $"Файл слишком большой: {file.Length / 1048576.0:0.#} МБ, допустимо до {_options.MaxFileBytes / 1048576.0:0.#} МБ.");
            }

            return File.ReadAllBytes(path);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ImageProcessingException("Нет прав доступа к файлу.", ex);
        }
        catch (IOException ex)
        {
            throw new ImageProcessingException($"Не удалось прочитать файл: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            throw new ImageProcessingException("Некорректный путь к файлу.", ex);
        }
    }
}
