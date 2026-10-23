using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using ImgTool.App.Infrastructure;
using ImgTool.App.Models;
using ImgTool.Core.Models;
using ImgTool.Core.Services;
using Microsoft.Win32;

namespace ImgTool.App.ViewModels;

/// <summary>Пакетная обработка изображений.</summary>
public sealed class BatchViewModel : BindableBase
{
    private readonly ImageProcessor _processor = new();
    private CancellationTokenSource? _cancellation;
    private FileItem? _selectedFile;
    private BitmapSource? _previewSource;
    private int _previewVersion;
    private bool _isBusy;
    private double _progressValue;
    private string _statusText = "Добавьте изображения, чтобы начать.";

    private string _outputDirectory = string.Empty;
    private string _fileNamePattern = "{name}_out";
    private ImageFormatKind _format = ImageFormatKind.Keep;
    private int _quality = 88;
    private CollisionPolicy _collisionPolicy = CollisionPolicy.Rename;
    private bool _preserveMetadata = true;
    private bool _resizeEnabled;
    private ResizeMode _resizeMode = ResizeMode.Fit;
    private int _resizeWidth = 1920;
    private int _resizeHeight = 1080;
    private int _resizePercent = 50;
    private bool _dontUpscale = true;
    private bool _cropEnabled;
    private CropMode _cropMode = CropMode.AspectRatio;
    private CropUnit _cropUnit = CropUnit.Pixels;
    private double _cropAspectWidth = 1;
    private double _cropAspectHeight = 1;
    private Anchor _cropAnchor = Anchor.Center;
    private double _cropMarginLeft;
    private double _cropMarginTop;
    private double _cropMarginRight;
    private double _cropMarginBottom;
    private double _cropRectangleX;
    private double _cropRectangleY;
    private double _cropRectangleWidth;
    private double _cropRectangleHeight;
    private int _rotationDegrees;
    private bool _flipHorizontal;
    private bool _flipVertical;

    private bool _chromaKeyEnabled;
    private string _chromaKeyColor = "#00FF00";
    private double _chromaKeyTolerance = 35;
    private double _chromaKeySoftness = 8;
    private bool _chromaKeyDespill = true;
    private string _chromaKeyBackground = "#FFFFFF";

    private bool _watermarkEnabled;
    private WatermarkKind _watermarkKind = WatermarkKind.Text;
    private string _watermarkText = "imgTool";
    private string _watermarkFont = "Segoe UI";
    private float _watermarkFontSize = 42f;
    private string _watermarkColor = "#FFFFFF";
    private float _watermarkOpacity = 0.65f;
    private Anchor _watermarkPosition = Anchor.BottomRight;
    private float _watermarkMargin = 24f;
    private bool _watermarkShadow = true;
    private string _watermarkLogoPath = string.Empty;
    private float _watermarkLogoWidth = 18f;

    private bool _recursiveFolders;

