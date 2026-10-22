namespace ImgTool.Core.Models;

/// <summary>Параметры сборки коллажа.</summary>
public sealed class CollageOptions
{
    /// <summary>Количество колонок.</summary>
    public int Columns { get; set; } = 3;

    /// <summary>
    /// Ширина итогового изображения в пикселях. Если не задана, ширина ячейки
    /// берётся из первого изображения, а итоговая ширина рассчитывается по числу колонок.
    /// </summary>
    public int? Width { get; set; } = 1920;

    /// <summary>Пропорция ячейки (ширина / высота).</summary>
    public double CellAspect { get; set; } = 1.0;

    public int Spacing { get; set; } = 12;

    public int Padding { get; set; } = 16;

    /// <summary>Цвет фона в формате HEX (#RGB, #RRGGBB или #AARRGGBB).</summary>
    public string BackgroundHex { get; set; } = "#12161F";

    /// <summary>Прозрачный фон. Имеет смысл только для PNG и WebP.</summary>
    public bool TransparentBackground { get; set; }

    /// <summary>Раскладка: сетка, вертикальная или горизонтальная склейка.</summary>
    public CollageLayout Layout { get; set; } = CollageLayout.Grid;

    public CollageFit Fit { get; set; } = CollageFit.Cover;

    public ImageFormatKind Format { get; set; } = ImageFormatKind.Jpeg;

    public int Quality { get; set; } = 90;
}

/// <summary>Результат сборки коллажа.</summary>
public sealed record CollageResult(string OutputPath, int Width, int Height, int ImageCount);
