using System.Windows.Input;
using ImgTool.App.Infrastructure;

namespace ImgTool.App.ViewModels;

public enum AppSection
{
    Batch,
    Collage,
    Metadata,

    // Новые значения добавлены в конец, чтобы не менять номера в сохранённых настройках.
    Gif,
    Video
}

/// <summary>Корневая ViewModel приложения.</summary>
public sealed class MainViewModel : BindableBase
{
    private AppSection _activeSection = AppSection.Batch;

    public MainViewModel()
    {
        SelectSectionCommand = new RelayCommand(parameter =>
        {
            if (parameter is string text && Enum.TryParse(text, true, out AppSection section))
            {
                ActiveSection = section;
            }
        });
    }

    public BatchViewModel Batch { get; } = new();

    public GifViewModel Gif { get; } = new();

    public VideoViewModel Video { get; } = new();

    public CollageViewModel Collage { get; } = new();

    public MetadataViewModel Metadata { get; } = new();

    public ICommand SelectSectionCommand { get; }

    public AppSection ActiveSection
    {
        get => _activeSection;
        set
        {
            if (!Set(ref _activeSection, value))
            {
                return;
            }

            RaisePropertyChanged(
                nameof(IsBatchSection),
                nameof(IsGifSection),
                nameof(IsVideoSection),
                nameof(IsCollageSection),
                nameof(IsMetadataSection),
                nameof(SectionTitle),
                nameof(SectionSubtitle));
        }
    }

    public bool IsBatchSection => ActiveSection == AppSection.Batch;

    public bool IsGifSection => ActiveSection == AppSection.Gif;

    public bool IsVideoSection => ActiveSection == AppSection.Video;

    public bool IsCollageSection => ActiveSection == AppSection.Collage;

    public bool IsMetadataSection => ActiveSection == AppSection.Metadata;

    public string SectionTitle => ActiveSection switch
    {
        AppSection.Gif => "GIF → кадры",
        AppSection.Video => "MP4 → кадры",
        AppSection.Collage => "Коллаж",
        AppSection.Metadata => "Метаданные",
        _ => "Пакетная обработка"
    };

    public string SectionSubtitle => ActiveSection switch
    {
        AppSection.Gif => "Извлеките все кадры анимированного GIF в отдельные изображения",
        AppSection.Video => "Извлеките кадры видео в PNG или другой формат",
        AppSection.Collage => "Соберите несколько изображений в единую сетку",
        AppSection.Metadata => "EXIF, очистка и переименование по дате съёмки",
        _ => "Ресайз, обрезка, поворот, конвертация и водяные знаки"
    };

    public string VersionText => "imgTool 1.0 · SkiaSharp";

    public void Shutdown()
    {
        Batch.Shutdown();
        Gif.Shutdown();
        Video.Shutdown();
        Collage.Shutdown();
        Metadata.Shutdown();
    }
}
