using SkiaSharp;

namespace ImgTool.Core.Services;

/// <summary>Разбор цветов в формате HEX (#RGB, #RRGGBB, #AARRGGBB).</summary>
public static class ColorParser
{
    public static readonly SKColor Default = SKColors.White;

    public static SKColor Parse(string? hex, SKColor? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback ?? Default;
        }

        return SKColor.TryParse(hex.Trim(), out SKColor color) ? color : fallback ?? Default;
    }

    public static SKColor WithOpacity(string? hex, float opacity, SKColor? fallback = null)
    {
        SKColor color = Parse(hex, fallback);
        return color.WithAlpha((byte)Math.Round(Math.Clamp(opacity, 0f, 1f) * 255));
    }

    public static bool IsValid(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        return SKColor.TryParse(hex.Trim(), out _);
    }

    /// <summary>Преобразует цвет в строку #RRGGBB для интерфейса.</summary>
    public static string ToHex(SKColor color) =>
        $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
}
