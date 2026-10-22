namespace ImgTool.Core.Models;

/// <summary>Параметры пакетной обработки изображений.</summary>
public sealed class ProcessingOptions
{
    /// <summary>Папка результата. Пустое значение — сохранять рядом с исходником.</summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Шаблон имени файла. Поддерживает {name}, {ext}, {index}, {date}, {width}, {height}, {format}.
    /// </summary>
    public string FileNamePattern { get; set; } = "{name}_out";

    public ImageFormatKind Format { get; set; } = ImageFormatKind.Keep;

    /// <summary>Качество JPEG/WebP, от 1 до 100.</summary>
    public int Quality { get; set; } = 88;

    public CollisionPolicy CollisionPolicy { get; set; } = CollisionPolicy.Rename;

    /// <summary>Сохранять EXIF/XMP/IPTC метаданные.</summary>
    public bool PreserveMetadata { get; set; } = true;

    public bool ResizeEnabled { get; set; }

    public ResizeMode ResizeMode { get; set; } = ResizeMode.Fit;

    public int ResizeWidth { get; set; } = 1920;

    public int ResizeHeight { get; set; } = 1080;

    public int ResizePercent { get; set; } = 50;

    /// <summary>Не увеличивать изображения больше исходного размера.</summary>
    public bool DontUpscale { get; set; } = true;

    public bool CropEnabled { get; set; }

    /// <summary>Способ обрезки: по пропорции, полями или прямоугольником.</summary>
    public CropMode CropMode { get; set; }

    /// <summary>Единицы полей и прямоугольника обрезки.</summary>
    public CropUnit CropUnit { get; set; }

    /// <summary>Ширина пропорции обрезки (например, 16).</summary>
    public double CropAspectWidth { get; set; } = 1;

    /// <summary>Высота пропорции обрезки (например, 9).</summary>
    public double CropAspectHeight { get; set; } = 1;

    public Anchor CropAnchor { get; set; } = Anchor.Center;

    /// <summary>Левое поле обрезки в единицах <see cref="CropUnit"/>.</summary>
    public double CropMarginLeft { get; set; }

    /// <summary>Верхнее поле обрезки в единицах <see cref="CropUnit"/>.</summary>
    public double CropMarginTop { get; set; }

    /// <summary>Правое поле обрезки в единицах <see cref="CropUnit"/>.</summary>
    public double CropMarginRight { get; set; }

    /// <summary>Нижнее поле обрезки в единицах <see cref="CropUnit"/>.</summary>
    public double CropMarginBottom { get; set; }

    /// <summary>Левый край прямоугольника обрезки в единицах <see cref="CropUnit"/>.</summary>
    public double CropRectangleX { get; set; }

    /// <summary>Верхний край прямоугольника обрезки в единицах <see cref="CropUnit"/>.</summary>
    public double CropRectangleY { get; set; }

    /// <summary>Ширина прямоугольника обрезки в единицах <see cref="CropUnit"/>.</summary>
    public double CropRectangleWidth { get; set; }

    /// <summary>Высота прямоугольника обрезки в единицах <see cref="CropUnit"/>.</summary>
    public double CropRectangleHeight { get; set; }

    /// <summary>Поворот: 0, 90, 180 или 270 градусов.</summary>
    public int RotationDegrees { get; set; }

    public bool FlipHorizontal { get; set; }

    public bool FlipVertical { get; set; }

    public WatermarkOptions Watermark { get; set; } = new();

    public ProcessingOptions Clone()
    {
        var clone = (ProcessingOptions)MemberwiseClone();
        clone.Watermark = new WatermarkOptions
        {
            Enabled = Watermark.Enabled,
            Kind = Watermark.Kind,
            Text = Watermark.Text,
            FontFamily = Watermark.FontFamily,
            FontSize = Watermark.FontSize,
            ColorHex = Watermark.ColorHex,
            Opacity = Watermark.Opacity,
            Position = Watermark.Position,
            Margin = Watermark.Margin,
            Shadow = Watermark.Shadow,
            LogoPath = Watermark.LogoPath,
            LogoWidthPercent = Watermark.LogoWidthPercent
        };
        return clone;
    }
}
