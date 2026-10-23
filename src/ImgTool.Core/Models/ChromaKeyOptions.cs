namespace ImgTool.Core.Models;

/// <summary>Параметры удаления фона по ключевому цвету.</summary>
public sealed class ChromaKeyOptions
{
    public bool Enabled { get; set; }

    /// <summary>Цвет фона, который нужно сделать прозрачным.</summary>
    public string KeyColorHex { get; set; } = "#00FF00";

    /// <summary>Допуск по цветовому расстоянию, от 0 до 100.</summary>
    public double Tolerance { get; set; } = 35;

    /// <summary>Ширина мягкого перехода на границе, от 0 до 100.</summary>
    public double Softness { get; set; } = 8;

    /// <summary>Убирать цветной ореол на краях объекта.</summary>
    public bool Despill { get; set; } = true;

    /// <summary>Цвет подложки для форматов без прозрачности (JPEG, BMP).</summary>
    public string BackgroundHex { get; set; } = "#FFFFFF";
}
