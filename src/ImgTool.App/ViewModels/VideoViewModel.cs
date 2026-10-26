using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using ImgTool.App.Infrastructure;
using ImgTool.App.Models;
using ImgTool.Core.Models;
using ImgTool.Core.Services;

namespace ImgTool.App.ViewModels;

public sealed class VideoViewModel : BindableBase
{
    private string _sourcePath = string.Empty;
    private string _outputDirectory = string.Empty;
    private string _fileNamePattern = "{name}_frame_{index}";
    private ImageFormatKind _format = ImageFormatKind.Png;
    private int _quality = 90;
    private CollisionPolicy _collisionPolicy = CollisionPolicy.Rename;
    private double _frameIntervalSeconds = 1.0;
    private bool _takeEveryFrame;
    private int _everyNthFrame = 1;
    private bool _isExtracting;
    private bool _canCancel;
    private int _progressCurrent;
    private int _progressTotal;
    private string _progressText = string.Empty;
    private string _infoText = string.Empty;
    private string _logText = string.Empty;
    private bool _hasSource;

    public VideoViewModel()
    {
        AddFileCommand = new AsyncRelayCommand(AddFileAsync);
        AddFolderCommand = new AsyncRelayCommand(AddFolderAsync);
        BrowseOutputCommand = new RelayCommand(BrowseOutput);
        ExtractCommand = new AsyncRelayCommand(ExtractAsync, () => CanExtract);
        CancelCommand = new RelayCommand(Cancel, () => CanCancel);
        ClearLogCommand = new RelayCommand(ClearLog);
    }

    public ObservableCollection<FileItem> Files { get; } = [];

    public ICommand AddFileCommand { get; }
    public ICommand AddFolderCommand { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand ExtractCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearLogCommand { get; }

    public string SourcePath { get => _sourcePath; set => Set(ref _sourcePath, value); }

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
        set => Set(ref _format, value);
    }

    public int Quality
    {
        get => _quality;
        set => Set(ref _quality, value);
    }

    public CollisionPolicy CollisionPolicy
    {
        get => _collisionPolicy;
        set => Set(ref _collisionPolicy, value);
    }

    public double FrameIntervalSeconds
    {
        get => _frameIntervalSeconds;
        set => Set(ref _frameIntervalSeconds, value);
    }

    public bool TakeEveryFrame
    {
        get => _takeEveryFrame;
        set => Set(ref _takeEveryFrame, value);
    }

    public int EveryNthFrame
    {
        get => _everyNthFrame;
        set => Set(ref _everyNthFrame, value);
    }

