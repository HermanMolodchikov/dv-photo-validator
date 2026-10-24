using System.Windows.Media;
using System.Windows.Media.Imaging;
using DvPhotoValidator.Core.Geometry;
using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;
using DvPhotoValidator.Core.Validation;

namespace DvPhotoValidator.App.ViewModels;

public enum GuideKind
{
    HeadTop,
    Eyes,
    Chin,
}

/// <summary>
/// State of the crop editor. The square frame is fixed on screen and the photo moves and scales behind it.
/// Landmarks are stored in source pixels, so they stay attached to the face when the crop changes.
/// </summary>
public sealed class CropEditorViewModel : ObservableObject
{
    public const double CanvasSize = 560;
    public const double FrameOffset = 40;
    public const double FrameSize = 480;

    // Initial hint positions of unset guides, as a fraction of the frame height from the top.
    private const double DefaultTop = 0.14;
    private const double DefaultEyes = 0.42;
    private const double DefaultChin = 0.76;

    private static readonly DoubleCollection Dashed = Freeze([5, 3]);

    private BitmapSource? _image;
    private int _width;
    private int _height;
    private CropRect _crop;
    private double? _top;
    private double? _eyes;
    private double? _chin;
    private RuleProfile _profile;

    public CropEditorViewModel(RuleProfile profile)
    {
        _profile = profile;
    }

    /// <summary>Raised after any change of the crop or the landmarks.</summary>
    public event EventHandler? GeometryChanged;

    public BitmapSource? Image => _image;

    public bool HasImage => _image is not null;

    public CropRect Crop => _crop;

    public int SourceWidth => _width;

    public int SourceHeight => _height;

    public HeadLandmarks Landmarks => new(_top, _chin, _eyes);

    public double Scale => _crop.Size > 0 ? FrameSize / _crop.Size : 1;

    public double ImageLeft => FrameOffset - _crop.X * Scale;

    public double ImageTop => FrameOffset - _crop.Y * Scale;

    public double ImageWidth => _width * Scale;

    public double ImageHeight => _height * Scale;

    public double TopGuideY => ToDisplay(_top ?? _crop.Y + DefaultTop * _crop.Size);

    public double EyesGuideY => ToDisplay(_eyes ?? _crop.Y + DefaultEyes * _crop.Size);

    public double ChinGuideY => ToDisplay(_chin ?? _crop.Y + DefaultChin * _crop.Size);

    public DoubleCollection? TopDash => _top is null ? Dashed : null;

    public DoubleCollection? EyesDash => _eyes is null ? Dashed : null;

    public DoubleCollection? ChinDash => _chin is null ? Dashed : null;

    /// <summary>Band where the eye line must be: EyeMin..EyeMax of the frame height, measured from the bottom.</summary>
    public double EyeZoneTop => FrameOffset + (1 - _profile.EyeMaxRatio) * FrameSize;

    public double EyeZoneHeight => (_profile.EyeMaxRatio - _profile.EyeMinRatio) * FrameSize;

    /// <summary>Band where the chin must be, given the current head-top guide.</summary>
    public double ChinZoneTop => TopGuideY + _profile.HeadMinRatio * FrameSize;

    public double ChinZoneHeight => (_profile.HeadMaxRatio - _profile.HeadMinRatio) * FrameSize;

    public string HeadText
    {
        get
        {
            var m = HeadGeometryValidator.Measure(_crop, Landmarks);
            var range = $"норма {_profile.HeadMinRatio * 100:0}–{_profile.HeadMaxRatio * 100:0}%";
            return m.HeadHeightPercent is { } h ? $"Высота головы: {h:0.#}% ({range})" : $"Высота головы: не отмечена ({range})";
        }
    }

    public string EyesText
    {
        get
        {
            var m = HeadGeometryValidator.Measure(_crop, Landmarks);
            var range = $"норма {_profile.EyeMinRatio * 100:0}–{_profile.EyeMaxRatio * 100:0}%";
            return m.EyeHeightPercent is { } e ? $"Линия глаз: {e:0.#}% от низа ({range})" : $"Линия глаз: не отмечена ({range})";
        }
    }

    public string CropText => HasImage
        ? $"Рамка: {_crop.Size:0}×{_crop.Size:0} px исходника, X={_crop.X:0}, Y={_crop.Y:0}"
        : "";

    public void SetImage(BitmapSource displayImage, int sourceWidth, int sourceHeight)
    {
        _image = displayImage;
        _width = sourceWidth;
        _height = sourceHeight;
        _crop = CropGeometry.Default(sourceWidth, sourceHeight);
        _top = _eyes = _chin = null;
        Changed();
    }

    public void SetProfile(RuleProfile profile)
    {
        _profile = profile;
        Changed();
    }

    public void ResetCrop()
    {
        if (!HasImage)
        {
            return;
        }

        _crop = CropGeometry.Default(_width, _height);
        Changed();
    }

    public void ResetLandmarks()
    {
        _top = _eyes = _chin = null;
        Changed();
    }

    /// <summary>Moves the photo by a screen-space delta (the frame stays in place).</summary>
    public void Pan(double dxDisplay, double dyDisplay)
    {
        if (!HasImage)
        {
            return;
        }

        _crop = CropGeometry.Pan(_crop, -dxDisplay / Scale, -dyDisplay / Scale, _width, _height);
        Changed();
    }

    /// <summary>factor &gt; 1 enlarges the photo (smaller crop) around a canvas point.</summary>
    public void Zoom(double factor, double displayX, double displayY)
    {
        if (!HasImage)
        {
            return;
        }

        var anchorX = _crop.X + (displayX - FrameOffset) / Scale;
        var anchorY = FromDisplay(displayY);
        _crop = CropGeometry.Zoom(_crop, factor, anchorX, anchorY, _width, _height);
        Changed();
    }

    public void ZoomAtCenter(double factor) =>
        Zoom(factor, FrameOffset + FrameSize / 2, FrameOffset + FrameSize / 2);

    public void MoveGuide(GuideKind kind, double displayY)
    {
        if (!HasImage)
        {
            return;
        }

        var y = Math.Clamp(FromDisplay(displayY), 0, _height);
        switch (kind)
        {
            case GuideKind.HeadTop:
                _top = y;
                break;
            case GuideKind.Eyes:
                _eyes = y;
                break;
            case GuideKind.Chin:
                _chin = y;
                break;
        }

        Changed();
    }

    private double ToDisplay(double sourceY) => FrameOffset + (sourceY - _crop.Y) * Scale;

    private double FromDisplay(double displayY) => _crop.Y + (displayY - FrameOffset) / Scale;

    private void Changed()
    {
        OnPropertyChanged(string.Empty);
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private static DoubleCollection Freeze(double[] values)
    {
        var collection = new DoubleCollection(values);
        collection.Freeze();
        return collection;
    }
}
