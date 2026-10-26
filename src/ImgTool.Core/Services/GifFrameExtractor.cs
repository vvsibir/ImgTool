using ImgTool.Core.Models;
using SkiaSharp;

namespace ImgTool.Core.Services;

/// <summary>Извлечение кадров из анимированного GIF в отдельные изображения.</summary>
public static class GifFrameExtractor
{
    /// <summary>Поддерживается ли файл этим модулем.</summary>
    public static bool IsSupportedSource(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase);

    /// <summary>Читает размеры и число кадров без извлечения.</summary>
    public static GifSourceInfo ReadInfo(string sourcePath)
    {
        string path = ValidateSource(sourcePath);

        using SKData data = SKData.Create(path)
            ?? throw new IOException($"Не удалось прочитать файл «{path}».");
        using SKCodec codec = SKCodec.Create(data)
            ?? throw new InvalidDataException($"Не удалось распознать GIF «{path}».");

        return new GifSourceInfo(
            codec.Info.Width,
            codec.Info.Height,
            Math.Max(1, codec.FrameCount));
    }

    /// <summary>
    /// Извлекает все кадры GIF. Каждый кадр сохраняется отдельным файлом;
    /// ошибка одного кадра не прерывает извлечение остальных.
    /// </summary>
    public static GifExtractionResult Extract(
        string sourcePath,
        GifExtractionOptions options,
        IProgress<GifFrameProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        string path = ValidateSource(sourcePath);
        ImageFormatKind format = NormalizeFormat(options.Format);
        CollisionPolicy collisionPolicy = NormalizeCollisionPolicy(options.CollisionPolicy);
        int quality = Math.Clamp(options.Quality, 1, 100);

        using SKData data = SKData.Create(path)
            ?? throw new IOException($"Не удалось прочитать файл «{path}».");
        using SKCodec codec = SKCodec.Create(data)
            ?? throw new InvalidDataException($"Не удалось распознать GIF «{path}».");

        int width = codec.Info.Width;
        int height = codec.Info.Height;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException($"GIF «{path}» имеет некорректные размеры.");
        }

        int frameCount = Math.Max(1, codec.FrameCount);
        SKCodecFrameInfo[] frameInfos = codec.FrameInfo;
        var frames = new List<GifFrameResult>(frameCount);
        var errors = new List<string>();

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int frameNumber = frameIndex + 1;
            int duration = frameIndex < frameInfos.Length ? frameInfos[frameIndex].Duration : 0;
            string? outputPath;

            try
            {
                outputPath = OutputPathBuilder.Build(
                    path,
                    options.OutputDirectory,
                    options.FileNamePattern,
                    collisionPolicy,
                    frameNumber,
                    format,
                    width,
                    height);
            }
            catch (Exception ex)
            {
                string message = $"Кадр {frameNumber}: {ex.Message}";
                errors.Add(message);
                frames.Add(new GifFrameResult
                {
                    FrameIndex = frameNumber,
                    Duration = duration,
                    Success = false,
                    Error = message
                });
                progress?.Report(new GifFrameProgress(frameNumber, frameCount));
                continue;
            }

            if (outputPath is null)
            {
                frames.Add(new GifFrameResult
                {
                    FrameIndex = frameNumber,
                    Duration = duration,
                    Success = false,
                    Skipped = true
                });
                progress?.Report(new GifFrameProgress(frameNumber, frameCount));
                continue;
            }

            try
            {
                using var bitmap = new SKBitmap(new SKImageInfo(
                    width,
                    height,
                    SKColorType.Bgra8888,
                    SKAlphaType.Premul));

                // PriorFrame = -1 заставляет кодек самостоятельно декодировать
                // предыдущие кадры и учесть их способ отображения/очистки.
                var codecOptions = new SKCodecOptions(frameIndex, -1)
                {
                    ZeroInitialized = SKZeroInitialized.Yes
                };

                SKCodecResult decodeResult = codec.GetPixels(
                    bitmap.Info,
                    bitmap.GetPixels(),
                    codecOptions);

                if (decodeResult is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
                {
                    throw new InvalidDataException(
                        $"Не удалось декодировать кадр {frameNumber} ({decodeResult}).");
                }

                string? directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                SkiaImage.Save(bitmap, outputPath, format, quality);

                frames.Add(new GifFrameResult
                {
                    FrameIndex = frameNumber,
                    Duration = duration,
                    OutputPath = outputPath,
                    Success = true
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                string message = $"Кадр {frameNumber}: {ex.Message}";
                errors.Add(message);
                frames.Add(new GifFrameResult
                {
                    FrameIndex = frameNumber,
                    Duration = duration,
                    Success = false,
                    Error = message
                });
            }

            progress?.Report(new GifFrameProgress(frameNumber, frameCount));
        }

        return new GifExtractionResult
        {
            SourcePath = path,
            Width = width,
            Height = height,
            FrameCount = frameCount,
            Frames = frames,
            Errors = errors
        };
    }

    private static string ValidateSource(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("Путь к GIF не задан.", nameof(sourcePath));
        }

        string path = Path.GetFullPath(sourcePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Файл «{path}» не найден.", path);
        }

        if (!IsSupportedSource(path))
        {
            throw new NotSupportedException($"Файл «{path}» не является GIF.");
        }

        return path;
    }

    private static ImageFormatKind NormalizeFormat(ImageFormatKind format) =>
        format is ImageFormatKind.Jpeg or ImageFormatKind.Png
            or ImageFormatKind.WebP or ImageFormatKind.Bmp
            ? format
            : ImageFormatKind.Png;

    private static CollisionPolicy NormalizeCollisionPolicy(CollisionPolicy policy) =>
        Enum.IsDefined(policy) ? policy : CollisionPolicy.Rename;
}
