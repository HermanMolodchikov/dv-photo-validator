using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DvPhotoValidator.App.Commands;
using DvPhotoValidator.App.Services;
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

    private LoadedImage? _loaded;
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
        RuleProfileLoadResult profiles)
    {
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
        PreviewCommand = new AsyncRelayCommand(PreviewAsync, () => HasImage && !IsBusy);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => HasImage && !IsBusy);
        CopyReportCommand = new RelayCommand(CopyReport, () => HasImage);

        if (profiles.Errors.Count > 0)
        {
            StatusText = "Не все файлы правил загружены: " + string.Join("; ", profiles.Errors);
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

    public ICommand PreviewCommand { get; }

    public ICommand ExportCommand { get; }

    public ICommand CopyReportCommand { get; }

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
            StatusText = $"Открыто: {name}. Перетащите линии к верху головы, глазам и подбородку.";
            AddHistory("Открыт файл", name);
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
        };
        _frameReport = _engine.Run(context, _profile);
        FrameGroup.Update(_frameReport.Issues);
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
        var reduced = CropService.Resample(image, 0, 0, image.Width, image.Height,
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
