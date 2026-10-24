using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DvPhotoValidator.App.Commands;
using DvPhotoValidator.App.Services;
using DvPhotoValidator.Core.Geometry;
using DvPhotoValidator.Core.Imaging;
using DvPhotoValidator.Core.Interfaces;
using DvPhotoValidator.Core.Models;
using DvPhotoValidator.Core.Rules;
using DvPhotoValidator.Core.Validation;
using DvPhotoValidator.Imaging;

namespace DvPhotoValidator.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const int MaxDisplaySide = 3000;

    private readonly IImageLoader _loader;
    private readonly ICropService _cropService;
    private readonly IJpegExporter _exporter;
    private readonly ValidationEngine _engine;
    private readonly IDialogService _dialogs;
    private readonly IFaceAnalyzer? _faceAnalyzer;
    private readonly IQualityAnalyzer? _qualityAnalyzer;
    private readonly HashSet<string> _confirmed = [];
    private QualityMeasurements? _quality;
    private int _qualityVersion;

    private LoadedImage? _loaded;
    private FaceAnalysis? _face;
    private RuleProfile _profile;
    private ValidationReport _sourceReport = ValidationReport.Empty;
    private ValidationReport _frameReport = ValidationReport.Empty;
    private ValidationReport? _exportReport;
    private bool _isBusy;
    private string _statusText = "Откройте фотографию или перетащите её в окно.";
    private string _sourceInfo = "";
    private BitmapSource? _previewImage;
    private string _previewInfo = "Нажмите «Предпросмотр», чтобы увидеть итоговое изображение 600×600.";
    private bool _previewStale;
    private int _selectedTab;
    private double _uiScale = 1.0;

    public MainViewModel(
        IImageLoader loader,
        ICropService cropService,
        IJpegExporter exporter,
        ValidationEngine engine,
        IDialogService dialogs,
        RuleProfileLoadResult profiles,
        IFaceAnalyzer? faceAnalyzer = null,
        string? faceAnalyzerError = null,
        IQualityAnalyzer? qualityAnalyzer = null)
    {
        _faceAnalyzer = faceAnalyzer;
        _qualityAnalyzer = qualityAnalyzer;
        _loader = loader;
        _cropService = cropService;
        _exporter = exporter;
        _engine = engine;
        _dialogs = dialogs;

        Profiles = new ObservableCollection<RuleProfile>(profiles.Profiles);
        _profile = Profiles[0];
        Editor = new CropEditorViewModel(_profile);
        Editor.GeometryChanged += (_, _) => OnGeometryChanged();

        SourceGroup = new IssueGroupViewModel(
            "Исходный файл",
            "Технические требования к открытому файлу как есть. Экспорт сам приведёт формат, размер и цвет к нужным.",
            "Фотография не открыта.");
        FrameGroup = new IssueGroupViewModel(
            "Кадр и визуальные оценки",
            "Пересчитывается при каждом изменении рамки и ориентиров.",
            "Фотография не открыта.");
        ExportGroup = new IssueGroupViewModel(
            "Сохранённый файл",
            "Повторная проверка файла после записи на диск.",
            "Файл ещё не сохранён.");

        OpenCommand = new AsyncRelayCommand(OpenAsync, () => !IsBusy);
        ResetCropCommand = new RelayCommand(Editor.ResetCrop, () => HasImage);
        ResetLandmarksCommand = new RelayCommand(Editor.ResetLandmarks, () => HasImage);
        CheckFrameCommand = new RelayCommand(CheckFrame, () => HasImage);
        AutoAlignCommand = new AsyncRelayCommand(AutoAlignAsync, () => HasImage && !IsBusy && _faceAnalyzer is not null);
        PreviewCommand = new AsyncRelayCommand(PreviewAsync, () => HasImage && !IsBusy);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => HasImage && !IsBusy);
        CopyReportCommand = new RelayCommand(CopyReport, () => HasImage);
        ToggleConfirmCommand = new RelayCommand<string>(ToggleConfirm);

        if (profiles.Errors.Count > 0)
        {
            StatusText = "Не все файлы правил загружены: " + string.Join("; ", profiles.Errors);
        }
        else if (faceAnalyzerError is not null)
        {
            StatusText = faceAnalyzerError;
        }
    }

    public CropEditorViewModel Editor { get; }

    public ObservableCollection<RuleProfile> Profiles { get; }

    public RuleProfile SelectedProfile
    {
        get => _profile;
        set
        {
            if (value is null || !SetProperty(ref _profile, value))
            {
                return;
            }

            Editor.SetProfile(value);
            if (_loaded is not null)
            {
                _sourceReport = BuildSourceReport(_loaded);
                SourceGroup.Update(_sourceReport.Issues);
            }

            ExportGroup.Clear("Правила изменены — сохраните файл заново.");
            _exportReport = null;
        }
    }

    public IssueGroupViewModel SourceGroup { get; }

    public IssueGroupViewModel FrameGroup { get; }

    public IssueGroupViewModel ExportGroup { get; }

    public ObservableCollection<HistoryEntry> History { get; } = [];

    public IReadOnlyList<double> UiScales { get; } = [1.0, 1.25, 1.5];

    public double UiScale
    {
        get => _uiScale;
        set => SetProperty(ref _uiScale, value);
    }

    public bool HasImage => _loaded is not null;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string SourceInfo
    {
        get => _sourceInfo;
        private set => SetProperty(ref _sourceInfo, value);
    }

    public BitmapSource? PreviewImage
    {
        get => _previewImage;
        private set => SetProperty(ref _previewImage, value);
    }

    public string PreviewInfo
    {
        get => _previewInfo;
        private set => SetProperty(ref _previewInfo, value);
    }

    public bool PreviewStale
    {
        get => _previewStale;
        private set => SetProperty(ref _previewStale, value);
    }

    public int SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, value);
    }

    public ICommand OpenCommand { get; }

    public ICommand ResetCropCommand { get; }

    public ICommand ResetLandmarksCommand { get; }

    public ICommand CheckFrameCommand { get; }

    public ICommand AutoAlignCommand { get; }

    public bool CanAutoAlign => _faceAnalyzer is not null;

    public ICommand PreviewCommand { get; }

    public ICommand ExportCommand { get; }

    public ICommand CopyReportCommand { get; }

    /// <summary>Marks a "check it yourself" item as confirmed by the user (or removes the mark).</summary>
    public ICommand ToggleConfirmCommand { get; }

    public async Task LoadAsync(string path)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "Открытие и проверка файла…";
        try
        {
            var loaded = await Task.Run(() => _loader.Load(path));
            var display = await Task.Run(() => CreateDisplayBitmap(loaded.Image));

            _loaded = loaded;
            _face = null;
            _quality = null;
            _qualityVersion++;
            _confirmed.Clear();
            PreviewImage = null;
            PreviewStale = false;
            PreviewInfo = "Нажмите «Предпросмотр», чтобы увидеть итоговое изображение 600×600.";
            _exportReport = null;
            ExportGroup.Clear("Файл ещё не сохранён.");
            _sourceReport = BuildSourceReport(loaded);
            SourceGroup.Update(_sourceReport.Issues);
            SourceInfo = DescribeSource(loaded);
            OnPropertyChanged(nameof(HasImage));
            Editor.SetImage(display, loaded.Image.Width, loaded.Image.Height);
            SelectedTab = 0;

            var name = Path.GetFileName(loaded.FilePath);
            AddHistory("Открыт файл", name);
            StatusText = $"Открыто: {name}. Перетащите линии к верху головы, глазам и подбородку.";
            if (_faceAnalyzer is not null)
            {
                await AnalyzeAndAlignAsync();
            }
        }
        catch (ImageProcessingException ex)
        {
            StatusText = "Не удалось открыть файл: " + ex.Message;
            AddHistory("Ошибка открытия", ex.Message);
            _dialogs.ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AutoAlignAsync()
    {
        IsBusy = true;
        try
        {
            await AnalyzeAndAlignAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Detects the face, estimates landmarks and proposes a crop. Never invents a frame when no face is found.
    /// Re-running discards manual corrections of the lines, which is what "align automatically" means.
    /// </summary>
    private async Task AnalyzeAndAlignAsync()
    {
        if (_loaded is null || _faceAnalyzer is null)
        {
            return;
        }

        StatusText = "Поиск лица и расчёт кадра…";
        var loaded = _loaded;
        FaceAnalysis analysis;
        try
        {
            analysis = await Task.Run(() => _faceAnalyzer.Analyze(loaded.Image));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            StatusText = $"Автоматический анализ лица не выполнен ({ex.GetType().Name}). Отметьте ориентиры вручную.";
            return;
        }

        if (!ReferenceEquals(loaded, _loaded))
        {
            return; // Another photo was opened meanwhile.
        }

        _face = analysis;
        if (analysis.Primary is not { } face || analysis.Landmarks is not { } landmarks)
        {
            Editor.ApplyAutomatic(null, null, null);
            StatusText = "Лицо не найдено автоматически. Отметьте ориентиры вручную или выберите другое фото.";
            AddHistory("Лицо не найдено", Path.GetFileName(loaded.FilePath));
            return;
        }

        var proposal = AutoCrop.Compute(landmarks, face.EyeCenter.X, loaded.Image.Width, loaded.Image.Height, _profile);
        Editor.ApplyAutomatic(face, landmarks, proposal?.Crop);

        var messages = new List<string> { $"Кадр выровнен автоматически (уверенность {landmarks.Confidence:P0})." };
        if (analysis.Faces.Count > 1)
        {
            messages.Add($"Найдено лиц: {analysis.Faces.Count}.");
        }

        if (proposal?.WasConstrained == true)
        {
            messages.Add("Лицо близко к краю фото — идеальное положение рамки недостижимо.");
        }

        messages.AddRange(analysis.Notes);
        messages.Add("Проверьте линии и при необходимости поправьте их.");
        StatusText = string.Join(" ", messages);
        AddHistory("Автовыравнивание", $"уверенность {landmarks.Confidence:P0}");
    }

    private async Task OpenAsync()
    {
        if (_dialogs.PickImageFile() is { } path)
        {
            await LoadAsync(path);
        }
    }

    private void OnGeometryChanged()
    {
        ValidateFrame();
        ScheduleQualityAnalysis();
        if (PreviewImage is not null)
        {
            PreviewStale = true;
        }
    }

    private void ValidateFrame()
    {
        if (_loaded is null)
        {
            FrameGroup.Clear();
            return;
        }

        var context = new ValidationContext
        {
            Crop = Editor.Crop,
            SourceWidth = Editor.SourceWidth,
            SourceHeight = Editor.SourceHeight,
            Landmarks = Editor.Landmarks,
            Capture = new CaptureDateInfo(_loaded.SourceFacts.DateTaken),
            Face = _face,
            Quality = _quality,
            ConfirmedByUser = _confirmed,
        };
        _frameReport = _engine.Run(context, _profile);
        FrameGroup.Update(_frameReport.Issues);
    }

    private void ToggleConfirm(string ruleId)
    {
        if (!_confirmed.Remove(ruleId))
        {
            _confirmed.Add(ruleId);
        }

        ValidateFrame();
    }

    /// <summary>
    /// Quality is measured on the rendered frame, which takes a few hundred milliseconds on large photos,
    /// so it runs in the background after the crop has stopped changing for a moment.
    /// </summary>
    private async void ScheduleQualityAnalysis()
    {
        var version = ++_qualityVersion;
        if (_qualityAnalyzer is null || _loaded is null || _face?.Primary is not { } face)
        {
            if (_quality is not null)
            {
                _quality = null;
                ValidateFrame();
            }

            return;
        }

        await Task.Delay(300);
        if (version != _qualityVersion)
        {
            return;
        }

        var loaded = _loaded;
        var crop = Editor.Crop;
        var landmarks = Editor.Landmarks;
        var size = _profile.ExportSize;
        QualityMeasurements? quality;
        try
        {
            quality = await Task.Run(() => _qualityAnalyzer.Analyze(loaded.Image, crop, face, landmarks, size));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            quality = null;
        }

        if (version != _qualityVersion || !ReferenceEquals(loaded, _loaded))
        {
            return;
        }

        _quality = quality;
        ValidateFrame();
    }

    private void CheckFrame()
    {
        ValidateFrame();
        SelectedTab = 0;
        var failures = _frameReport.Issues.Count(i => i.Status == ValidationStatus.Fail);
        StatusText = failures == 0
            ? "Кадр проверен: несоответствий по измеримым параметрам нет. Пункты «Проверьте вручную» требуют вашего внимания."
            : $"Кадр проверен: несоответствий — {failures}. Подробности на вкладке «Проверка».";
    }

    private async Task PreviewAsync()
    {
        if (_loaded is null)
        {
            return;
        }

        IsBusy = true;
        StatusText = "Подготовка предпросмотра…";
        try
        {
            var crop = Editor.Crop;
            var profile = _profile;
            var source = _loaded.Image;
            var (bitmap, encoded) = await Task.Run(() =>
            {
                var rendered = _cropService.Render(source, crop, profile.ExportSize);
                var fit = _exporter.EncodeToFit(rendered, profile);
                return (CreateBitmap(rendered), fit);
            });
            PreviewImage = bitmap;
            PreviewStale = false;
            PreviewInfo = $"{profile.ExportSize}×{profile.ExportSize} px, JPEG, sRGB · ≈{encoded.Data.Length / 1024.0:0.#} КиБ при качестве {encoded.Quality} (лимит {profile.MaxFileBytes / 1024.0:0.#} КиБ)";
            SelectedTab = 1;
            StatusText = "Предпросмотр готов.";
        }
        catch (ImageProcessingException ex)
        {
            PreviewInfo = ex.Message;
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportAsync()
    {
        if (_loaded is null)
        {
            return;
        }

        var initialDirectory = Path.GetDirectoryName(_loaded.FilePath);
        if (_dialogs.PickSavePath(initialDirectory, "dv_photo.jpg") is not { } path)
        {
            return;
        }

        if (string.Equals(Path.GetFullPath(path), _loaded.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            _dialogs.ShowError("Нельзя перезаписать исходную фотографию. Выберите другое имя файла.");
            return;
        }

        IsBusy = true;
        StatusText = "Сохранение и повторная проверка файла…";
        try
        {
            var crop = Editor.Crop;
            var profile = _profile;
            var source = _loaded.Image;
            var result = await Task.Run(() =>
            {
                var rendered = _cropService.Render(source, crop, profile.ExportSize);
                return _exporter.Export(rendered, path, profile);
            });

            _exportReport = result.Report;
            ExportGroup.Update(result.Report.Issues);
            SelectedTab = 0;
            var name = Path.GetFileName(result.FilePath);
            StatusText = $"Сохранено и проверено: {result.FilePath} — {result.FileBytes:N0} байт, качество JPEG {result.Quality}.";
            AddHistory("Сохранён файл", $"{name}: {result.FileBytes / 1024.0:0.#} КиБ, качество {result.Quality}");
        }
        catch (ExportVerificationException ex)
        {
            _exportReport = ex.Report;
            ExportGroup.Update(ex.Report.Issues);
            StatusText = ex.Message;
            AddHistory("Экспорт отменён", ex.Message);
            _dialogs.ShowError(ex.Message);
        }
        catch (ImageProcessingException ex)
        {
            StatusText = "Файл не сохранён: " + ex.Message;
            AddHistory("Ошибка сохранения", ex.Message);
            _dialogs.ShowError("Файл не сохранён: " + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CopyReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"DV Photo Validator — отчёт проверки. Правила: {_profile.DisplayName}");
        sb.AppendLine();
        sb.AppendLine(_sourceReport.ToText("== Исходный файл =="));
        sb.AppendLine(_frameReport.ToText("== Кадр =="));
        if (_exportReport is not null)
        {
            sb.AppendLine(_exportReport.ToText("== Сохранённый файл =="));
        }

        _dialogs.CopyToClipboard(sb.ToString());
        StatusText = "Отчёт скопирован в буфер обмена.";
    }

    private ValidationReport BuildSourceReport(LoadedImage loaded)
    {
        var report = _engine.Run(ValidationContext.ForFile(loaded.SourceFacts), _profile);
        var notes = new List<ValidationIssue>();
        if (loaded.SourceFacts.Orientation != 1)
        {
            notes.Add(new ValidationIssue("EXIF_ORIENTATION", ValidationStatus.Pass, "Ориентация",
                $"Применён тег EXIF Orientation = {loaded.SourceFacts.Orientation}; фото повёрнуто правильно."));
        }

        if (loaded.WasColorConverted)
        {
            notes.Add(new ValidationIssue("COLOR_CONVERTED", ValidationStatus.Pass, "Преобразование цвета",
                "Цвета преобразованы из встроенного профиля в sRGB."));
        }

        if (loaded.HadTransparency)
        {
            notes.Add(new ValidationIssue("TRANSPARENCY", ValidationStatus.Warning, "Прозрачность",
                "В файле есть прозрачные области; они заменены белым цветом. Убедитесь, что фон фотографии настоящий, а не вырезанный."));
        }

        return report.Concat(notes);
    }

    private static string DescribeSource(LoadedImage loaded)
    {
        var f = loaded.SourceFacts;
        var date = f.DateTaken is { } d ? $" · снято {d:d}" : "";
        return $"{Path.GetFileName(loaded.FilePath)} · {f.Format.ToString().ToUpperInvariant()} · {f.Width}×{f.Height} · {f.FileBytes / 1024.0:N0} КиБ{date}";
    }

    private void AddHistory(string action, string details) =>
        History.Insert(0, new HistoryEntry(DateTime.Now, action, details));

    /// <summary>Very large photos are shown through a reduced copy; all measurements still use full resolution.</summary>
    private static BitmapSource CreateDisplayBitmap(RgbImage image)
    {
        var longest = Math.Max(image.Width, image.Height);
        if (longest <= MaxDisplaySide)
        {
            return CreateBitmap(image);
        }

        var k = (double)MaxDisplaySide / longest;
        var reduced = Resampler.Resample(image, 0, 0, image.Width, image.Height,
            Math.Max(1, (int)Math.Round(image.Width * k)), Math.Max(1, (int)Math.Round(image.Height * k)));
        return CreateBitmap(reduced);
    }

    private static BitmapSource CreateBitmap(RgbImage image)
    {
        var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgr24, null, image.Pixels, image.Stride);
        bitmap.Freeze();
        return bitmap;
    }
}
