using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;

namespace DvPhotoValidator.Core.Validation;

/// <summary>
/// Internal quality criteria for the automatic visual checks. They are heuristics calibrated on sample
/// photos (docs/algorithms.md), not official requirements, so violations are Warnings, never Fails.
/// </summary>
public static class QualityThresholds
{
    public const double MinBackgroundLightness = 75;
    public const double MaxBackgroundChroma = 10;
    public const double MaxBackgroundZoneSpread = 10;
    public const double MaxBackgroundShadowFraction = 0.10;
    public const double MaxBackgroundTextureFraction = 0.03;
    public const double MaxNoiseSigma = 2.5;
    public const double MinEyeRegionSharpness = 40;
    public const double MaxHighlightFraction = 0.03;
    public const double MaxDarkFraction = 0.05;
    public const double MaxFaceSideDifference = 12;
    public const double MaxRollDegrees = 5;
    public const double MaxYawRatio = 0.12;
    public const double MinPitchRatio = 0.45;
    public const double MaxPitchRatio = 0.68;
    public const double OpenEyeMin = 0.09;
    public const double ClosedEyeMax = 0.05;
    public const double MinEyeContrast = 12;
    public const double GlassesRimScore = 0.08;
    public const double GlassesBridgeScore = 0.06;
}

/// <summary>Base for rules that read <see cref="ValidationContext.Quality"/>; all results are Visual.</summary>
public abstract class QualityRule : IValidationRule
{
    public abstract string Id { get; }

    public IEnumerable<ValidationIssue> Evaluate(ValidationContext context, RuleProfile profile) =>
        context.Crop is null || context.Quality is not { } quality
            ? []
            : Evaluate(quality).Select(i => i with { Category = ValidationCategory.Visual });

    protected abstract IEnumerable<ValidationIssue> Evaluate(QualityMeasurements quality);

    protected ValidationIssue Pass(string title, string description, double? value = null, string? unit = null) =>
        new(Id, ValidationStatus.Pass, title, description, value, unit);

    protected ValidationIssue Warn(string title, string description, double? value = null, string? unit = null) =>
        new(Id, ValidationStatus.Warning, title, description, value, unit);

    protected ValidationIssue Manual(string title, string description, double? value = null, string? unit = null) =>
        new(Id, ValidationStatus.ManualReview, title, description, value, unit);
}

public sealed class BackgroundRule : QualityRule
{
    public override string Id => "BACKGROUND";

    protected override IEnumerable<ValidationIssue> Evaluate(QualityMeasurements quality)
    {
        if (quality.Background is not { } bg)
        {
            yield return Manual("Фон", "Вокруг головы слишком мало фона для анализа. Убедитесь, что фон однородный белый или почти белый.");
            yield break;
        }

        var problems = new List<string>();
        if (bg.MedianLightness < QualityThresholds.MinBackgroundLightness)
        {
            problems.Add($"фон тёмный или серый (светлота {bg.MedianLightness:0} из 100, ориентир — не ниже {QualityThresholds.MinBackgroundLightness:0})");
        }

        if (bg.MedianChroma > QualityThresholds.MaxBackgroundChroma)
        {
            problems.Add($"у фона заметный цветной оттенок (насыщенность {bg.MedianChroma:0})");
        }

        if (bg.ZoneSpread > QualityThresholds.MaxBackgroundZoneSpread)
        {
            problems.Add($"фон освещён неравномерно: стороны различаются по светлоте на {bg.ZoneSpread:0}");
        }

        if (bg.ShadowFraction > QualityThresholds.MaxBackgroundShadowFraction)
        {
            problems.Add($"на фоне есть затемнённые участки или тени ({bg.ShadowFraction:P0} фона)");
        }

        if (bg.TextureFraction > QualityThresholds.MaxBackgroundTextureFraction)
        {
            problems.Add($"на фоне видны узоры, края предметов или текстура ({bg.TextureFraction:P0} фона)");
        }

        yield return problems.Count == 0
            ? Pass("Фон", $"Светлый, однородный и нейтральный (светлота {bg.MedianLightness:0} из 100). Анализировалась область вокруг головы выше подбородка.",
                Math.Round(bg.MedianLightness), "L*")
            : Warn("Фон", "Возможные проблемы: " + string.Join("; ", problems) + ". Требуется однородный белый или почти белый фон без теней.",
                Math.Round(bg.MedianLightness), "L*");
    }
}

public sealed class SharpnessRule : QualityRule
{
    public override string Id => "SHARPNESS";

    protected override IEnumerable<ValidationIssue> Evaluate(QualityMeasurements quality)
    {
        var s = quality.Sharpness.EyeRegionSharpness;
        yield return s >= QualityThresholds.MinEyeRegionSharpness
            ? Pass("Резкость", "Достаточно мелких деталей в области глаз.", Math.Round(s))
            : Warn("Резкость", "Низкая резкость: мало выраженных деталей в области глаз. Возможно, снимок не в фокусе, смазан или сильно увеличен.", Math.Round(s));
    }
}

public sealed class NoiseRule : QualityRule
{
    public override string Id => "NOISE";

