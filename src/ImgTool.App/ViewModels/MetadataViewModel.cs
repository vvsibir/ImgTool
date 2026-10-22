using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ImgTool.App.Infrastructure;
using ImgTool.App.Models;
using ImgTool.Core.Services;
using Microsoft.Win32;

namespace ImgTool.App.ViewModels;

/// <summary>Просмотр EXIF, удаление метаданных и переименование по дате.</summary>
public sealed class MetadataViewModel : BindableBase
{
    private FileItem? _selectedFile;
    private bool _isBusy;
    private bool _isLoading;
    private string _statusText = "Выберите файл, чтобы увидеть метаданные.";
    private string _renamePattern = "{date}_{time}";
    private int _loadVersion;
    private bool _hasDetails;
    private string _summaryDimensions = "—";
    private string _summaryFormat = "—";
    private string _summarySize = "—";
    private string _summaryDate = "—";
    private string _summaryHasMetadata = "—";

    public MetadataViewModel()
    {
        AddFilesCommand = new RelayCommand(AddFiles);
        AddFolderCommand = new RelayCommand(AddFolder);
        ClearCommand = new RelayCommand(ClearFiles);
        RemoveMetadataCommand = new AsyncRelayCommand(
            RemoveMetadataAsync,
            () => !IsBusy && SelectedFile is not null);
        RenameCommand = new AsyncRelayCommand(
            RenameAsync,
            () => !IsBusy && Files.Count > 0);
        RefreshCommand = new RelayCommand(() => LoadDetails(SelectedFile));

        Files.CollectionChanged += (_, _) =>
        {
            RenameCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(FilesCountText));
        };
    }

    public ObservableCollection<FileItem> Files { get; } = [];

    public ObservableCollection<ExifEntry> ExifEntries { get; } = [];

    public RelayCommand AddFilesCommand { get; }

    public RelayCommand AddFolderCommand { get; }

    public RelayCommand ClearCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public AsyncRelayCommand RemoveMetadataCommand { get; }

    public AsyncRelayCommand RenameCommand { get; }

    public FileItem? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (!Set(ref _selectedFile, value))
            {
                return;
            }

            RemoveMetadataCommand.RaiseCanExecuteChanged();
            LoadDetails(value);
        }
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

            RemoveMetadataCommand.RaiseCanExecuteChanged();
            RenameCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public string RenamePattern
    {
        get => _renamePattern;
        set => Set(ref _renamePattern, value);
    }

    public bool HasDetails
    {
        get => _hasDetails;
        private set => Set(ref _hasDetails, value);
    }

    public string SummaryDimensions
    {
        get => _summaryDimensions;
        private set => Set(ref _summaryDimensions, value);
    }

    public string SummaryFormat
    {
        get => _summaryFormat;
        private set => Set(ref _summaryFormat, value);
    }

    public string SummarySize
    {
        get => _summarySize;
        private set => Set(ref _summarySize, value);
    }

    public string SummaryDate
    {
        get => _summaryDate;
        private set => Set(ref _summaryDate, value);
    }

    public string SummaryHasMetadata
    {
        get => _summaryHasMetadata;
        private set => Set(ref _summaryHasMetadata, value);
    }

    public string FilesCountText => Files.Count == 0
        ? "Файлы не выбраны"
        : $"{Files.Count} файл(ов)";

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
            StatusText = $"Добавлено файлов: {Files.Count}.";
        }
        else
        {
            StatusText = "Новые изображения не найдены.";
        }
    }

    public void Shutdown()
    {
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
        var dialog = new OpenFolderDialog { Title = "Папка с изображениями" };
        if (dialog.ShowDialog() == true)
        {
            AddPaths([dialog.FolderName]);
        }
    }

    private void ClearFiles()
    {
        Files.Clear();
        SelectedFile = null;
        ExifEntries.Clear();
        ClearSummary();
        StatusText = "Список очищен.";
    }

    private async void LoadDetails(FileItem? item)
    {
        int version = ++_loadVersion;
        IsLoading = true;
        ExifEntries.Clear();
        ClearSummary();

        if (item is null)
        {
            IsLoading = false;
            return;
        }

        string path = item.Path;

        try
        {
            ImageDetails details = await Task.Run(() => MetadataService.ReadDetails(path));
            if (version != _loadVersion)
            {
                return;
            }

            ImageSummary summary = details.Summary;
            SummaryDimensions = summary.Dimensions;
            SummaryFormat = summary.Format;
            SummarySize = FileItem.FormatSize(summary.FileSize);
            SummaryDate = MetadataService.FormatDate(summary.DateTaken);
            SummaryHasMetadata = summary.HasMetadata ? "Есть" : "Нет";
            HasDetails = true;

            foreach (ExifEntry entry in details.ExifEntries)
            {
                ExifEntries.Add(entry);
            }

            StatusText = details.ExifEntries.Count == 0
                ? "В файле нет EXIF-данных."
                : $"Найдено полей: {details.ExifEntries.Count}.";
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
            {
                StatusText = "Не удалось прочитать метаданные: " + ex.Message;
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    private async Task RemoveMetadataAsync()
    {
        FileItem? item = SelectedFile;
        if (item is null)
        {
            return;
        }

        MessageBoxResult confirm = MessageBox.Show(
            $"Удалить метаданные из файла «{item.Name}»?\n\nФайл будет перекодирован. Это необратимо.",
            "Удаление метаданных",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        StatusText = "Удаление метаданных…";

        try
        {
            string path = item.Path;
            await Task.Run(() => MetadataService.RemoveMetadata(path));
            item.NotifySizeChanged();
            StatusText = $"Метаданные удалены: {item.Name}";
            LoadDetails(item);
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RenameAsync()
    {
        if (string.IsNullOrWhiteSpace(RenamePattern))
        {
            StatusText = "Укажите шаблон переименования.";
            return;
        }

        var itemsByPath = Files
            .Where(f => File.Exists(f.Path))
            .ToDictionary(f => f.Path, f => f, StringComparer.OrdinalIgnoreCase);

        if (itemsByPath.Count == 0)
        {
            StatusText = "Нет доступных файлов для переименования.";
            return;
        }

        IsBusy = true;
        StatusText = "Переименование…";

        try
        {
            string[] paths = itemsByPath.Keys.ToArray();
            IReadOnlyList<RenameResult> results = await Task.Run(
                () => MetadataService.RenameByDate(paths, RenamePattern));

            int renamed = 0;
            string? firstError = null;

            foreach (RenameResult result in results)
            {
                if (result.Success && result.TargetPath is not null)
                {
                    if (itemsByPath.TryGetValue(result.SourcePath, out FileItem? item))
                    {
                        item.Path = result.TargetPath;
                    }

                    renamed++;
                }
                else if (result.Error is not null &&
                         !result.Error.StartsWith("Имя уже", StringComparison.CurrentCultureIgnoreCase))
                {
                    firstError ??= result.Error;
                }
            }

            StatusText = firstError is null
                ? $"Переименовано файлов: {renamed}."
                : $"Переименовано: {renamed}. Ошибка: {firstError}";

            if (SelectedFile is not null)
            {
                LoadDetails(SelectedFile);
            }
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearSummary()
    {
        HasDetails = false;
        SummaryDimensions = "—";
        SummaryFormat = "—";
        SummarySize = "—";
        SummaryDate = "—";
        SummaryHasMetadata = "—";
    }
}
