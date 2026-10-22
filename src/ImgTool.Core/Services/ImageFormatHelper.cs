using ImgTool.Core.Models;
using SkiaSharp;

namespace ImgTool.Core.Services;

/// <summary>Работа с поддерживаемыми форматами изображений.</summary>
public static class ImageFormatHelper
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".webp"
    };

    public static IReadOnlyCollection<string> Extensions => SupportedExtensions;

    public static bool IsSupportedSource(string path) => SupportedExtensions.Contains(Path.GetExtension(path));

    public static ImageFormatKind FromExtension(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => ImageFormatKind.Jpeg,
        ".png" => ImageFormatKind.Png,
        ".webp" => ImageFormatKind.WebP,
        ".bmp" => ImageFormatKind.Bmp,
        _ => ImageFormatKind.Keep
    };

    public static ImageFormatKind Resolve(ImageFormatKind requested, string sourcePath)
    {
        if (requested != ImageFormatKind.Keep)
        {
            return requested;
        }

        ImageFormatKind detected = FromExtension(sourcePath);
        return detected == ImageFormatKind.Keep ? ImageFormatKind.Png : detected;
    }

    public static string GetExtension(ImageFormatKind format) => format switch
    {
        ImageFormatKind.Jpeg => ".jpg",
        ImageFormatKind.Png => ".png",
        ImageFormatKind.WebP => ".webp",
        ImageFormatKind.Bmp => ".bmp",
        _ => ".png"
    };

    public static string GetFormatName(ImageFormatKind format) => format switch
    {
        ImageFormatKind.Jpeg => "JPEG",
        ImageFormatKind.Png => "PNG",
        ImageFormatKind.WebP => "WebP",
        ImageFormatKind.Bmp => "BMP",
        _ => "PNG"
    };

    public static bool SupportsQuality(ImageFormatKind format) =>
        format is ImageFormatKind.Jpeg or ImageFormatKind.WebP;

    public static void Save(SKBitmap bitmap, string path, ImageFormatKind format, int quality = 90) =>
        SkiaImage.Save(bitmap, path, format, quality);

    public static void SaveByExtension(SKBitmap bitmap, string path, int quality = 92) =>
        SkiaImage.SaveByExtension(bitmap, path, quality);

    public static string DescribeFormat(string path)
    {
        try
        {
            using SKData data = SKData.Create(path);
            using SKCodec? codec = data is null ? null : SKCodec.Create(data);
            if (codec is not null)
            {
                return codec.EncodedFormat switch
                {
                    SKEncodedImageFormat.Jpeg => "JPEG",
                    SKEncodedImageFormat.Png => "PNG",
                    SKEncodedImageFormat.Webp => "WebP",
                    SKEncodedImageFormat.Bmp => "BMP",
                    SKEncodedImageFormat.Gif => "GIF",
                    SKEncodedImageFormat.Ico => "ICO",
                    _ => codec.EncodedFormat.ToString().ToUpperInvariant()
                };
            }
        }
        catch
        {
            // Ниже используем расширение файла.
        }

        string extension = Path.GetExtension(path).TrimStart('.');
        return string.IsNullOrEmpty(extension) ? "?" : extension.ToUpperInvariant();
    }

    /// <summary>Расширение файла для заданного формата (с точкой).</summary>
    public static string NormalizeExtension(string extension) =>
        extension.StartsWith('.') ? extension : "." + extension;
}
