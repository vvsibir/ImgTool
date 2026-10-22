using ImgTool.Core.Models;
using SkiaSharp;

namespace ImgTool.Core.Services;

/// <summary>Пакетная обработка изображений.</summary>
public sealed class ImageProcessor
{
    /// <summary>Обрабатывает набор файлов. Ошибки отдельных файлов не прерывают пакет.</summary>
    public async Task<IReadOnlyList<ProcessingResult>> ProcessBatchAsync(
        IReadOnlyList<string> sourcePaths,
        ProcessingOptions options,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ProcessingResult>(sourcePaths.Count);

        for (int i = 0; i < sourcePaths.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string path = sourcePaths[i];
            string fileName = Path.GetFileName(path);
            progress?.Report(new BatchProgress(i, sourcePaths.Count, fileName, null));

            ProcessingResult result;
            try
            {
                result = await Task.Run(
                    () => ProcessFile(path, options, i + 1),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result = ProcessingResult.Fail(path, ex.Message);
            }

            results.Add(result);
            progress?.Report(new BatchProgress(i + 1, sourcePaths.Count, fileName, result));
        }

        return results;
    }

    /// <summary>Обрабатывает один файл.</summary>
    public ProcessingResult ProcessFile(string sourcePath, ProcessingOptions options, int index = 1)
    {
        if (!File.Exists(sourcePath))
        {
            return ProcessingResult.Fail(sourcePath, "Файл не найден.");
        }

        SKBitmap bitmap = SkiaImage.LoadOriented(sourcePath);

        try
        {
            bitmap = ApplyRotationAndFlip(bitmap, options);
            bitmap = ApplyCrop(bitmap, options);
            bitmap = ApplyResize(bitmap, options);

            if (options.Watermark.Enabled)
            {
                WatermarkPainter.Apply(bitmap, options.Watermark);
            }

            ImageFormatKind format = ImageFormatHelper.Resolve(options.Format, sourcePath);
            string? outputPath = OutputPathBuilder.Build(
                sourcePath,
                options,
                index,
                format,
                bitmap.Width,
                bitmap.Height);

            if (outputPath is null)
            {
                return ProcessingResult.Skip(sourcePath);
            }

            string? directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                ImageFormatHelper.Save(bitmap, outputPath, format, options.Quality);
            }
            catch
            {
                TryDelete(outputPath);
                throw;
            }

            if (options.PreserveMetadata && format == ImageFormatKind.Jpeg)
            {
                try
                {
                    JpegMetadataPreserver.Copy(sourcePath, outputPath);
                }
                catch
                {
                    // Метаданные не являются критичными: результат всё равно пригоден.
                }
            }

            return ProcessingResult.Ok(sourcePath, outputPath);
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    private static SKBitmap ApplyRotationAndFlip(SKBitmap bitmap, ProcessingOptions options)
    {
        int rotation = NormalizeRotation(options.RotationDegrees);
        if (rotation != 0)
        {
            bitmap = Replace(bitmap, SkiaImage.Rotate(bitmap, rotation));
        }

        if (options.FlipHorizontal)
        {
            bitmap = Replace(bitmap, SkiaImage.FlipHorizontal(bitmap));
        }

        if (options.FlipVertical)
        {
            bitmap = Replace(bitmap, SkiaImage.FlipVertical(bitmap));
        }

        return bitmap;
    }

    private static SKBitmap ApplyCrop(SKBitmap bitmap, ProcessingOptions options)
    {
        if (!options.CropEnabled)
        {
            return bitmap;
        }

        SKRectI crop = CalculateCrop(
            bitmap.Width,
            bitmap.Height,
            options.CropAspectWidth,
            options.CropAspectHeight,
            options.CropAnchor);

        if (crop.Width == bitmap.Width && crop.Height == bitmap.Height)
        {
            return bitmap;
        }

        return Replace(bitmap, SkiaImage.Crop(bitmap, crop));
    }

    private static SKBitmap ApplyResize(SKBitmap bitmap, ProcessingOptions options)
    {
        if (!options.ResizeEnabled)
        {
            return bitmap;
        }

        int sourceWidth = bitmap.Width;
        int sourceHeight = bitmap.Height;
        int targetWidth;
        int targetHeight;

        if (options.ResizeMode == ResizeMode.Percent)
        {
            double percent = Math.Clamp(options.ResizePercent, 1, 1000);
            if (options.DontUpscale)
            {
                percent = Math.Min(percent, 100);
            }

            targetWidth = Math.Max(1, (int)Math.Round(sourceWidth * percent / 100.0));
            targetHeight = Math.Max(1, (int)Math.Round(sourceHeight * percent / 100.0));
        }
        else
        {
            int boxWidth = Math.Max(0, options.ResizeWidth);
            int boxHeight = Math.Max(0, options.ResizeHeight);
            if (boxWidth == 0 && boxHeight == 0)
            {
                return bitmap;
            }

            bool cover = options.ResizeMode == ResizeMode.Cover;
            bool stretch = options.ResizeMode == ResizeMode.Stretch;

            if (stretch)
            {
                targetWidth = boxWidth > 0 ? boxWidth : sourceWidth;
                targetHeight = boxHeight > 0 ? boxHeight : sourceHeight;
                if (options.DontUpscale)
                {
                    targetWidth = Math.Min(targetWidth, sourceWidth);
                    targetHeight = Math.Min(targetHeight, sourceHeight);
                }
            }
            else
            {
                double scale;
                if (boxWidth == 0)
                {
                    scale = boxHeight / (double)sourceHeight;
                }
                else if (boxHeight == 0)
                {
                    scale = boxWidth / (double)sourceWidth;
                }
                else
                {
                    scale = cover
                        ? Math.Max(boxWidth / (double)sourceWidth, boxHeight / (double)sourceHeight)
                        : Math.Min(boxWidth / (double)sourceWidth, boxHeight / (double)sourceHeight);
                }

                if (options.DontUpscale)
                {
                    scale = Math.Min(scale, 1.0);
                }

                targetWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
                targetHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            }
        }

        if (targetWidth == sourceWidth && targetHeight == sourceHeight)
        {
            return bitmap;
        }

        bitmap = Replace(bitmap, SkiaImage.Resize(bitmap, targetWidth, targetHeight));

        if (options.ResizeMode == ResizeMode.Cover && options.ResizeWidth > 0 && options.ResizeHeight > 0)
        {
            int cropWidth = Math.Min(options.ResizeWidth, bitmap.Width);
            int cropHeight = Math.Min(options.ResizeHeight, bitmap.Height);
            SKRectI crop = CalculateCrop(
                bitmap.Width,
                bitmap.Height,
                cropWidth,
                cropHeight,
                options.CropAnchor);

            if (crop.Width != bitmap.Width || crop.Height != bitmap.Height)
            {
                bitmap = Replace(bitmap, SkiaImage.Crop(bitmap, crop));
            }
        }

        return bitmap;
    }

    /// <summary>Вычисляет прямоугольник обрезки под заданную пропорцию.</summary>
    public static SKRectI CalculateCrop(
        int width,
        int height,
        double aspectWidth,
        double aspectHeight,
        Anchor anchor)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        if (aspectWidth <= 0 || aspectHeight <= 0)
        {
            return new SKRectI(0, 0, width, height);
        }

        double targetAspect = aspectWidth / aspectHeight;
        double currentAspect = width / (double)height;

        int cropWidth;
        int cropHeight;
        if (currentAspect > targetAspect)
        {
            cropHeight = height;
            cropWidth = (int)Math.Round(height * targetAspect);
        }
        else
        {
            cropWidth = width;
            cropHeight = (int)Math.Round(width / targetAspect);
        }

        cropWidth = Math.Clamp(cropWidth, 1, width);
        cropHeight = Math.Clamp(cropHeight, 1, height);

        bool left = anchor is Anchor.TopLeft or Anchor.Left or Anchor.BottomLeft;
        bool right = anchor is Anchor.TopRight or Anchor.Right or Anchor.BottomRight;
        bool top = anchor is Anchor.TopLeft or Anchor.Top or Anchor.TopRight;
        bool bottom = anchor is Anchor.BottomLeft or Anchor.Bottom or Anchor.BottomRight;

        int x = left ? 0 : right ? width - cropWidth : (width - cropWidth) / 2;
        int y = top ? 0 : bottom ? height - cropHeight : (height - cropHeight) / 2;

        return new SKRectI(x, y, x + cropWidth, y + cropHeight);
    }

    private static SKBitmap Replace(SKBitmap original, SKBitmap replacement)
    {
        if (!ReferenceEquals(original, replacement))
        {
            original.Dispose();
        }

        return replacement;
    }

    private static int NormalizeRotation(int degrees)
    {
        int normalized = degrees % 360;
        if (normalized < 0)
        {
            normalized += 360;
        }

        return normalized switch
        {
            90 or 180 or 270 => normalized,
            _ => 0
        };
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Игнорируем: исходная ошибка важнее.
        }
    }
}
