namespace ImgTool.Core.Models;

/// <summary>Настройки водяного знака.</summary>
public sealed class WatermarkOptions
{
    public bool Enabled { get; set; }

    public WatermarkKind Kind { get; set; } = WatermarkKind.Text;

    public string Text { get; set; } = "imgTool";

    public string FontFamily { get; set; } = "Segoe UI";

    /// <summary>Размер шрифта в пикселях.</summary>
    public float FontSize { get; set; } = 42f;

    /// <summary>Цвет в формате HEX, например #FFFFFF.</summary>
    public string ColorHex { get; set; } = "#FFFFFF";

    /// <summary>Непрозрачность от 0 до 1.</summary>
    public float Opacity { get; set; } = 0.65f;

    public Anchor Position { get; set; } = Anchor.BottomRight;

    /// <summary>Отступ от краёв в пикселях.</summary>
    public float Margin { get; set; } = 24f;

    /// <summary>Тень под текстом для лучшей читаемости.</summary>
    public bool Shadow { get; set; } = true;

    /// <summary>Путь к изображению логотипа.</summary>
    public string LogoPath { get; set; } = string.Empty;

    /// <summary>Ширина логотипа в процентах от ширины изображения.</summary>
    public float LogoWidthPercent { get; set; } = 18f;
}