    public bool IsExtracting
    {
        get => _isExtracting;
        set
        {
            if (Set(ref _isExtracting, value))
            {
                RaisePropertyChanged(nameof(CanExtract));
                ((AsyncRelayCommand)ExtractCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanCancel
    {
        get => _canCancel;
        set
        {
            if (Set(ref _canCancel, value))
                ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
        }
    }

    public int ProgressCurrent
    {
        get => _progressCurrent;
        set => Set(ref _progressCurrent, value);
    }

    public int ProgressTotal
    {
        get => _progressTotal;
        set => Set(ref _progressTotal, value);
    }

    public string ProgressText
    {
        get => _progressText;
        set => Set(ref _progressText, value);
    }

    public string InfoText
    {
        get => _infoText;
        set => Set(ref _infoText, value);
    }

    public string LogText
    {
        get => _logText;
        set => Set(ref _logText, value);
    }

    public bool HasSource
    {
        get => _hasSource;
        set => Set(ref _hasSource, value);
    }

    public bool CanExtract => HasSource && !IsExtracting && !string.IsNullOrWhiteSpace(FileNamePattern);

    private CancellationTokenSource? _cts;

    public Task AddFileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Выбрать MP4",
            Filter = "MP4|*.mp4|Все видео|*.mp4;*.mkv;*.avi;*.mov|Все файлы|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
            return Task.CompletedTask;

        AddSource(dialog.FileName);
        return Task.CompletedTask;
    }

    public Task AddFolderAsync()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Выбрать папку с видео"
        };

        if (dialog.ShowDialog() != true)
            return Task.CompletedTask;

        string[] files = Directory.GetFiles(dialog.FolderName, "*.mp4", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(dialog.FolderName, "*.mkv", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(dialog.FolderName, "*.avi", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(dialog.FolderName, "*.mov", SearchOption.AllDirectories))
            .ToArray();

        if (files.Length == 0)
        {
            AppendLog("В папке не найдено видеофайлов.");
            return Task.CompletedTask;
        }

        AddSource(files[0]);
        return Task.CompletedTask;
    }

    public void AddSource(string path)
    {
        if (!File.Exists(path))
            return;

        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".mp4" && ext != ".mkv" && ext != ".avi" && ext != ".mov")
            return;

        SourcePath = path;
        HasSource = true;
        if (string.IsNullOrWhiteSpace(OutputDirectory))
            OutputDirectory = Path.GetDirectoryName(path) ?? string.Empty;

        try
        {
            var info = VideoFrameExtractor.ReadInfo(path);
            InfoText = $"Разрешение: {info.Width}×{info.Height}\nДлительность: {info.DurationSeconds:F1} с\nFPS: {(info.Fps.HasValue ? info.Fps.Value.ToString("F2") : "неизвестно")}";
            AppendLog($"Загружено: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            InfoText = $"Не удалось прочитать информацию о видео: {ex.Message}";
        }

        RaisePropertyChanged(nameof(CanExtract));
        ((AsyncRelayCommand)ExtractCommand).RaiseCanExecuteChanged();
    }

    public void AddDroppedFiles(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                string[] files = Directory.GetFiles(path, "*.mp4", SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    AddSource(files[0]);
                    break;
                }
                continue;
            }

            if (File.Exists(path))
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".mp4" || ext == ".mkv" || ext == ".avi" || ext == ".mov")
                {
                    AddSource(path);
                    break;
                }
            }
        }
    }

    private void BrowseOutput()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Выбрать папку для сохранения кадров"
        };

        if (dialog.ShowDialog() == true)
            OutputDirectory = dialog.FolderName;
    }

    private async Task ExtractAsync()
    {
        if (string.IsNullOrWhiteSpace(SourcePath) || !File.Exists(SourcePath))
            return;

        IsExtracting = true;
        CanCancel = true;
        _cts = new CancellationTokenSource();
        ProgressCurrent = 0;
        ProgressTotal = 0;
        ProgressText = "Подготовка...";
        AppendLog("Начало извлечения кадров...");

        var options = new VideoFrameExtractionOptions
        {
            SourcePath = SourcePath,
            OutputDirectory = string.IsNullOrWhiteSpace(OutputDirectory) ? (Path.GetDirectoryName(SourcePath) ?? string.Empty) : OutputDirectory,
            FileNamePattern = FileNamePattern,
            Format = Format,
            Quality = Quality,
            CollisionPolicy = CollisionPolicy,
            FrameIntervalSeconds = FrameIntervalSeconds,
            TakeEveryFrame = TakeEveryFrame,
            EveryNthFrame = EveryNthFrame > 0 ? EveryNthFrame : 1
        };

        try
        {
            await Task.Run(() =>
            {
                var result = VideoFrameExtractor.Extract(SourcePath, options, (current, total, text) =>
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        ProgressCurrent = current;
                        ProgressTotal = total;
                        ProgressText = text;
                    });
                }, _cts.Token);

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (result.Success)
                    {
                        AppendLog($"Готово. Сохранено кадров: {result.SavedCount}");
                    }
                    else if (!string.IsNullOrWhiteSpace(result.Error))
                    {
                        AppendLog($"Ошибка: {result.Error}");
                    }

                    if (result.SkippedCount > 0)
                        AppendLog($"Пропущено из-за совпадения имён: {result.SkippedCount}");
                });
            }, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Извлечение отменено.");
        }
        catch (Exception ex)
        {
            AppendLog($"Ошибка: {ex.Message}");
        }
        finally
        {
            IsExtracting = false;
            CanCancel = false;
            _cts?.Dispose();
            _cts = null;
            ProgressText = string.Empty;
        }
    }

    private void Cancel()
    {
        _cts?.Cancel();
    }

    private void ClearLog()
    {
        LogText = string.Empty;
    }

    private void AppendLog(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        LogText = string.IsNullOrEmpty(LogText) ? line : LogText + Environment.NewLine + line;
    }

    public Settings.VideoSettings Capture() => new()
    {
        OutputDirectory = OutputDirectory,
        FileNamePattern = FileNamePattern,
        Format = Format,
        Quality = Quality,
        CollisionPolicy = CollisionPolicy,
        FrameIntervalSeconds = FrameIntervalSeconds,
        TakeEveryFrame = TakeEveryFrame,
        EveryNthFrame = EveryNthFrame
    };

    public void Shutdown()
    {
        Cancel();
    }
}
