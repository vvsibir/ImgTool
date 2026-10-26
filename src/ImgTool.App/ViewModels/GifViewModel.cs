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

/// <summary>Извлечение кадров из GIF в набор изображений.</summary>
public sealed class GifViewModel : BindableBase
{
    private CancellationTokenSource? _cancellation;
    private FileItem? _selectedFile;
    private BitmapSource? _previewSource;
    private int _previewVersion;
    private bool _isBusy;
    private double _progressValue;
    private string _statusText = "Добавьте GIF-файлы, чтобы извлечь кадры.";

    private string _outputDirectory = string.Empty;
    private string _fileNamePattern = "{name}_frame_{index}";
    private ImageFormatKind _format = ImageFormatKind.Png;
    private int _quality = 92;
    private CollisionPolicy _collisionPolicy = CollisionPolicy.Rename;
    private bool _recursiveFolders;

    public GifViewModel()
    {
        AddFilesCommand = new RelayCommand(AddFiles);
        AddFolderCommand = new RelayCommand(AddFolder);
        RemoveSelectedCommand = new RelayCommand(RemoveSelected);
        ClearCommand = new RelayCommand(ClearFiles);
        BrowseOutputCommand = new RelayCommand(BrowseOutput);
        OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder);
        ExtractCommand = new AsyncRelayCommand(ExtractAsync, () => !IsBusy && Files.Count > 0);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy && _cancellation is not null);

        Files.CollectionChanged += (_, _) =>
        {
            ExtractCommand.RaiseCanExecuteChanged();
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

    public RelayCommand OpenOutputFolderCommand { get; }

    public AsyncRelayCommand ExtractCommand { get; }

    public RelayCommand CancelCommand { get; }

    public IReadOnlyList<NamedValue<ImageFormatKind>> Formats { get; } =
    [
        new("PNG", ImageFormatKind.Png),
        new("JPEG", ImageFormatKind.Jpeg),
        new("WebP", ImageFormatKind.WebP),
        new("BMP", ImageFormatKind.Bmp)
    ];

    public IReadOnlyList<NamedValue<CollisionPolicy>> CollisionPolicies { get; } =
    [
        new("Добавить суффикс", CollisionPolicy.Rename),
        new("Перезаписать", CollisionPolicy.Overwrite),
        new("Пропустить", CollisionPolicy.Skip)
    ];

    public FileItem? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (Set(ref _selectedFile, value))
            {
                LoadPreview(value);
                RemoveSelectedCommand.RaiseCanExecuteChanged();
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

            ExtractCommand.RaiseCanExecuteChanged();
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

    public bool RecursiveFolders
    {
        get => _recursiveFolders;
        set => Set(ref _recursiveFolders, value);
    }

    public bool QualityEnabled => Format is ImageFormatKind.Jpeg or ImageFormatKind.WebP;

    public string FilesCountText => Files.Count == 0
        ? "GIF-файлы не выбраны"
        : $"{Files.Count} GIF · {FileItem.FormatSize(Files.Sum(f => File.Exists(f.Path) ? new FileInfo(f.Path).Length : 0))}";

    public void AddPaths(IEnumerable<string> paths)
    {
        var existing = new HashSet<string>(Files.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
        FileItem? firstAdded = null;

        foreach (string path in FileItem.ExpandPaths(
                     paths,
                     RecursiveFolders,
                     GifFrameExtractor.IsSupportedSource))
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
            StatusText = $"Добавлено GIF: {Files.Count}.";
        }
        else
        {
            StatusText = "GIF-файлы не найдены.";
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
            Title = "Выберите GIF-файлы",
            Multiselect = true,
            Filter = "GIF|*.gif|Все файлы|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            AddPaths(dialog.FileNames);
        }
    }

    private void AddFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Выберите папку с GIF" };
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
        var dialog = new OpenFolderDialog { Title = "Папка для кадров" };
        if (dialog.ShowDialog() == true)
        {
            OutputDirectory = dialog.FolderName;
        }
    }

    private void OpenOutputFolder()
    {
        string directory = string.IsNullOrWhiteSpace(OutputDirectory)
            ? (Files.FirstOrDefault()?.Folder ?? string.Empty)
            : Environment.ExpandEnvironmentVariables(OutputDirectory.Trim());

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            StatusText = "Папка результата ещё не создана.";
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
    }

    private void Cancel() => _cancellation?.Cancel();

    private async Task ExtractAsync()
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

        var options = new GifExtractionOptions
        {
            OutputDirectory = OutputDirectory,
            FileNamePattern = FileNamePattern,
            Format = Format,
            Quality = Quality,
            CollisionPolicy = CollisionPolicy
        };

        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsBusy = true;
        ProgressValue = 0;
        Log.Clear();
        Log.Add($"Извлечение кадров: {Files.Count} GIF.");

        try
        {
            int saved = 0;
            int skipped = 0;
            int failedFiles = 0;
            CancellationToken token = cancellation.Token;

            for (int index = 0; index < Files.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                FileItem item = Files[index];
                int fileIndex = index;

                StatusText = $"GIF {index + 1} из {Files.Count}: {item.Name}";

                var progress = new Progress<GifFrameProgress>(value =>
                {
                    double fraction = value.Total == 0
                        ? 0
                        : value.Completed / (double)value.Total;
                    ProgressValue = (fileIndex + fraction) * 100.0 / Files.Count;
                    StatusText =
                        $"GIF {fileIndex + 1} из {Files.Count}: кадр {value.Completed} из {value.Total} — {item.Name}";
                });

                try
                {
                    GifExtractionResult result = await Task.Run(
                        () => GifFrameExtractor.Extract(item.Path, options, progress, token),
                        token);

                    saved += result.SavedCount;
                    skipped += result.SkippedCount;

                    if (result.Errors.Count > 0)
                    {
                        failedFiles++;
                        AddLog($"✗ {item.Name}: {result.Errors[0]}");
                        foreach (string error in result.Errors.Skip(1).Take(20))
                        {
                            AddLog("  " + error);
                        }
                    }
                    else if (result.SkippedCount == result.FrameCount)
                    {
                        AddLog($"— {item.Name}: все кадры пропущены");
                    }
                    else
                    {
                        AddLog($"✓ {item.Name}: {result.SavedCount} кадров");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failedFiles++;
                    AddLog($"✗ {item.Name}: {ex.Message}");
                }
            }

            ProgressValue = 100;
            StatusText = $"Готово: сохранено {saved}, пропущено {skipped}, ошибок {failedFiles}.";
            AddLog(StatusText);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Извлечение отменено.";
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
            return "Укажите шаблон имени кадра.";
        }

        if (Format == ImageFormatKind.Keep)
        {
            return "Выберите формат кадров.";
        }

        if (!string.IsNullOrWhiteSpace(OutputDirectory))
        {
            try
            {
                string directory = Environment.ExpandEnvironmentVariables(OutputDirectory.Trim());
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                return "Не удалось создать папку результата: " + ex.Message;
            }
        }

        return null;
    }

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
