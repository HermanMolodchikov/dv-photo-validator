using DvPhotoValidator.Core.Imaging;
using DvPhotoValidator.Core.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DvPhotoValidator.Vision;

/// <summary>
/// YuNet face detector (OpenCV Zoo, MIT) run through ONNX Runtime on the CPU.
/// The image is scaled to fit 640×640 (aspect kept, zero padding), as the model has a fixed input.
/// </summary>
public sealed class FaceDetector : IDisposable
{
    public const string DefaultModelFile = "face_detection_yunet_2023mar.onnx";
    public const int InputSize = 640;

    private readonly InferenceSession _session;
    private readonly float _scoreThreshold;
    private readonly double _nmsThreshold;

    public FaceDetector(string modelPath, float scoreThreshold = 0.6f, double nmsThreshold = 0.3)
    {
        var options = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
        _session = new InferenceSession(modelPath, options);
        _scoreThreshold = scoreThreshold;
        _nmsThreshold = nmsThreshold;
    }

    /// <summary>Faces in source-image pixels, the largest first.</summary>
    public IReadOnlyList<DetectedFace> Detect(RgbImage image)
    {
        var scale = (double)InputSize / Math.Max(image.Width, image.Height);
        var w = Math.Clamp((int)Math.Round(image.Width * scale), 1, InputSize);
        var h = Math.Clamp((int)Math.Round(image.Height * scale), 1, InputSize);
        var resized = Resampler.Resample(image, 0, 0, image.Width, image.Height, w, h);

        // NCHW, BGR channel order, raw 0..255 values — the same as OpenCV's blobFromImage defaults.
        var tensor = new DenseTensor<float>([1, 3, InputSize, InputSize]);
        var buffer = tensor.Buffer.Span;
        var plane = InputSize * InputSize;
        var px = resized.Pixels;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var s = (y * w + x) * 3;
                var d = y * InputSize + x;
                buffer[d] = px[s];
                buffer[plane + d] = px[s + 1];
                buffer[2 * plane + d] = px[s + 2];
            }
        }

        var input = NamedOnnxValue.CreateFromTensor(_session.InputMetadata.Keys.First(), tensor);
        using var results = _session.Run([input]);
        var outputs = results.ToDictionary(r => r.Name, r => r.AsEnumerable<float>().ToArray());

        var actualScale = (double)w / image.Width;
        return YuNetDecoder.Suppress(YuNetDecoder.Decode(outputs, InputSize, _scoreThreshold), _nmsThreshold)
            .Select(f => YuNetDecoder.Scale(f, 1 / actualScale))
            .OrderByDescending(f => f.Area)
            .ToList();
    }

    public void Dispose() => _session.Dispose();
}
