using ImgTool.Core.Models;
using SkiaSharp;

namespace ImgTool.Core.Services;

/// <summary>
/// Обёртка над SkiaSharp: загрузка с учётом EXIF-ориентации, геометрические
/// преобразования, изменение размера и сохранение в поддерживаемые форматы.
/// </summary>
public static class SkiaImage
{
    /// <summary>Качественная кубическая интерполяция (Mitchell).</summary>
    public static readonly SKSamplingOptions HighQualitySampling = new(SKCubicResampler.Mitchell);

    private static SKImageInfo CreateInfo(int width, int height) =>
        new(Math.Max(1, width), Math.Max(1, height), SKColorType.Bgra8888, SKAlphaType.Premul);

    /// <summary>Создаёт пустое прозрачное изображение.</summary>
    public static SKBitmap Create(int width, int height)
    {
        var bitmap = new SKBitmap(CreateInfo(width, height));
        bitmap.Erase(SKColors.Transparent);
        return bitmap;
    }

    /// <summary>
    /// Загружает изображение и применяет EXIF-ориентацию. Возвращённый битмап
    /// всегда имеет тип Bgra8888/Premul и должен быть освобождён вызывающим кодом.
    /// </summary>
    public static SKBitmap LoadOriented(string path)
    {
        using SKData data = SKData.Create(path)
            ?? throw new IOException($"Не удалось прочитать файл «{path}».");

        using SKCodec codec = SKCodec.Create(data)
            ?? throw new InvalidDataException($"Не удалось распознать изображение «{path}».");

        SKImageInfo info = codec.Info;
        var bitmap = new SKBitmap(CreateInfo(info.Width, info.Height));

        try
        {
            SKCodecResult result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            {
                throw new InvalidDataException(
                    $"Не удалось декодировать изображение «{path}» ({result}).");
            }

            return ApplyOrigin(bitmap, codec.EncodedOrigin);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>Загружает изображение без применения EXIF-ориентации.</summary>
    public static SKBitmap LoadRaw(string path)
    {
        using SKData data = SKData.Create(path)
            ?? throw new IOException($"Не удалось прочитать файл «{path}».");

        using SKCodec codec = SKCodec.Create(data)
            ?? throw new InvalidDataException($"Не удалось распознать изображение «{path}».");

        var bitmap = new SKBitmap(CreateInfo(codec.Info.Width, codec.Info.Height));
        try
        {
            SKCodecResult result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            {
                throw new InvalidDataException(
                    $"Не удалось декодировать изображение «{path}» ({result}).");
            }

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static SKBitmap ApplyOrigin(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
        {
            return source;
        }

        SKBitmap? result = origin switch
        {
            SKEncodedOrigin.TopRight => FlipHorizontal(source),
            SKEncodedOrigin.BottomRight => Rotate180(source),
            SKEncodedOrigin.BottomLeft => FlipVertical(source),
            SKEncodedOrigin.LeftTop => FlipHorizontal(Rotate90(source)),
            SKEncodedOrigin.RightTop => FlipHorizontal(Rotate270(source)),
            SKEncodedOrigin.RightBottom => Rotate270(source),
            _ => null
        };

        if (result is null)
        {
            return source;
        }

        source.Dispose();
        return result;
    }

    /// <summary>Поворот на 90, 180 или 270 градусов. Исходный битмап не изменяется.</summary>
    public static SKBitmap Rotate(SKBitmap source, int degrees)
    {
        int normalized = ((degrees % 360) + 360) % 360;
        return normalized switch
        {
            90 => Rotate90(source),
            180 => Rotate180(source),
            270 => Rotate270(source),
            _ => source.Copy()
        };
    }

    public static SKBitmap Rotate90(SKBitmap source)
    {
        int width = source.Height;
        int height = source.Width;
        SKBitmap target = Create(width, height);

        try
        {
            using var canvas = new SKCanvas(target);
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(width, 0);
            canvas.RotateDegrees(90);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(source, 0, 0, paint);
            return target;
        }
        catch
        {
            target.Dispose();
            throw;
        }
    }

    public static SKBitmap Rotate180(SKBitmap source)
    {
        SKBitmap target = Create(source.Width, source.Height);

        try
        {
            using var canvas = new SKCanvas(target);
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(source.Width, source.Height);
            canvas.RotateDegrees(180);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(source, 0, 0, paint);
            return target;
        }
        catch
        {
            target.Dispose();
            throw;
        }
    }

    public static SKBitmap Rotate270(SKBitmap source)
    {
        int width = source.Height;
        int height = source.Width;
        SKBitmap target = Create(width, height);

        try
        {
            using var canvas = new SKCanvas(target);
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(0, height);
            canvas.RotateDegrees(-90);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(source, 0, 0, paint);
            return target;
        }
        catch
        {
            target.Dispose();
            throw;
        }
    }

    public static SKBitmap FlipHorizontal(SKBitmap source)
    {
        SKBitmap target = Create(source.Width, source.Height);

        try
        {
            using var canvas = new SKCanvas(target);
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(source.Width, 0);
            canvas.Scale(-1, 1);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(source, 0, 0, paint);
            return target;
        }
        catch
        {
            target.Dispose();
            throw;
        }
    }

    public static SKBitmap FlipVertical(SKBitmap source)
    {
        SKBitmap target = Create(source.Width, source.Height);

        try
        {
            using var canvas = new SKCanvas(target);
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(0, source.Height);
            canvas.Scale(1, -1);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(source, 0, 0, paint);
            return target;
        }
        catch
        {
            target.Dispose();
            throw;
        }
    }

    /// <summary>Обрезка прямоугольником. Исходный битмап не изменяется.</summary>
    public static SKBitmap Crop(SKBitmap source, SKRectI rectangle)
    {
        rectangle.Intersect(new SKRectI(0, 0, source.Width, source.Height));
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            throw new ArgumentException("Область обрезки выходит за границы изображения.", nameof(rectangle));
        }

        if (rectangle.Width == source.Width && rectangle.Height == source.Height)
        {
            return source.Copy();
        }

        SKBitmap target = Create(rectangle.Width, rectangle.Height);
        try
        {
            using var canvas = new SKCanvas(target);
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(
                source,
                new SKRect(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom),
                new SKRect(0, 0, rectangle.Width, rectangle.Height),
                paint);
            return target;
        }
        catch
        {
            target.Dispose();
            throw;
        }
    }

    /// <summary>Изменение размера. Исходный битмап не изменяется.</summary>
    public static SKBitmap Resize(SKBitmap source, int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        if (width == source.Width && height == source.Height)
        {
            return source.Copy();
        }

        SKBitmap? resized = source.Resize(new SKSizeI(width, height), HighQualitySampling);
        if (resized is null)
        {
            throw new InvalidOperationException("Не удалось изменить размер изображения.");
        }

        // Приводим результат к единому формату пикселей.
        if (resized.ColorType == SKColorType.Bgra8888 && resized.AlphaType == SKAlphaType.Premul)
        {
            return resized;
        }

        SKBitmap target = Create(width, height);
        try
        {
            using var canvas = new SKCanvas(target);
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(resized, 0, 0, paint);
            return target;
        }
        finally
        {
            resized.Dispose();
        }
    }

    /// <summary>Накладывает изображение на непрозрачный фон (нужно для JPEG и BMP).</summary>
    public static SKBitmap Flatten(SKBitmap source, SKColor background)
    {
        SKBitmap target = new(new SKImageInfo(
            Math.Max(1, source.Width),
            Math.Max(1, source.Height),
            SKColorType.Bgra8888,
            SKAlphaType.Opaque));

        try
        {
            target.Erase(background);
            using var canvas = new SKCanvas(target);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(source, 0, 0, paint);
            return target;
        }
        catch
        {
            target.Dispose();
            throw;
        }
    }

    /// <summary>Сохранение в формате, определяемом расширением файла.</summary>
    public static void SaveByExtension(SKBitmap bitmap, string path, int quality = 92)
    {
        ImageFormatKind format = ImageFormatHelper.FromExtension(path);
        if (format == ImageFormatKind.Keep)
        {
            throw new NotSupportedException(
                $"Формат «{Path.GetExtension(path)}» не поддерживается для сохранения.");
        }

        ImageFormatHelper.Save(bitmap, path, format, quality);
    }

    /// <summary>Сохраняет JPEG/WebP/PNG/BMP. BMP кодируется встроенным writer-ом (24 бита).</summary>
    public static void Save(SKBitmap bitmap, string path, ImageFormatKind format, int quality = 90)
    {
        if (format == ImageFormatKind.Bmp)
        {
            SaveBmp(bitmap, path);
            return;
        }

        int q = Math.Clamp(quality, 1, 100);
        SKEncodedImageFormat encoded = format switch
        {
            ImageFormatKind.Jpeg => SKEncodedImageFormat.Jpeg,
            ImageFormatKind.WebP => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Png
        };

        // JPEG не поддерживает прозрачность — подкладываем белый фон.
        SKBitmap? flattened = format == ImageFormatKind.Jpeg ? Flatten(bitmap, SKColors.White) : null;
        SKBitmap source = flattened ?? bitmap;

        try
        {
            using SKImage image = SKImage.FromBitmap(source);
            using SKData data = image.Encode(encoded, q)
                ?? throw new InvalidOperationException($"Не удалось закодировать изображение в {format}.");

            using FileStream stream = File.Create(path);
            data.SaveTo(stream);
        }
        finally
        {
            flattened?.Dispose();
        }
    }

    /// <summary>Записывает 24-битный BMP (BITMAPINFOHEADER, строки снизу вверх).</summary>
    public static void SaveBmp(SKBitmap bitmap, string path)
    {
        using SKBitmap flat = Flatten(bitmap, SKColors.White);
        int width = flat.Width;
        int height = flat.Height;
        int rowSize = width * 3;
        int paddedRowSize = (rowSize + 3) & ~3;
        int imageSize = paddedRowSize * height;
        const int headerSize = 54;
        int fileSize = headerSize + imageSize;

        if (fileSize < 0)
        {
            throw new InvalidOperationException("Изображение слишком велико для формата BMP.");
        }

        var pixels = flat.GetPixelSpan();
        int sourceStride = flat.RowBytes;
        byte[] row = new byte[paddedRowSize];

        using FileStream stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0);
        writer.Write(headerSize);

        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(imageSize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        for (int y = height - 1; y >= 0; y--)
        {
            ReadOnlySpan<byte> sourceRow = pixels.Slice(y * sourceStride, width * 4);
            for (int x = 0; x < width; x++)
            {
                int sourceIndex = x * 4;
                int targetIndex = x * 3;
                row[targetIndex] = sourceRow[sourceIndex];         // B
                row[targetIndex + 1] = sourceRow[sourceIndex + 1]; // G
                row[targetIndex + 2] = sourceRow[sourceIndex + 2]; // R
            }

            if (paddedRowSize > rowSize)
            {
                Array.Clear(row, rowSize, paddedRowSize - rowSize);
            }

            writer.Write(row);
        }
    }

    /// <summary>Возвращает размеры с учётом EXIF-ориентации.</summary>
    public static (int Width, int Height) ReadOrientedSize(string path)
    {
        using SKData data = SKData.Create(path)
            ?? throw new IOException($"Не удалось прочитать файл «{path}».");
        using SKCodec codec = SKCodec.Create(data)
            ?? throw new InvalidDataException($"Не удалось распознать изображение «{path}».");

        SKImageInfo info = codec.Info;
        return IsQuarterTurn(codec.EncodedOrigin)
            ? (info.Height, info.Width)
            : (info.Width, info.Height);
    }

    private static bool IsQuarterTurn(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
}
