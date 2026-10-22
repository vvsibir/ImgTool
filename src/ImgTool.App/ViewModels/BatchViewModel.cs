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
    private double _cropAspectWidth = 1;
    private double _cropAspectHeight = 1;
    private Anchor _cropAnchor = Anchor.Center;
    private int _rotationDegrees;
    private bool _flipHorizontal;
    private bool _flipVertical;

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

        if (CropEnabled && (CropAspectWidth <= 0 || CropAspectHeight <= 0))
        {
            return "Пропорция обрезки должна быть положительной.";
        }

        if (WatermarkEnabled && WatermarkKind == WatermarkKind.Logo &&
            !File.Exists(WatermarkLogoPath))
        {
            return "Выберите файл логотипа для водяного знака.";
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
        CropAspectWidth = CropAspectWidth,
        CropAspectHeight = CropAspectHeight,
        CropAnchor = CropAnchor,
        RotationDegrees = RotationDegrees,
        FlipHorizontal = FlipHorizontal,
        FlipVertical = FlipVertical,
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
