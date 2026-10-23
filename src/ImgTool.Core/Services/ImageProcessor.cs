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
            ApplyChromaKey(bitmap, options);
            bitmap = ApplyResize(bitmap, options);

            if (options.Watermark.Enabled)
            {
                WatermarkPainter.Apply(bitmap, options.Watermark);
            }

            ImageFormatKind format = ImageFormatHelper.Resolve(options.Format, sourcePath);
            if (options.ChromaKey.Enabled && format is ImageFormatKind.Jpeg or ImageFormatKind.Bmp)
            {
                SKColor background = ColorParser.Parse(
                    options.ChromaKey.BackgroundHex,
                    SKColors.White);
                bitmap = Replace(bitmap, SkiaImage.Flatten(bitmap, background));
            }

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

        SKRectI crop = CalculateCrop(bitmap.Width, bitmap.Height, options);

        if (crop.Width == bitmap.Width && crop.Height == bitmap.Height)
        {
            return bitmap;
        }

        return Replace(bitmap, SkiaImage.Crop(bitmap, crop));
    }

    private static void ApplyChromaKey(SKBitmap bitmap, ProcessingOptions options)
    {
        if (!options.ChromaKey.Enabled)
        {
            return;
        }

        ChromaKeyRemover.Apply(bitmap, options.ChromaKey);
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

    /// <summary>Вычисляет прямоугольник обрезки по параметрам обработки.</summary>
    public static SKRectI CalculateCrop(int width, int height, ProcessingOptions options)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        return options.CropMode switch
        {
            CropMode.Margins => CalculateMarginsCrop(width, height, options),
            CropMode.Rectangle => CalculateRectangleCrop(width, height, options),
            _ => CalculateCrop(
                width,
                height,
                options.CropAspectWidth,
                options.CropAspectHeight,
                options.CropAnchor)
        };
    }

    private static SKRectI CalculateMarginsCrop(int width, int height, ProcessingOptions options)
    {
        double left = options.CropMarginLeft;
        double top = options.CropMarginTop;
        double right = options.CropMarginRight;
        double bottom = options.CropMarginBottom;

        if (left < 0 || top < 0 || right < 0 || bottom < 0)
        {
            throw new InvalidOperationException("Поля обрезки не могут быть отрицательными.");
        }

        if (options.CropUnit == CropUnit.Percent &&
            (left + right >= 100 || top + bottom >= 100))
        {
            throw new InvalidOperationException("Сумма полей обрезки должна быть меньше 100%.");
        }

        int x = (int)Math.Round(ToPixels(left, width, options.CropUnit));
        int y = (int)Math.Round(ToPixels(top, height, options.CropUnit));
        int rightEdge = width - (int)Math.Round(ToPixels(right, width, options.CropUnit));
        int bottomEdge = height - (int)Math.Round(ToPixels(bottom, height, options.CropUnit));

        if (rightEdge <= x || bottomEdge <= y)
        {
            throw new InvalidOperationException("Поля обрезки больше изображения.");
        }

        return new SKRectI(x, y, rightEdge, bottomEdge);
    }

    private static SKRectI CalculateRectangleCrop(int width, int height, ProcessingOptions options)
    {
        double x = options.CropRectangleX;
        double y = options.CropRectangleY;
        double rectangleWidth = options.CropRectangleWidth;
        double rectangleHeight = options.CropRectangleHeight;

        if (x < 0 || y < 0)
        {
            throw new InvalidOperationException("Координаты обрезки не могут быть отрицательными.");
        }

        if (rectangleWidth <= 0 || rectangleHeight <= 0)
        {
            throw new InvalidOperationException("Ширина и высота области обрезки должны быть больше нуля.");
        }

        if (options.CropUnit == CropUnit.Percent &&
            (x + rectangleWidth > 100.000001 || y + rectangleHeight > 100.000001))
        {
            throw new InvalidOperationException("Область обрезки выходит за пределы 100%.");
        }

        int left = (int)Math.Round(ToPixels(x, width, options.CropUnit));
        int top = (int)Math.Round(ToPixels(y, height, options.CropUnit));
        int right = (int)Math.Round(ToPixels(x + rectangleWidth, width, options.CropUnit));
        int bottom = (int)Math.Round(ToPixels(y + rectangleHeight, height, options.CropUnit));

        if (left < 0 || top < 0 || right > width || bottom > height)
        {
            throw new InvalidOperationException("Область обрезки выходит за пределы изображения.");
        }

        if (right <= left || bottom <= top)
        {
            throw new InvalidOperationException("Область обрезки пуста.");
        }

        return new SKRectI(left, top, right, bottom);
    }

    private static double ToPixels(double value, int dimension, CropUnit unit) =>
        unit == CropUnit.Percent ? dimension * value / 100.0 : value;

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