    protected override IEnumerable<ValidationIssue> Evaluate(QualityMeasurements quality)
    {
        if (quality.Background is not { } bg)
        {
            yield break;
        }

        yield return bg.NoiseSigma <= QualityThresholds.MaxNoiseSigma
            ? Pass("Цифровой шум", "Шум на фоне незаметен.", Math.Round(bg.NoiseSigma, 1))
            : Warn("Цифровой шум", "На однотонном фоне заметен цифровой шум или зерно — обычно из-за слабого освещения. Снимите фото при более ярком свете.",
                Math.Round(bg.NoiseSigma, 1));
    }
}

public sealed class LightingRule : QualityRule
{
    public override string Id => "LIGHTING";

    protected override IEnumerable<ValidationIssue> Evaluate(QualityMeasurements quality)
    {
        var l = quality.Lighting;
        var problems = new List<string>();
        if (l.HighlightFraction > QualityThresholds.MaxHighlightFraction)
        {
            problems.Add($"пересвеченные участки на лице ({l.HighlightFraction:P0})");
        }

        if (l.DarkFraction > QualityThresholds.MaxDarkFraction)
        {
            problems.Add($"провалы в тени на лице ({l.DarkFraction:P0})");
        }

        if (l.SideDifference > QualityThresholds.MaxFaceSideDifference)
        {
            problems.Add($"одна половина лица заметно темнее другой (разница {l.SideDifference:0}) — свет падает сбоку");
        }

        yield return problems.Count == 0
            ? Pass("Освещение лица", "Без пересветов и провалов; освещение равномерное.", Math.Round(l.SideDifference, 1))
            : Warn("Освещение лица", "Возможные проблемы: " + string.Join("; ", problems) + ".", Math.Round(l.SideDifference, 1));
    }
}

public sealed class PoseRule : QualityRule
{
    public override string Id => "POSE";

    protected override IEnumerable<ValidationIssue> Evaluate(QualityMeasurements quality)
    {
        var p = quality.Pose;
        var problems = new List<string>();
        if (Math.Abs(p.RollDegrees) > QualityThresholds.MaxRollDegrees)
        {
            problems.Add($"голова наклонена к плечу на {Math.Abs(p.RollDegrees):0}°");
        }

        if (Math.Abs(p.YawRatio) > QualityThresholds.MaxYawRatio)
        {
            problems.Add("голова повёрнута в сторону");
        }

        if (double.IsNaN(p.PitchRatio))
        {
            yield return Manual("Положение головы", "Не удалось оценить наклон головы — проверьте, что лицо снято анфас.");
            yield break;
        }

        if (p.PitchRatio < QualityThresholds.MinPitchRatio)
        {
            problems.Add("голова, вероятно, запрокинута назад");
        }
        else if (p.PitchRatio > QualityThresholds.MaxPitchRatio)
        {
            problems.Add("голова, вероятно, опущена вниз");
        }

        yield return problems.Count == 0
            ? Pass("Положение головы", $"Анфас: наклон {Math.Abs(p.RollDegrees):0.#}°, поворот и наклон вперёд/назад в пределах нормы (эвристика).",
                Math.Round(Math.Abs(p.RollDegrees), 1), "°")
            : Warn("Положение головы", "Возможные проблемы: " + string.Join("; ", problems) + ". Требуется анфас, взгляд в камеру.",
                Math.Round(Math.Abs(p.RollDegrees), 1), "°");
    }
}

public sealed class EyesOpenRule : QualityRule
{
    public override string Id => "EYES_OPEN";

    protected override IEnumerable<ValidationIssue> Evaluate(QualityMeasurements quality)
    {
        var e = quality.Eyes;
        if (Math.Min(e.LeftContrast, e.RightContrast) < QualityThresholds.MinEyeContrast)
        {
            yield return Manual("Глаза открыты", "Область глаз слишком малоконтрастна для автоматической оценки. Убедитесь, что оба глаза открыты и хорошо видны.");
            yield break;
        }

        var min = Math.Min(e.LeftOpenness, e.RightOpenness);
        if (min >= QualityThresholds.OpenEyeMin)
        {
            yield return Pass("Глаза открыты", "В обоих глазах видна радужка.", Math.Round(min, 2));
        }
        else if (min <= QualityThresholds.ClosedEyeMax)
        {
            yield return Warn("Глаза открыты", "Похоже, один или оба глаза закрыты или прищурены. Оба глаза должны быть открыты.", Math.Round(min, 2));
        }
        else
        {
            yield return Manual("Глаза открыты", "Автоматическая оценка не уверена. Убедитесь, что оба глаза открыты и не закрыты волосами.", Math.Round(min, 2));
        }
    }
}

public sealed class GlassesRule : QualityRule
{
    public override string Id => "GLASSES";

    protected override IEnumerable<ValidationIssue> Evaluate(QualityMeasurements quality)
    {
        var g = quality.Glasses;
        // Reported only when signs of a frame are found; the heuristic cannot prove their absence.
        if (g.RimScore > QualityThresholds.GlassesRimScore || g.BridgeScore > QualityThresholds.GlassesBridgeScore)
        {
            yield return Warn("Очки", "Похоже, на фото есть очки (контуры оправы под глазами или на переносице). Очки на фото для DV не допускаются.");
        }
    }
}
