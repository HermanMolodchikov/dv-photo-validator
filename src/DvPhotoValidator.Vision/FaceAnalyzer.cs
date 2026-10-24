using DvPhotoValidator.Core.Interfaces;
using DvPhotoValidator.Core.Models;

namespace DvPhotoValidator.Vision;

/// <summary>Face detection + landmark estimation. Runs locally; images never leave the computer.</summary>
public sealed class FaceAnalyzer : IFaceAnalyzer, IDisposable
{
    private readonly FaceDetector _detector;

    public FaceAnalyzer(string modelPath)
    {
        _detector = new FaceDetector(modelPath);
    }

    public static string DefaultModelPath => Path.Combine(AppContext.BaseDirectory, "Models", FaceDetector.DefaultModelFile);

    /// <summary>Creates the analyzer from the bundled model, or returns null with a reason the user can read.</summary>
    public static FaceAnalyzer? TryCreateDefault(out string? error)
    {
        error = null;
        if (!File.Exists(DefaultModelPath))
        {
            error = $"Модель распознавания лица не найдена ({FaceDetector.DefaultModelFile}). Доступна только ручная разметка.";
            return null;
        }

        try
        {
            return new FaceAnalyzer(DefaultModelPath);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            error = $"Не удалось загрузить модель распознавания лица ({ex.GetType().Name}). Доступна только ручная разметка.";
            return null;
        }
    }

    public FaceAnalysis Analyze(RgbImage image)
    {
        var faces = _detector.Detect(image);
        if (faces.Count == 0)
        {
            return new FaceAnalysis(faces, null);
        }

        var (landmarks, notes) = LandmarkDetector.Estimate(image, faces[0]);
        return new FaceAnalysis(faces, landmarks) { Notes = notes };
    }

    public void Dispose() => _detector.Dispose();
}
