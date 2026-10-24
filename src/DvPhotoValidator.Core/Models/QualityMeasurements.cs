namespace DvPhotoValidator.Core.Models;

// Raw measurements of the final 600×600 frame. Validation rules turn them into statuses;
// all thresholds are internal quality criteria (see QualityThresholds), not official norms.

/// <param name="SampleFraction">Share of the frame classified as background.</param>
/// <param name="MedianLightness">Median L* (0 black … 100 white).</param>
/// <param name="MedianChroma">Median C* = √(a*² + b*²); 0 is neutral grey/white.</param>
/// <param name="ZoneSpread">Difference of median L* between left, right and top background zones.</param>
/// <param name="ShadowFraction">Share of background pixels noticeably darker than the median.</param>
/// <param name="TextureFraction">Share of background pixels on edges (patterns, objects).</param>
/// <param name="NoiseSigma">Robust estimate of pixel noise on the background, in luma units.</param>
public sealed record BackgroundMeasurement(
    double SampleFraction,
    double MedianLightness,
    double MedianChroma,
    double ZoneSpread,
    double ShadowFraction,
    double TextureFraction,
    double NoiseSigma);

/// <param name="EyeRegionSharpness">Variance of the Laplacian in the eye region.</param>
public sealed record SharpnessMeasurement(double EyeRegionSharpness);

/// <param name="HighlightFraction">Share of face pixels that are clipped white.</param>
/// <param name="DarkFraction">Share of face pixels that are nearly black.</param>
/// <param name="SideDifference">Difference of median L* between the left and right halves of the face.</param>
public sealed record LightingMeasurement(double HighlightFraction, double DarkFraction, double SideDifference);

/// <param name="RollDegrees">Tilt of the eye line towards a shoulder.</param>
/// <param name="YawRatio">Horizontal nose offset from the eye midpoint, in eye distances (turn left/right).</param>
/// <param name="PitchRatio">(nose − eyes) / (mouth − eyes) vertically (head up/down); ≈ 0.56 when facing the camera.</param>
public sealed record PoseMeasurement(double RollDegrees, double YawRatio, double PitchRatio);

/// <param name="Openness">Visible height of the dark iris region, in eye distances, per eye (image left, image right).</param>
/// <param name="Contrast">Luma standard deviation of each eye patch; low values make the openness unreliable.</param>
public sealed record EyesMeasurement(double LeftOpenness, double RightOpenness, double LeftContrast, double RightContrast);

/// <param name="RimScore">Edge density under the eyes minus edge density on the cheeks (frame rims).</param>
/// <param name="BridgeScore">Edge density on the nose bridge between the eyes.</param>
public sealed record GlassesMeasurement(double RimScore, double BridgeScore);

public sealed record QualityMeasurements(
    BackgroundMeasurement? Background,
    SharpnessMeasurement Sharpness,
    LightingMeasurement Lighting,
    PoseMeasurement Pose,
    EyesMeasurement Eyes,
    GlassesMeasurement Glasses);
