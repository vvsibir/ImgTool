using System.IO;
using System.Windows.Media.Imaging;
using ImgTool.Core.Services;
using SkiaSharp;

namespace ImgTool.App.Infrastructure;

/// <summary>Создание превью для WPF. Использует Skia, поэтому понимает WebP.</summary>
public static class BitmapHelper
{
    public static BitmapSource? LoadPreview(string path, int maxSize = 1000)
    {
        try
        {
            using SKBitmap bitmap = SkiaImage.LoadOriented(path);
            int width = bitmap.Width;
            int height = bitmap.Height;

            if (width > maxSize || height > maxSize)
            {
                double scale = Math.Min(maxSize / (double)width, maxSize / (double)height);
                int targetWidth = Math.Max(1, (int)Math.Round(width * scale));
                int targetHeight = Math.Max(1, (int)Math.Round(height * scale));

                using SKBitmap resized = SkiaImage.Resize(bitmap, targetWidth, targetHeight);
                return ToBitmapSource(resized);
            }

            return ToBitmapSource(bitmap);
        }
        catch
        {
            return null;
        }
    }

    public static BitmapSource? LoadFull(string path)
    {
        try
        {
            using SKBitmap bitmap = SkiaImage.LoadOriented(path);
            return ToBitmapSource(bitmap);
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource ToBitmapSource(SKBitmap bitmap)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());

        var result = new BitmapImage();
        result.BeginInit();
        result.CacheOption = BitmapCacheOption.OnLoad;
        result.StreamSource = stream;
        result.EndInit();
        result.Freeze();
        return result;
    }
}
