namespace ImgTool.Core.Models;

/// <summary>Параметры извлечения кадров из GIF.</summary>
public sealed class GifExtractionOptions
{
    /// <summary>Папка результата. Пустое значение — рядом с исходным GIF.</summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Шаблон имени кадра. Поддерживает {name}, {index}, {ext}, {format},
    /// {date}, {width} и {height}.
    /// </summary>
    public string FileNamePattern { get; set; } = "{name}_frame_{index}";

    /// <summary>Формат сохранения кадров.</summary>
    public ImageFormatKind Format { get; set; } = ImageFormatKind.Png;

    /// <summary>Качество для JPEG и WebP.</summary>
    public int Quality { get; set; } = 92;

    /// <summary>Поведение при совпадении имён.</summary>
    public CollisionPolicy CollisionPolicy { get; set; } = CollisionPolicy.Rename;
}
