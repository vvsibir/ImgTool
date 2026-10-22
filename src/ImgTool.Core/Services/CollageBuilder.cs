using ImgTool.Core.Models;
using SkiaSharp;

namespace ImgTool.Core.Services;

/// <summary>Сборка коллажа из нескольких изображений.</summary>
public sealed class CollageBuilder
{
    public async Task<CollageResult> BuildAsync(
        IReadOnlyList<string> sourcePaths,
        CollageOptions options,
        string outputPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(
            () => Build(sourcePaths, options, outputPath, progress, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    public CollageResult Build(
        IReadOnlyList<string> sourcePaths,
        CollageOptions options,
        string outputPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (sourcePaths.Count == 0)
        {
            throw new InvalidOperationException("Добавьте хотя бы одно изображение.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("Не указан путь для сохранения коллажа.");
        }

        const int MaxDimension = 20_000;
        int spacing = Math.Clamp(options.Spacing, 0, 400);
        int padding = Math.Clamp(options.Padding, 0, 400);

        int columns = 0;
        int cellWidth = 0;
        int cellHeight = 0;
        int width;
        int height;
        List<(int Width, int Height)>? stripSizes = null;
        int stripCrossSize = 0;

        if (options.Layout == CollageLayout.Grid)
        {
            columns = Math.Clamp(options.Columns, 1, Math.Min(12, sourcePaths.Count));
            double cellAspect = Math.Clamp(options.CellAspect, 0.2, 5.0);
            int rows = (int)Math.Ceiling(sourcePaths.Count / (double)columns);

            if (options.Width is int requestedWidth)
            {
                width = Math.Clamp(requestedWidth, 200, MaxDimension);
                cellWidth = (width - (padding * 2) - (spacing * (columns - 1))) / columns;
                if (cellWidth < 16)
                {
                    throw new InvalidOperationException(
                        "Слишком маленькая ширина или слишком большие отступы для выбранного числа колонок.");
                }
            }
            else
            {
                // Ширина не задана: размер ячейки равен ширине первого изображения,
                // а итоговая ширина зависит от числа колонок.
                (int firstWidth, _) = SkiaImage.ReadOrientedSize(sourcePaths[0]);
                int available = MaxDimension - (padding * 2) - (spacing * (columns - 1));
                int maxCellWidth = Math.Max(16, available / columns);
                cellWidth = Math.Clamp(firstWidth, 16, maxCellWidth);
                width = (padding * 2) + (columns * cellWidth) + (spacing * (columns - 1));
            }

            cellHeight = Math.Max(16, (int)Math.Round(cellWidth / cellAspect));
            height = (padding * 2) + (rows * cellHeight) + (spacing * (rows - 1));
        }
        else
        {
            // Склейка «как есть»: изображения не масштабируются. Поперечный размер
            // полосы равен максимальному размеру кадра, продольный — сумме размеров.
            bool vertical = options.Layout == CollageLayout.VerticalStrip;
            stripSizes = new List<(int Width, int Height)>(sourcePaths.Count);
            int totalLength = 0;

            foreach (string path in sourcePaths)
            {
                (int sourceWidth, int sourceHeight) = SkiaImage.ReadOrientedSize(path);
                stripSizes.Add((sourceWidth, sourceHeight));
                stripCrossSize = Math.Max(stripCrossSize, vertical ? sourceWidth : sourceHeight);
                totalLength += vertical ? sourceHeight : sourceWidth;
            }

            int crossSize = stripCrossSize + (padding * 2);
            int length = totalLength + (padding * 2) + (spacing * (sourcePaths.Count - 1));
            width = vertical ? crossSize : length;
            height = vertical ? length : crossSize;

            if (width > MaxDimension || height > MaxDimension)
            {
                throw new InvalidOperationException(
                    $"Результат {width}×{height} px превышает допустимые {MaxDimension} px. " +
                    "Уменьшите число изображений, отступы или поля.");
            }
        }

        SKColor background = options.TransparentBackground
            ? SKColors.Transparent
            : ColorParser.Parse(options.BackgroundHex, new SKColor(0x12, 0x16, 0x1F));
        using var collage = new SKBitmap(new SKImageInfo(
            width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        collage.Erase(background);

        using (var canvas = new SKCanvas(collage))
        using (var paint = new SKPaint { IsAntialias = true })
        {
            if (stripSizes is not null)
            {
                bool vertical = options.Layout == CollageLayout.VerticalStrip;
                int x = padding;
                int y = padding;

                for (int index = 0; index < sourcePaths.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using SKBitmap source = SkiaImage.LoadOriented(sourcePaths[index]);
                    int left = vertical
                        ? padding + ((stripCrossSize - source.Width) / 2)
                        : x;
                    int top = vertical
                        ? y
                        : padding + ((stripCrossSize - source.Height) / 2);

                    using SKImage image = SKImage.FromBitmap(source);
                    canvas.DrawImage(image, left, top, paint);

                    if (vertical)
                    {
                        y += source.Height + spacing;
                    }
                    else
                    {
                        x += source.Width + spacing;
                    }

                    progress?.Report((index + 1) * 100.0 / sourcePaths.Count);
                }
            }
            else
            {
                for (int index = 0; index < sourcePaths.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string path = sourcePaths[index];
                    using SKBitmap source = SkiaImage.LoadOriented(path);

                    int column = index % columns;
                    int row = index / columns;
                    float x = padding + (column * (cellWidth + spacing));
                    float y = padding + (row * (cellHeight + spacing));
                    var destination = new SKRect(x, y, x + cellWidth, y + cellHeight);

                    SKRect sourceRect;
                    SKRect targetRect;

                    if (options.Fit == CollageFit.Cover)
                    {
                        double scale = Math.Max(
                            cellWidth / (double)source.Width,
                            cellHeight / (double)source.Height);
                        float sourceWidth = (float)(cellWidth / scale);
                        float sourceHeight = (float)(cellHeight / scale);
                        float sourceLeft = (source.Width - sourceWidth) / 2f;
                        float sourceTop = (source.Height - sourceHeight) / 2f;
                        sourceRect = new SKRect(
                            sourceLeft,
                            sourceTop,
                            sourceLeft + sourceWidth,
                            sourceTop + sourceHeight);
                        targetRect = destination;
                    }
                    else
                    {
                        double scale = Math.Min(
                            cellWidth / (double)source.Width,
                            cellHeight / (double)source.Height);
                        float drawWidth = Math.Max(1f, (float)(source.Width * scale));
                        float drawHeight = Math.Max(1f, (float)(source.Height * scale));
                        float drawX = x + ((cellWidth - drawWidth) / 2f);
                        float drawY = y + ((cellHeight - drawHeight) / 2f);
                        sourceRect = new SKRect(0, 0, source.Width, source.Height);
                        targetRect = new SKRect(drawX, drawY, drawX + drawWidth, drawY + drawHeight);
                    }

                    using SKImage image = SKImage.FromBitmap(source);
                    canvas.DrawImage(image, sourceRect, targetRect, SkiaImage.HighQualitySampling, paint);

                    progress?.Report((index + 1) * 100.0 / sourcePaths.Count);
                }
            }
        }

        string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        ImageFormatKind format = ImageFormatHelper.FromExtension(outputPath);
        if (format == ImageFormatKind.Keep)
        {
            format = options.Format == ImageFormatKind.Keep ? ImageFormatKind.Jpeg : options.Format;
        }

        ImageFormatHelper.Save(collage, outputPath, format, options.Quality);
        return new CollageResult(outputPath, width, height, sourcePaths.Count);
    }
}
