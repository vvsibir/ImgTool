using ImgTool.Core.Models;
using SkiaSharp;

namespace ImgTool.Core.Services;

/// <summary>Отрисовка текстового или графического водяного знака.</summary>
public static class WatermarkPainter
{
    /// <summary>Рисует водяной знак прямо на битмапе.</summary>
    public static void Apply(SKBitmap target, WatermarkOptions options)
    {
        if (!options.Enabled)
        {
            return;
        }

        if (options.Kind == WatermarkKind.Text)
        {
            if (!string.IsNullOrWhiteSpace(options.Text))
            {
                DrawText(target, options);
            }

            return;
        }

        if (options.Kind == WatermarkKind.Logo &&
            !string.IsNullOrWhiteSpace(options.LogoPath) &&
            File.Exists(options.LogoPath))
        {
            DrawLogo(target, options);
        }
    }

    private static void DrawText(SKBitmap target, WatermarkOptions options)
    {
        string text = options.Text ?? string.Empty;
        float fontSize = Math.Clamp(options.FontSize, 6f, 1000f);
        float opacity = Math.Clamp(options.Opacity, 0f, 1f);
        if (opacity <= 0f)
        {
            return;
        }

        using SKTypeface typeface =
            SKTypeface.FromFamilyName(
                options.FontFamily,
                SKFontStyleWeight.Bold,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright)
            ?? SKTypeface.Default;
        using var font = new SKFont(typeface, fontSize);
        using var measurePaint = new SKPaint { IsAntialias = true };

        font.MeasureText(text, out SKRect bounds, measurePaint);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        SKColor color = ColorParser.WithOpacity(options.ColorHex, opacity);
        using var paint = new SKPaint { IsAntialias = true, Color = color };

        float padding = 4f;
        float width = bounds.Width + (padding * 2);
        float height = bounds.Height + (padding * 2);
        (float left, float top) = Place(
            options.Position,
            target.Width,
            target.Height,
            width,
            height,
            options.Margin);

        float x = left - bounds.Left;
        float y = top - bounds.Top;

        using var canvas = new SKCanvas(target);
        if (options.Shadow)
        {
            float offset = Math.Max(1f, fontSize * 0.045f);
            using var shadowPaint = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(0, 0, 0, (byte)(Math.Clamp(opacity, 0f, 1f) * 190))
            };
            canvas.DrawText(text, x + offset, y + offset, font, shadowPaint);
        }

        canvas.DrawText(text, x, y, font, paint);
    }

    private static void DrawLogo(SKBitmap target, WatermarkOptions options)
    {
        float opacity = Math.Clamp(options.Opacity, 0f, 1f);
        if (opacity <= 0f)
        {
            return;
        }

        using SKBitmap logo = SkiaImage.LoadOriented(options.LogoPath);
        int logoWidth = Math.Max(
            1,
            (int)MathF.Round(target.Width * Math.Clamp(options.LogoWidthPercent, 1f, 100f) / 100f));
        int logoHeight = Math.Max(1, (int)MathF.Round(logo.Height * (float)logoWidth / logo.Width));

        using SKBitmap resized = SkiaImage.Resize(logo, logoWidth, logoHeight);
        (float left, float top) = Place(
            options.Position,
            target.Width,
            target.Height,
            resized.Width,
            resized.Height,
            options.Margin);

        using var canvas = new SKCanvas(target);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White.WithAlpha((byte)Math.Round(opacity * 255))
        };
        canvas.DrawBitmap(resized, left, top, paint);
    }

    /// <summary>Вычисляет левый верхний угол для привязки Anchor.</summary>
    public static (float Left, float Top) Place(
        Anchor anchor,
        float canvasWidth,
        float canvasHeight,
        float width,
        float height,
        float marginValue)
    {
        float margin = Math.Max(0f, marginValue);
        float centerX = (canvasWidth - width) / 2f;
        float centerY = (canvasHeight - height) / 2f;
        float right = canvasWidth - width - margin;
        float bottom = canvasHeight - height - margin;

        (float, float) position = anchor switch
        {
            Anchor.TopLeft => (margin, margin),
            Anchor.Top => (centerX, margin),
            Anchor.TopRight => (right, margin),
            Anchor.Left => (margin, centerY),
            Anchor.Center => (centerX, centerY),
            Anchor.Right => (right, centerY),
            Anchor.BottomLeft => (margin, bottom),
            Anchor.Bottom => (centerX, bottom),
            Anchor.BottomRight => (right, bottom),
            _ => (right, bottom)
        };

        return (Math.Max(0f, position.Item1), Math.Max(0f, position.Item2));
    }
}
