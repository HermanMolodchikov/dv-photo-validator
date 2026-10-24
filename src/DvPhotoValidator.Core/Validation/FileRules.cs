using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;

namespace DvPhotoValidator.Core.Validation;

// Measurable technical requirements of the finished file. They are deterministic and reproducible.

public sealed class FileFormatRule : IValidationRule
{
    public string Id => "FILE_FORMAT";

    public IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile)
    {
        if (context.File is not { } file)
        {
            yield break;
        }

        yield return file.Format == ImageFileFormat.Jpeg
            ? new ValidationIssue(Id, ValidationStatus.Pass, "Формат JPEG", "Файл является изображением JPEG.")
            : new ValidationIssue(Id, ValidationStatus.Fail, "Формат файла",
                $"Требуется JPEG, фактический формат — {file.Format.ToString().ToUpperInvariant()}. Экспорт сохранит JPEG.");
    }
}

public sealed class FileSizeRule : IValidationRule
{
    public string Id => "FILE_SIZE";

    public IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile)
    {
        if (context.File is not { } file)
        {
            yield break;
        }

        var kib = Math.Round(file.FileBytes / 1024.0, 1);
        var limitKib = profile.MaxFileBytes / 1024.0;
        yield return file.FileBytes <= profile.MaxFileBytes
            ? new ValidationIssue(Id, ValidationStatus.Pass, "Размер файла",
                $"{file.FileBytes:N0} байт — в пределах лимита {profile.MaxFileBytes:N0} байт ({limitKib:0.#} КиБ).", kib, "КиБ")
            : new ValidationIssue(Id, ValidationStatus.Fail, "Размер файла",
                $"{file.FileBytes:N0} байт превышает лимит {profile.MaxFileBytes:N0} байт ({limitKib:0.#} КиБ).", kib, "КиБ");
    }
}

public sealed class SquareRule : IValidationRule
{
    public string Id => "DIMENSIONS_SQUARE";

    public IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile)
    {
        if (context.File is not { } file)
        {
            yield break;
        }

        yield return file.Width == file.Height
            ? new ValidationIssue(Id, ValidationStatus.Pass, "Соотношение сторон 1:1", "Изображение квадратное.")
            : new ValidationIssue(Id, ValidationStatus.Fail, "Соотношение сторон",
                $"Изображение {file.Width}×{file.Height} не квадратное; требуется 1:1.",
                Math.Round((double)file.Width / file.Height, 3));
    }
}

public sealed class DimensionsRule : IValidationRule
{
    public string Id => "DIMENSIONS_RANGE";

    public IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile)
    {
        if (context.File is not { } file)
        {
            yield break;
        }

        var side = Math.Min(file.Width, file.Height);
        var maxSide = Math.Max(file.Width, file.Height);
        var range = $"{profile.MinSize}–{profile.MaxSize} пикселей";
        if (side < profile.MinSize)
        {
            yield return new ValidationIssue(Id, ValidationStatus.Fail, "Разрешение",
                $"{file.Width}×{file.Height}: сторона меньше минимальной ({range}).", side, "px");
        }
        else if (maxSide > profile.MaxSize)
        {
            yield return new ValidationIssue(Id, ValidationStatus.Fail, "Разрешение",
                $"{file.Width}×{file.Height}: сторона больше максимальной ({range}).", maxSide, "px");
        }
        else
        {
            yield return new ValidationIssue(Id, ValidationStatus.Pass, "Разрешение",
                $"{file.Width}×{file.Height} — в допустимом диапазоне {range}.", side, "px");
        }
    }
}

public sealed class ColorModeRule : IValidationRule
{
    public string Id => "COLOR_MODE";

    public IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile)
    {
        if (context.File is not { } file)
        {
            yield break;
        }

        if (file.IsGrayscale)
        {
            yield return new ValidationIssue(Id, ValidationStatus.Fail, "Цветное изображение",
                "Изображение чёрно-белое или почти без цвета. Требуется цветная фотография.");
        }
        else if (file.BitsPerPixel < 24)
        {
            yield return new ValidationIssue(Id, ValidationStatus.Warning, "Глубина цвета",
                $"Глубина цвета {file.BitsPerPixel} бит на пиксель; ожидается 24 бита. Экспорт сохранит 24-битный JPEG.",
                file.BitsPerPixel, "бит");
        }
        else
        {
            yield return new ValidationIssue(Id, ValidationStatus.Pass, "Цветное изображение",
                $"Цветное изображение, {file.BitsPerPixel} бит на пиксель.");
        }
    }
}

public sealed class ColorSpaceRule : IValidationRule
{
    public string Id => "COLOR_SPACE";

    public IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile)
    {
        if (context.File is not { } file)
        {
            yield break;
        }

        yield return file.ColorSpace switch
        {
            ColorSpaceKind.Srgb => new ValidationIssue(Id, ValidationStatus.Pass, "Цветовое пространство sRGB",
                "Встроен профиль sRGB."),
            ColorSpaceKind.Untagged => new ValidationIssue(Id, ValidationStatus.Pass, "Цветовое пространство sRGB",
                "Цветовой профиль не встроен; по стандарту такие файлы интерпретируются как sRGB."),
            _ => new ValidationIssue(Id, ValidationStatus.Fail, "Цветовое пространство",
                "Встроен цветовой профиль, отличный от sRGB (например, Adobe RGB или CMYK). Экспорт преобразует цвета в sRGB."),
        };
    }
}

public sealed class PhotoAgeRule : IValidationRule
{
    public string Id => "PHOTO_AGE";

    public IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile)
    {
        if (context.Capture is not { } capture)
        {
            yield break;
        }

        var limit = $"не более {profile.MaxPhotoAgeMonths} мес.";
        // Without a capture date in the metadata there is nothing to measure, so nothing is reported.
        if (capture.TakenAt is not { } taken)
        {
            yield break;
        }

        if (taken > context.Now.AddDays(1))
        {
            yield return new ValidationIssue(Id, ValidationStatus.ManualReview, "Возраст фотографии",
                $"Дата съёмки в метаданных ({taken:d}) находится в будущем — вероятно, часы камеры настроены неверно.")
            { Category = ValidationCategory.Visual };
            yield break;
        }

        var months = (context.Now - taken).TotalDays / 30.44;
        // Metadata dates can be wrong, so an old date is a warning rather than a hard failure.
        yield return taken >= context.Now.AddMonths(-profile.MaxPhotoAgeMonths)
            ? new ValidationIssue(Id, ValidationStatus.Pass, "Возраст фотографии",
                $"По метаданным снимок сделан {taken:d} ({limit}).", Math.Round(months, 1), "мес.")
            { Category = ValidationCategory.Visual }
            : new ValidationIssue(Id, ValidationStatus.Warning, "Возраст фотографии",
                $"По метаданным снимок сделан {taken:d}; требуется фото {limit}. Если дата неверна, проверьте вручную.",
                Math.Round(months, 1), "мес.")
            { Category = ValidationCategory.Visual };
    }
}
