using ImgTool.Core.Models;
using SkiaSharp;

namespace ImgTool.Core.Services;

/// <summary>Удаление фона по ключевому цвету (хромакей).</summary>
public static class ChromaKeyRemover
{
    /// <summary>Масштаб допуска: 100% соответствуют расстоянию 255 в плоскости Cb/Cr.</summary>
    private const double ToleranceScale = 255.0;

    /// <summary>Применяет хромакей прямо к битмапу Bgra8888/Premul.</summary>
    public static void Apply(SKBitmap bitmap, ChromaKeyOptions options)
    {
        if (!options.Enabled || bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return;
        }

        if (bitmap.ColorType != SKColorType.Bgra8888)
        {
            throw new InvalidOperationException(
                "Хромакей поддерживает только изображения в формате Bgra8888.");
        }

        SKColor key = ColorParser.Parse(options.KeyColorHex, new SKColor(0, 255, 0));
        (double keyCb, double keyCr) = ToChroma(key.Red, key.Green, key.Blue);

        double tolerance = Math.Clamp(options.Tolerance, 0, 100) / 100.0 * ToleranceScale;
        double softness = Math.Clamp(options.Softness, 0, 100) / 100.0 * ToleranceScale;
        int keyChannel = GetDominantChannel(key);

        Span<byte> pixels = bitmap.GetPixelSpan();
        int rowBytes = bitmap.RowBytes;

        for (int y = 0; y < bitmap.Height; y++)
        {
            int row = y * rowBytes;
            for (int x = 0; x < bitmap.Width; x++)
            {
                int index = row + (x * 4);
                byte b = pixels[index];
                byte g = pixels[index + 1];
                byte r = pixels[index + 2];
                byte a = pixels[index + 3];

                if (a == 0)
                {
                    continue;
                }

                double red = Unpremultiply(r, a);
                double green = Unpremultiply(g, a);
                double blue = Unpremultiply(b, a);

                (double cb, double cr) = ToChroma(red, green, blue);
                double distance = Math.Sqrt(
                    ((cb - keyCb) * (cb - keyCb)) + ((cr - keyCr) * (cr - keyCr)));

                double alphaFactor;
                if (distance <= tolerance)
                {
                    alphaFactor = 0;
                }
                else if (softness > 0 && distance < tolerance + softness)
                {
                    alphaFactor = (distance - tolerance) / softness;
                }
                else
                {
                    alphaFactor = 1;
                }

                if (alphaFactor <= 0)
                {
                    ClearPixel(pixels, index);
                    continue;
                }

                double spillWidth = GetSpillWidth(softness, tolerance);
                if (options.Despill &&
                    keyChannel >= 0 &&
                    distance < tolerance + spillWidth)
                {
                    double strength = Math.Clamp(
                        1 - ((distance - tolerance) / spillWidth),
                        0,
                        1);

                    if (strength > 0)
                    {
                        double neutral = keyChannel switch
                        {
                            0 => Math.Max(green, blue),
                            1 => Math.Max(red, blue),
                            _ => Math.Max(red, green)
                        };

                        switch (keyChannel)
                        {
                            case 0:
                                if (red > neutral)
                                {
                                    red += (neutral - red) * strength;
                                }

                                break;
                            case 1:
                                if (green > neutral)
                                {
                                    green += (neutral - green) * strength;
                                }

                                break;
                            default:
                                if (blue > neutral)
                                {
                                    blue += (neutral - blue) * strength;
                                }

                                break;
                        }
                    }
                }

                byte newAlpha = (byte)Math.Clamp(
                    (int)Math.Round(a * alphaFactor),
                    0,
                    255);

                if (newAlpha == 0)
                {
                    ClearPixel(pixels, index);
                    continue;
                }

                pixels[index] = Premultiply(blue, newAlpha);
                pixels[index + 1] = Premultiply(green, newAlpha);
                pixels[index + 2] = Premultiply(red, newAlpha);
                pixels[index + 3] = newAlpha;
            }
        }
    }

    private static (double Cb, double Cr) ToChroma(double red, double green, double blue)
    {
        double cb = 128 - (0.168736 * red) - (0.331264 * green) + (0.5 * blue);
        double cr = 128 + (0.5 * red) - (0.418688 * green) - (0.081312 * blue);
        return (cb, cr);
    }

    private static double Unpremultiply(byte value, byte alpha) =>
        alpha == 255 ? value : Math.Min(255.0, value * 255.0 / alpha);

    private static byte Premultiply(double value, byte alpha) =>
        (byte)Math.Clamp((int)Math.Round(value * alpha / 255.0), 0, 255);

    private static void ClearPixel(Span<byte> pixels, int index)
    {
        pixels[index] = 0;
        pixels[index + 1] = 0;
        pixels[index + 2] = 0;
        pixels[index + 3] = 0;
    }

    private static int GetDominantChannel(SKColor color)
    {
        int max = Math.Max(color.Red, Math.Max(color.Green, color.Blue));
        int min = Math.Min(color.Red, Math.Min(color.Green, color.Blue));
        if (max - min < 24)
        {
            return -1;
        }

        if (color.Red == max)
        {
            return 0;
        }

        return color.Green == max ? 1 : 2;
    }

    private static double GetSpillWidth(double softness, double tolerance) =>
        softness > 0 ? softness * 2 : Math.Max(4.0, tolerance * 0.25);
}
