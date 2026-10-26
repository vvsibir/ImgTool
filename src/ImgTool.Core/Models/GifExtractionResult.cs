namespace ImgTool.Core.Models;

/// <summary>Сведения об анимированном GIF.</summary>
public readonly record struct GifSourceInfo(int Width, int Height, int FrameCount);

/// <summary>Прогресс извлечения кадров одного GIF.</summary>
public readonly record struct GifFrameProgress(int Completed, int Total);

/// <summary>Результат сохранения одного кадра.</summary>
public sealed class GifFrameResult
{
    /// <summary>Номер кадра, начиная с 1.</summary>
    public int FrameIndex { get; init; }

    /// <summary>Длительность кадра в миллисекундах.</summary>
    public int Duration { get; init; }

    /// <summary>Путь сохранённого файла. Для пропущенного кадра — <c>null</c>.</summary>
    public string? OutputPath { get; init; }

    /// <summary>Кадр успешно сохранён.</summary>
    public bool Success { get; init; }

    /// <summary>Сохранение пропущено из-за существующего файла.</summary>
    public bool Skipped { get; init; }

    /// <summary>Описание ошибки, если кадр не удалось сохранить.</summary>
    public string? Error { get; init; }
}

/// <summary>Результат извлечения всех кадров одного GIF.</summary>
public sealed class GifExtractionResult
{
    public string SourcePath { get; init; } = string.Empty;

    public int Width { get; init; }

    public int Height { get; init; }

    public int FrameCount { get; init; }

    public IReadOnlyList<GifFrameResult> Frames { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];

    public int SavedCount => Frames.Count(frame => frame.Success);

    public int SkippedCount => Frames.Count(frame => frame.Skipped);

    public int FailedCount => Errors.Count;

    public bool Success => Errors.Count == 0 && SavedCount > 0;
}
