using System.Windows.Media;
using System.Windows.Media.Imaging;
using DvPhotoValidator.Core.Interfaces;
using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;
using DvPhotoValidator.Core.Validation;

namespace DvPhotoValidator.Imaging;

/// <summary>
/// Saves the final JPEG. Quality is chosen deterministically by binary search as the highest value
/// whose output fits the size limit. The file is written to a temporary path, re-opened and verified,
/// and only then moved to the destination — success is never reported before that.
/// </summary>
public sealed class JpegExporter : IJpegExporter
{
    private readonly IImageInspector _inspector;
    private readonly ValidationEngine _engine;

    public JpegExporter(IImageInspector? inspector = null, ValidationEngine? engine = null)
    {
        _inspector = inspector ?? new ImageLoader();
        _engine = engine ?? ValidationEngine.CreateDefault();
    }

    public static byte[] Encode(RgbImage image, int quality)
    {
        var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgr24, null, image.Pixels, image.Stride);
        var encoder = new JpegBitmapEncoder { QualityLevel = Math.Clamp(quality, 1, 100) };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    public JpegEncodeResult EncodeToFit(RgbImage image, RuleProfile profile)
    {
        var cache = new Dictionary<int, byte[]>();
        byte[] At(int q)
        {
            if (!cache.TryGetValue(q, out var data))
            {
                data = Encode(image, q);
                cache[q] = data;
            }

            return data;
        }

        var top = Math.Clamp(profile.MaxJpegQuality, 1, 100);
        if (At(top).LongLength <= profile.MaxFileBytes)
        {
            return new JpegEncodeResult(At(top), top);
        }

        int lo = 1, hi = top - 1, best = 0;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (At(mid).LongLength <= profile.MaxFileBytes)
            {
                best = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        if (best == 0)
        {
            throw new ImageProcessingException(
                $"Не удалось уложиться в лимит {profile.MaxFileBytes:N0} байт даже при минимальном качестве JPEG.");
        }

        return new JpegEncodeResult(At(best), best);
    }

    public ExportResult Export(RgbImage image, string path, RuleProfile profile)
    {
        if (image.Width != profile.ExportSize || image.Height != profile.ExportSize)
        {
            throw new ArgumentException($"Expected {profile.ExportSize}×{profile.ExportSize} image.", nameof(image));
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new ImageProcessingException("Некорректный путь сохранения.");
        var encoded = EncodeToFit(image, profile);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                file.Write(encoded.Data);
                file.Flush(flushToDisk: true);
            }

            var facts = _inspector.Inspect(tempPath);
            var report = _engine.Run(ValidationContext.ForFile(facts), profile);
            if (facts.FileBytes != encoded.Data.LongLength)
            {
                report = report.Concat([new ValidationIssue("EXPORT_WRITE", ValidationStatus.Fail, "Запись файла",
                    $"Записано {facts.FileBytes:N0} байт вместо {encoded.Data.LongLength:N0}.")]);
            }

            if (report.HasFailures)
            {
                throw new ExportVerificationException("Сохранённый файл не прошёл повторную проверку; экспорт отменён.", report);
            }

            File.Move(tempPath, fullPath, overwrite: true);

            if (encoded.Quality < profile.MinAcceptableJpegQuality)
            {
                report = report.Concat([new ValidationIssue("EXPORT_QUALITY", ValidationStatus.Warning, "Качество сжатия",
                    $"Чтобы уложиться в лимит размера, качество JPEG снижено до {encoded.Quality}. Возможны заметные артефакты сжатия.",
                    encoded.Quality)]);
            }
            else
            {
                report = report.Concat([new ValidationIssue("EXPORT_QUALITY", ValidationStatus.Pass, "Качество сжатия",
                    $"Качество JPEG {encoded.Quality} — максимальное, при котором файл укладывается в лимит.", encoded.Quality)]);
            }

            return new ExportResult(fullPath, encoded.Quality, facts.FileBytes, facts, report);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ImageProcessingException("Нет прав на запись в выбранную папку.", ex);
        }
        catch (IOException ex) when (ex is not FileNotFoundException)
        {
            throw new ImageProcessingException($"Не удалось сохранить файл: {ex.Message}", ex);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover hidden temp file is harmless and will not be reported as the result.
        }
    }
}