    public BatchViewModel()
    {
        AddFilesCommand = new RelayCommand(AddFiles);
        AddFolderCommand = new RelayCommand(AddFolder);
        RemoveSelectedCommand = new RelayCommand(RemoveSelected);
        ClearCommand = new RelayCommand(ClearFiles);
        BrowseOutputCommand = new RelayCommand(BrowseOutput);
        BrowseLogoCommand = new RelayCommand(BrowseLogo);
        OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder);
        RunCommand = new AsyncRelayCommand(RunAsync, () => !IsBusy && Files.Count > 0);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy && _cancellation is not null);

        Files.CollectionChanged += (_, _) =>
        {
            RunCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(FilesCountText));
        };
    }

    public ObservableCollection<FileItem> Files { get; } = [];

    public ObservableCollection<string> Log { get; } = [];

    public RelayCommand AddFilesCommand { get; }

    public RelayCommand AddFolderCommand { get; }

    public RelayCommand RemoveSelectedCommand { get; }

    public RelayCommand ClearCommand { get; }

    public RelayCommand BrowseOutputCommand { get; }

    public RelayCommand BrowseLogoCommand { get; }

    public RelayCommand OpenOutputFolderCommand { get; }

    public AsyncRelayCommand RunCommand { get; }

    public RelayCommand CancelCommand { get; }

    public IReadOnlyList<NamedValue<ImageFormatKind>> Formats { get; } =
    [
        new("Как в исходнике", ImageFormatKind.Keep),
        new("JPEG", ImageFormatKind.Jpeg),
        new("PNG", ImageFormatKind.Png),
        new("WebP", ImageFormatKind.WebP),
        new("BMP", ImageFormatKind.Bmp)
    ];

    public IReadOnlyList<NamedValue<ResizeMode>> ResizeModes { get; } =
    [
        new("Вписать в рамки", ResizeMode.Fit),
        new("Заполнить с обрезкой", ResizeMode.Cover),
        new("Растянуть", ResizeMode.Stretch),
        new("Процент", ResizeMode.Percent)
    ];

    public IReadOnlyList<NamedValue<CropMode>> CropModes { get; } =
    [
        new("По пропорции", CropMode.AspectRatio),
        new("Одинаковые поля", CropMode.Margins),
        new("Прямоугольник", CropMode.Rectangle)
    ];

    public IReadOnlyList<NamedValue<CropUnit>> CropUnits { get; } =
    [
        new("Пиксели", CropUnit.Pixels),
        new("Проценты", CropUnit.Percent)
    ];

    public IReadOnlyList<NamedValue<Anchor>> Anchors { get; } =
    [
        new("Сверху слева", Anchor.TopLeft),
        new("Сверху", Anchor.Top),
        new("Сверху справа", Anchor.TopRight),
        new("Слева", Anchor.Left),
        new("По центру", Anchor.Center),
        new("Справа", Anchor.Right),
        new("Снизу слева", Anchor.BottomLeft),
        new("Снизу", Anchor.Bottom),
        new("Снизу справа", Anchor.BottomRight)
    ];

    public IReadOnlyList<NamedValue<CollisionPolicy>> CollisionPolicies { get; } =
    [
        new("Добавить суффикс", CollisionPolicy.Rename),
        new("Перезаписать", CollisionPolicy.Overwrite),
        new("Пропустить", CollisionPolicy.Skip)
    ];

    public IReadOnlyList<NamedValue<int>> Rotations { get; } =
    [
        new("Не поворачивать", 0),
        new("90° по часовой", 90),
        new("180°", 180),
        new("270° по часовой", 270)
    ];

    public IReadOnlyList<NamedValue<WatermarkKind>> WatermarkKinds { get; } =
    [
        new("Текст", WatermarkKind.Text),
        new("Логотип", WatermarkKind.Logo)
    ];

    public FileItem? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (Set(ref _selectedFile, value))
            {
                LoadPreview(value);
            }
        }
    }

    public BitmapSource? PreviewSource
    {
        get => _previewSource;
        private set => Set(ref _previewSource, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!Set(ref _isBusy, value))
            {
                return;
            }

            RunCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => Set(ref _progressValue, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public string FilesCountText => Files.Count == 0
        ? "Файлы не выбраны"
        : $"{Files.Count} файл(ов) · {FileItem.FormatSize(Files.Sum(f => new FileInfo(f.Path).Exists ? new FileInfo(f.Path).Length : 0))}";

    public string OutputDirectory
    {
        get => _outputDirectory;
        set => Set(ref _outputDirectory, value);
    }

    public string FileNamePattern
    {
        get => _fileNamePattern;
        set => Set(ref _fileNamePattern, value);
    }

    public ImageFormatKind Format
    {
        get => _format;
        set
        {
            if (Set(ref _format, value))
            {
                RaisePropertyChanged(nameof(QualityEnabled));
            }
        }
    }

    public int Quality
    {
        get => _quality;
        set => Set(ref _quality, Math.Clamp(value, 1, 100));
    }

    public CollisionPolicy CollisionPolicy
    {
        get => _collisionPolicy;
        set => Set(ref _collisionPolicy, value);
    }

    public bool PreserveMetadata
    {
        get => _preserveMetadata;
        set => Set(ref _preserveMetadata, value);
    }

    public bool ResizeEnabled
    {
        get => _resizeEnabled;
        set => Set(ref _resizeEnabled, value);
    }

    public ResizeMode ResizeMode
    {
        get => _resizeMode;
        set
        {
            if (Set(ref _resizeMode, value))
            {
                RaisePropertyChanged(nameof(IsPercentResize), nameof(IsBoxResize));
            }
        }
    }

    public bool IsPercentResize => ResizeMode == ResizeMode.Percent;

    public bool IsBoxResize => ResizeMode != ResizeMode.Percent;

    public int ResizeWidth
    {
        get => _resizeWidth;
        set => Set(ref _resizeWidth, Math.Max(0, value));
    }

    public int ResizeHeight
    {
        get => _resizeHeight;
        set => Set(ref _resizeHeight, Math.Max(0, value));
    }

    public int ResizePercent
    {
        get => _resizePercent;
        set => Set(ref _resizePercent, Math.Clamp(value, 1, 1000));
    }

    public bool DontUpscale
    {
        get => _dontUpscale;
        set => Set(ref _dontUpscale, value);
    }

    public bool CropEnabled
    {
        get => _cropEnabled;
        set => Set(ref _cropEnabled, value);
    }

    public CropMode CropMode
    {
        get => _cropMode;
        set
        {
            if (Set(ref _cropMode, value))
            {
                RaisePropertyChanged(
                    nameof(IsAspectCrop),
                    nameof(IsMarginsCrop),
                    nameof(IsRectangleCrop),
                    nameof(IsCropUnitVisible));
            }
        }
    }

    public bool IsAspectCrop => CropMode == CropMode.AspectRatio;

    public bool IsMarginsCrop => CropMode == CropMode.Margins;

    public bool IsRectangleCrop => CropMode == CropMode.Rectangle;

    public bool IsCropUnitVisible => CropMode != CropMode.AspectRatio;

    public CropUnit CropUnit
    {
        get => _cropUnit;
        set => Set(ref _cropUnit, value);
    }

    public double CropAspectWidth
    {
        get => _cropAspectWidth;
        set => Set(ref _cropAspectWidth, Math.Max(0.01, value));
    }

    public double CropAspectHeight
    {
        get => _cropAspectHeight;
        set => Set(ref _cropAspectHeight, Math.Max(0.01, value));
    }

    public Anchor CropAnchor
    {
        get => _cropAnchor;
        set => Set(ref _cropAnchor, value);
    }

    public double CropMarginLeft
    {
        get => _cropMarginLeft;
        set => Set(ref _cropMarginLeft, Math.Max(0, value));
    }

    public double CropMarginTop
    {
        get => _cropMarginTop;
        set => Set(ref _cropMarginTop, Math.Max(0, value));
    }

    public double CropMarginRight
    {
        get => _cropMarginRight;
        set => Set(ref _cropMarginRight, Math.Max(0, value));
    }

    public double CropMarginBottom
    {
        get => _cropMarginBottom;
        set => Set(ref _cropMarginBottom, Math.Max(0, value));
    }

    public double CropRectangleX
    {
        get => _cropRectangleX;
        set => Set(ref _cropRectangleX, Math.Max(0, value));
    }

    public double CropRectangleY
    {
        get => _cropRectangleY;
        set => Set(ref _cropRectangleY, Math.Max(0, value));
    }

    public double CropRectangleWidth
    {
        get => _cropRectangleWidth;
        set => Set(ref _cropRectangleWidth, Math.Max(0, value));
    }

    public double CropRectangleHeight
    {
        get => _cropRectangleHeight;
        set => Set(ref _cropRectangleHeight, Math.Max(0, value));
    }

    public int RotationDegrees
    {
        get => _rotationDegrees;
        set => Set(ref _rotationDegrees, value);
    }

    public bool FlipHorizontal
    {
        get => _flipHorizontal;
        set => Set(ref _flipHorizontal, value);
    }

    public bool FlipVertical
    {
        get => _flipVertical;
        set => Set(ref _flipVertical, value);
    }

    public bool ChromaKeyEnabled
    {
        get => _chromaKeyEnabled;
        set => Set(ref _chromaKeyEnabled, value);
    }

    public string ChromaKeyColor
    {
        get => _chromaKeyColor;
        set => Set(ref _chromaKeyColor, value);
    }

    public double ChromaKeyTolerance
    {
        get => _chromaKeyTolerance;
        set => Set(ref _chromaKeyTolerance, Math.Clamp(value, 0, 100));
    }

    public double ChromaKeySoftness
    {
        get => _chromaKeySoftness;
        set => Set(ref _chromaKeySoftness, Math.Clamp(value, 0, 100));
    }

    public bool ChromaKeyDespill
    {
        get => _chromaKeyDespill;
        set => Set(ref _chromaKeyDespill, value);
    }

    public string ChromaKeyBackground
    {
        get => _chromaKeyBackground;
        set => Set(ref _chromaKeyBackground, value);
    }

    public bool WatermarkEnabled
    {
        get => _watermarkEnabled;
        set => Set(ref _watermarkEnabled, value);
    }

    public WatermarkKind WatermarkKind
    {
        get => _watermarkKind;
        set
        {
            if (Set(ref _watermarkKind, value))
            {
                RaisePropertyChanged(nameof(IsTextWatermark), nameof(IsLogoWatermark));
            }
        }
    }

    public bool IsTextWatermark => WatermarkKind == WatermarkKind.Text;

    public bool IsLogoWatermark => WatermarkKind == WatermarkKind.Logo;

    public string WatermarkText
    {
        get => _watermarkText;
        set => Set(ref _watermarkText, value);
    }

    public string WatermarkFont
    {
        get => _watermarkFont;
        set => Set(ref _watermarkFont, value);
    }

    public float WatermarkFontSize
    {
        get => _watermarkFontSize;
        set => Set(ref _watermarkFontSize, Math.Clamp(value, 6f, 1000f));
    }

    public string WatermarkColor
    {
        get => _watermarkColor;
        set => Set(ref _watermarkColor, value);
    }

    public float WatermarkOpacity
    {
        get => _watermarkOpacity;
        set => Set(ref _watermarkOpacity, Math.Clamp(value, 0f, 1f));
    }

    public Anchor WatermarkPosition
    {
        get => _watermarkPosition;
        set => Set(ref _watermarkPosition, value);
    }

    public float WatermarkMargin
    {
        get => _watermarkMargin;
        set => Set(ref _watermarkMargin, Math.Clamp(value, 0f, 2000f));
    }

    public bool WatermarkShadow
    {
        get => _watermarkShadow;
        set => Set(ref _watermarkShadow, value);
    }

    public string WatermarkLogoPath
    {
        get => _watermarkLogoPath;
        set => Set(ref _watermarkLogoPath, value);
    }

    public float WatermarkLogoWidth
    {
        get => _watermarkLogoWidth;
        set => Set(ref _watermarkLogoWidth, Math.Clamp(value, 1f, 100f));
    }

    public bool RecursiveFolders
    {
        get => _recursiveFolders;
        set => Set(ref _recursiveFolders, value);
    }

    public bool QualityEnabled => Format is ImageFormatKind.Jpeg or ImageFormatKind.WebP;

    public void AddPaths(IEnumerable<string> paths)
    {
        var existing = new HashSet<string>(Files.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
        FileItem? firstAdded = null;

        foreach (string path in FileItem.ExpandPaths(paths, RecursiveFolders))
        {
            if (existing.Add(path))
            {
                var item = new FileItem(path);
                Files.Add(item);
                firstAdded ??= item;
            }
        }

        if (firstAdded is not null)
        {
            SelectedFile ??= firstAdded;
            StatusText = $"Добавлено файлов: {Files.Count}.";
        }
        else
        {
            StatusText = "Новые изображения не найдены.";
        }
    }

    public void Shutdown()
    {
        _cancellation?.Cancel();
    }

    private void AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите изображения",
            Multiselect = true,
            Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.webp|Все файлы|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            AddPaths(dialog.FileNames);
        }
    }

    private void AddFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Выберите папку с изображениями" };
        if (dialog.ShowDialog() == true)
        {
            AddPaths([dialog.FolderName]);
        }
    }

    private void RemoveSelected()
    {
        if (SelectedFile is null)
        {
            return;
        }

        int index = Files.IndexOf(SelectedFile);
        Files.Remove(SelectedFile);
        SelectedFile = Files.Count == 0
            ? null
            : Files[Math.Clamp(index, 0, Files.Count - 1)];
    }

    private void ClearFiles()
    {
        Files.Clear();
        SelectedFile = null;
        PreviewSource = null;
        Log.Clear();
        ProgressValue = 0;
        StatusText = "Список очищен.";
    }

    private void BrowseOutput()
    {
        var dialog = new OpenFolderDialog { Title = "Папка для сохранения" };
        if (dialog.ShowDialog() == true)
        {
            OutputDirectory = dialog.FolderName;
        }
    }

    private void BrowseLogo()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите логотип",
            Filter = "Изображения|*.png;*.jpg;*.jpeg;*.webp;*.bmp|Все файлы|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            WatermarkLogoPath = dialog.FileName;
        }
    }

    private void OpenOutputFolder()
    {
        string directory = string.IsNullOrWhiteSpace(OutputDirectory)
            ? (Files.FirstOrDefault()?.Folder ?? string.Empty)
            : OutputDirectory;

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            StatusText = "Папка результата ещё не создана.";
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
    }

    private void Cancel() => _cancellation?.Cancel();

    private async Task RunAsync()
    {
        if (Files.Count == 0)
        {
            return;
        }

        string? validation = ValidateOptions();
        if (validation is not null)
        {
            StatusText = validation;
            return;
        }

        _cancellation = new CancellationTokenSource();
        IsBusy = true;
        ProgressValue = 0;
        Log.Clear();
        Log.Add($"Запуск обработки: {Files.Count} файл(ов).");

        try
        {
            ProcessingOptions options = CreateOptions();
            string[] paths = Files.Select(f => f.Path).ToArray();

            var progress = new Progress<BatchProgress>(p =>
            {
                ProgressValue = p.Total == 0 ? 0 : p.Completed * 100.0 / p.Total;
                if (p.Result is null)
                {
                    StatusText = $"Обработка {Math.Min(p.Completed + 1, p.Total)} из {p.Total}: {p.FileName}";
                    return;
                }

                ProcessingResult result = p.Result;
                if (result.Skipped)
                {
                    AddLog($"— {Path.GetFileName(result.SourcePath)} — пропущен");
                }
                else if (result.Success)
                {
                    AddLog($"✓ {Path.GetFileName(result.SourcePath)} → {Path.GetFileName(result.OutputPath!)}");
                }
                else
                {
                    AddLog($"✗ {Path.GetFileName(result.SourcePath)} — {result.Error}");
                }
            });

            IReadOnlyList<ProcessingResult> results = await _processor.ProcessBatchAsync(
                paths,
                options,
                progress,
                _cancellation.Token);

            int ok = results.Count(r => r.Success);
            int skipped = results.Count(r => r.Skipped);
            int failed = results.Count - ok - skipped;
            StatusText = $"Готово: успешно {ok}, ошибок {failed}, пропущено {skipped}.";
            AddLog(StatusText);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Обработка отменена.";
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка: " + ex.Message;
            AddLog(StatusText);
        }
        finally
        {
            IsBusy = false;
            _cancellation?.Dispose();
            _cancellation = null;
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    private string? ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(FileNamePattern))
        {
            return "Укажите шаблон имени файла.";
        }

        if (CropEnabled)
        {
            switch (CropMode)
            {
                case CropMode.AspectRatio:
                    if (CropAspectWidth <= 0 || CropAspectHeight <= 0)
                    {
                        return "Пропорция обрезки должна быть положительной.";
                    }

                    break;

                case CropMode.Margins:
                    if (CropMarginLeft < 0 || CropMarginTop < 0 ||
                        CropMarginRight < 0 || CropMarginBottom < 0)
                    {
                        return "Поля обрезки не могут быть отрицательными.";
                    }

                    if (CropUnit == CropUnit.Percent &&
                        (CropMarginLeft + CropMarginRight >= 100 ||
                         CropMarginTop + CropMarginBottom >= 100))
                    {
                        return "Сумма полей обрезки должна быть меньше 100%.";
                    }

                    break;

                case CropMode.Rectangle:
                    if (CropRectangleX < 0 || CropRectangleY < 0)
                    {
                        return "Координаты обрезки не могут быть отрицательными.";
                    }

                    if (CropRectangleWidth <= 0 || CropRectangleHeight <= 0)
                    {
                        return "Укажите ширину и высоту области обрезки.";
                    }

                    if (CropUnit == CropUnit.Percent &&
                        (CropRectangleX + CropRectangleWidth > 100 ||
                         CropRectangleY + CropRectangleHeight > 100))
                    {
                        return "Область обрезки выходит за пределы 100%.";
                    }

                    break;
            }
        }

        if (WatermarkEnabled && WatermarkKind == WatermarkKind.Logo &&
            !File.Exists(WatermarkLogoPath))
        {
            return "Выберите файл логотипа для водяного знака.";
        }

        if (ChromaKeyEnabled && !ColorParser.IsValid(ChromaKeyColor))
        {
            return "Укажите корректный цвет хромакея в формате HEX.";
        }

        if (ChromaKeyEnabled && !ColorParser.IsValid(ChromaKeyBackground))
        {
            return "Укажите корректный цвет фона для JPEG/BMP в формате HEX.";
        }

        return null;
    }

    private ProcessingOptions CreateOptions() => new()
    {
        OutputDirectory = OutputDirectory,
        FileNamePattern = FileNamePattern,
        Format = Format,
        Quality = Quality,
        CollisionPolicy = CollisionPolicy,
        PreserveMetadata = PreserveMetadata,
        ResizeEnabled = ResizeEnabled,
        ResizeMode = ResizeMode,
        ResizeWidth = ResizeWidth,
        ResizeHeight = ResizeHeight,
        ResizePercent = ResizePercent,
        DontUpscale = DontUpscale,
        CropEnabled = CropEnabled,
        CropMode = CropMode,
        CropUnit = CropUnit,
        CropAspectWidth = CropAspectWidth,
        CropAspectHeight = CropAspectHeight,
        CropAnchor = CropAnchor,
        CropMarginLeft = CropMarginLeft,
        CropMarginTop = CropMarginTop,
        CropMarginRight = CropMarginRight,
        CropMarginBottom = CropMarginBottom,
        CropRectangleX = CropRectangleX,
        CropRectangleY = CropRectangleY,
        CropRectangleWidth = CropRectangleWidth,
        CropRectangleHeight = CropRectangleHeight,
        RotationDegrees = RotationDegrees,
        FlipHorizontal = FlipHorizontal,
        FlipVertical = FlipVertical,
        ChromaKey = new ChromaKeyOptions
        {
            Enabled = ChromaKeyEnabled,
            KeyColorHex = ChromaKeyColor,
            Tolerance = ChromaKeyTolerance,
            Softness = ChromaKeySoftness,
            Despill = ChromaKeyDespill,
            BackgroundHex = ChromaKeyBackground
        },
        Watermark = new WatermarkOptions
        {
            Enabled = WatermarkEnabled,
            Kind = WatermarkKind,
            Text = WatermarkText,
            FontFamily = WatermarkFont,
            FontSize = WatermarkFontSize,
            ColorHex = WatermarkColor,
            Opacity = WatermarkOpacity,
            Position = WatermarkPosition,
            Margin = WatermarkMargin,
            Shadow = WatermarkShadow,
            LogoPath = WatermarkLogoPath,
            LogoWidthPercent = WatermarkLogoWidth
        }
    };

    private async void LoadPreview(FileItem? item)
    {
        int version = ++_previewVersion;
        PreviewSource = null;

        if (item is null)
        {
            return;
        }

        string path = item.Path;
        BitmapSource? preview = await Task.Run(() => BitmapHelper.LoadPreview(path));
        if (version == _previewVersion)
        {
            PreviewSource = preview;
        }
    }

    private void AddLog(string line)
    {
        Log.Add(line);
        while (Log.Count > 500)
        {
            Log.RemoveAt(0);
        }
    }
}
