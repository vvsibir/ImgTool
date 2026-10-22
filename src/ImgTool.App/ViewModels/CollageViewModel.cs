using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using ImgTool.App.Infrastructure;
using ImgTool.App.Models;
using ImgTool.Core.Models;
using ImgTool.Core.Services;
using Microsoft.Win32;

namespace ImgTool.App.ViewModels;

/// <summary>Сборка коллажа.</summary>
public sealed class CollageViewModel : BindableBase
{
    private readonly CollageBuilder _builder = new();
    private CancellationTokenSource? _cancellation;
    private FileItem? _selectedFile;
    private BitmapSource? _previewSource;
    private bool _isBusy;
    private double _progressValue;
    private string _statusText = "Добавьте изображения для коллажа.";

    private int _columns = 3;
    private int? _width = 1920;
    private double _cellAspect = 1.0;
    private int _spacing = 12;
    private int _padding = 16;
    private string _backgroundHex = "#12161F";
    private bool _transparentBackground;
    private CollageLayout _layout = CollageLayout.Grid;
    private CollageFit _fit = CollageFit.Cover;
    private ImageFormatKind _format = ImageFormatKind.Jpeg;
    private int _quality = 90;
    private string _outputPath = string.Empty;

    public CollageViewModel()
    {
        AddFilesCommand = new RelayCommand(AddFiles);
        AddFolderCommand = new RelayCommand(AddFolder);
        RemoveSelectedCommand = new RelayCommand(RemoveSelected);
        MoveUpCommand = new RelayCommand(MoveUp, () => SelectedFile is not null);
        MoveDownCommand = new RelayCommand(MoveDown, () => SelectedFile is not null);
        ClearCommand = new RelayCommand(ClearFiles);
        BrowseOutputCommand = new RelayCommand(BrowseOutput);
        BuildCommand = new AsyncRelayCommand(BuildAsync, () => !IsBusy && Files.Count > 0);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy && _cancellation is not null);

        Files.CollectionChanged += (_, _) =>
        {
            BuildCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(FilesCountText));
        };
    }

    public ObservableCollection<FileItem> Files { get; } = [];

    public RelayCommand AddFilesCommand { get; }

    public RelayCommand AddFolderCommand { get; }

    public RelayCommand RemoveSelectedCommand { get; }

    public RelayCommand MoveUpCommand { get; }

    public RelayCommand MoveDownCommand { get; }

    public RelayCommand ClearCommand { get; }

    public RelayCommand BrowseOutputCommand { get; }

    public AsyncRelayCommand BuildCommand { get; }

    public RelayCommand CancelCommand { get; }

    public IReadOnlyList<NamedValue<CollageFit>> Fits { get; } =
    [
        new("Заполнить ячейку (обрезка)", CollageFit.Cover),
        new("Вписать целиком", CollageFit.Contain)
    ];

    public IReadOnlyList<NamedValue<CollageLayout>> Layouts { get; } =
    [
        new("Сетка", CollageLayout.Grid),
        new("Вертикальная склейка", CollageLayout.VerticalStrip),
        new("Горизонтальная склейка", CollageLayout.HorizontalStrip)
    ];

    public IReadOnlyList<NamedValue<ImageFormatKind>> Formats { get; } =
    [
        new("JPEG", ImageFormatKind.Jpeg),
        new("PNG", ImageFormatKind.Png),
        new("WebP", ImageFormatKind.WebP),
        new("BMP", ImageFormatKind.Bmp)
    ];

    public FileItem? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (Set(ref _selectedFile, value))
            {
                MoveUpCommand.RaiseCanExecuteChanged();
                MoveDownCommand.RaiseCanExecuteChanged();
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

            BuildCommand.RaiseCanExecuteChanged();
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
        ? "Изображения не выбраны"
        : $"{Files.Count} изображений";

    public int Columns
    {
        get => _columns;
        set => Set(ref _columns, Math.Clamp(value, 1, 12));
    }

    public int? Width
    {
        get => _width;
        set => Set(ref _width, value is null ? null : Math.Clamp(value.Value, 200, 20000));
    }

    public double CellAspect
    {
        get => _cellAspect;
        set => Set(ref _cellAspect, Math.Clamp(value, 0.2, 5.0));
    }

    public int Spacing
    {
        get => _spacing;
        set => Set(ref _spacing, Math.Clamp(value, 0, 400));
    }

    public int Padding
    {
        get => _padding;
        set => Set(ref _padding, Math.Clamp(value, 0, 400));
    }

    public string BackgroundHex
    {
        get => _backgroundHex;
        set => Set(ref _backgroundHex, value);
    }

    public bool TransparentBackground
    {
        get => _transparentBackground;
        set
        {
            if (Set(ref _transparentBackground, value))
            {
                RaisePropertyChanged(nameof(IsBackgroundColorEnabled));
            }
        }
    }

    public bool SupportsTransparentBackground => Format is ImageFormatKind.Png or ImageFormatKind.WebP;

    public bool IsBackgroundColorEnabled => !TransparentBackground;

    public CollageFit Fit
    {
        get => _fit;
        set => Set(ref _fit, value);
    }

    public CollageLayout Layout
    {
        get => _layout;
        set
        {
            if (Set(ref _layout, value))
            {
                RaisePropertyChanged(nameof(IsGridLayout), nameof(IsStripLayout));
            }
        }
    }

    public bool IsGridLayout => Layout == CollageLayout.Grid;

    public bool IsStripLayout => !IsGridLayout;

    public ImageFormatKind Format
    {
        get => _format;
        set
        {
            if (!Set(ref _format, value))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(OutputPath))
            {
                OutputPath = Path.ChangeExtension(OutputPath, ImageFormatHelper.GetExtension(value));
            }

            RaisePropertyChanged(nameof(SupportsTransparentBackground));

            if (!SupportsTransparentBackground && TransparentBackground)
            {
                TransparentBackground = false;
            }
        }
    }

    public int Quality
    {
        get => _quality;
        set => Set(ref _quality, Math.Clamp(value, 1, 100));
    }

    public string OutputPath
    {
        get => _outputPath;
        set => Set(ref _outputPath, value);
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        var existing = new HashSet<string>(Files.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
        FileItem? firstAdded = null;

        foreach (string path in FileItem.ExpandPaths(paths))
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
            StatusText = $"Добавлено изображений: {Files.Count}.";
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
            Title = "Изображения для коллажа",
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
        var dialog = new OpenFolderDialog { Title = "Папка с изображениями" };
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

    private void MoveUp()
    {
        if (SelectedFile is null)
        {
            return;
        }

        int index = Files.IndexOf(SelectedFile);
        if (index <= 0)
        {
            return;
        }

        FileItem item = SelectedFile;
        Files.Move(index, index - 1);
        SelectedFile = item;
    }

    private void MoveDown()
    {
        if (SelectedFile is null)
        {
            return;
        }

        int index = Files.IndexOf(SelectedFile);
        if (index < 0 || index >= Files.Count - 1)
        {
            return;
        }

        FileItem item = SelectedFile;
        Files.Move(index, index + 1);
        SelectedFile = item;
    }

    private void ClearFiles()
    {
        Files.Clear();
        SelectedFile = null;
        PreviewSource = null;
        ProgressValue = 0;
        StatusText = "Список очищен.";
    }

    private void BrowseOutput()
    {
        string extension = ImageFormatHelper.GetExtension(Format);
        var dialog = new SaveFileDialog
        {
            Title = "Сохранить коллаж",
            Filter = "JPEG|*.jpg|PNG|*.png|WebP|*.webp|BMP|*.bmp",
            DefaultExt = extension,
            AddExtension = true,
            FileName = "collage" + extension,
            FilterIndex = Format switch
            {
                ImageFormatKind.Png => 2,
                ImageFormatKind.WebP => 3,
                ImageFormatKind.Bmp => 4,
                _ => 1
            }
        };

        if (dialog.ShowDialog() == true)
        {
            OutputPath = dialog.FileName;
            Format = ImageFormatHelper.FromExtension(dialog.FileName) switch
            {
                ImageFormatKind.Keep => Format,
                var detected => detected
            };
        }
    }

    private void Cancel() => _cancellation?.Cancel();

    private async Task BuildAsync()
    {
        string? validation = Validate();
        if (validation is not null)
        {
            StatusText = validation;
            return;
        }

        if (string.IsNullOrEmpty(Path.GetExtension(OutputPath)))
        {
            OutputPath = Path.ChangeExtension(OutputPath, ImageFormatHelper.GetExtension(Format));
        }

        _cancellation = new CancellationTokenSource();
        IsBusy = true;
        ProgressValue = 0;
        StatusText = "Сборка коллажа…";

        try
        {
            var options = new CollageOptions
            {
                Columns = Columns,
                Width = Width,
                CellAspect = CellAspect,
                Spacing = Spacing,
                Padding = Padding,
                BackgroundHex = BackgroundHex,
                TransparentBackground = TransparentBackground,
                Layout = Layout,
                Fit = Fit,
                Format = Format,
                Quality = Quality
            };

            var progress = new Progress<double>(value =>
            {
                ProgressValue = value;
                StatusText = $"Сборка коллажа: {value:0}%";
            });

            CollageResult result = await _builder.BuildAsync(
                Files.Select(f => f.Path).ToArray(),
                options,
                OutputPath,
                progress,
                _cancellation.Token);

            ProgressValue = 100;
            StatusText = $"Коллаж сохранён: {Path.GetFileName(result.OutputPath)} · {result.Width}×{result.Height}";
            PreviewSource = await Task.Run(() => BitmapHelper.LoadPreview(result.OutputPath));
        }
        catch (OperationCanceledException)
        {
            StatusText = "Сборка коллажа отменена.";
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            _cancellation?.Dispose();
            _cancellation = null;
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    private string? Validate()
    {
        if (Files.Count == 0)
        {
            return "Добавьте хотя бы одно изображение.";
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            return "Укажите путь для сохранения коллажа.";
        }

        if (!TransparentBackground && !ColorParser.IsValid(BackgroundHex))
        {
            return "Цвет фона должен быть в формате HEX, например #12161F или #0012161F.";
        }

        return null;
    }

    private async void LoadPreview(FileItem? item)
    {
        if (item is null)
        {
            PreviewSource = null;
            return;
        }

        string path = item.Path;
        BitmapSource? preview = await Task.Run(() => BitmapHelper.LoadPreview(path));
        if (SelectedFile?.Path == path)
        {
            PreviewSource = preview;
        }
    }
}
