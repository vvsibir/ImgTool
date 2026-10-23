using ImgTool.App.ViewModels;
using ImgTool.Core.Models;

namespace ImgTool.App.Settings;

/// <summary>Пользовательские настройки, сохраняемые между запусками.</summary>
public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    public WindowSettings Window { get; set; } = new();

    public AppSection ActiveSection { get; set; } = AppSection.Batch;

    public BatchSettings Batch { get; set; } = new();

    public CollageSettings Collage { get; set; } = new();

    public MetadataSettings Metadata { get; set; } = new();
}

/// <summary>Положение и размер главного окна.</summary>
public sealed class WindowSettings
{
    public double? Left { get; set; }

    public double? Top { get; set; }

    public double? Width { get; set; }

    public double? Height { get; set; }

    public bool Maximized { get; set; }
}

/// <summary>Настройки пакетной обработки.</summary>
public sealed class BatchSettings
{
    public string OutputDirectory { get; set; } = string.Empty;

    public string FileNamePattern { get; set; } = "{name}_out";

    public ImageFormatKind Format { get; set; } = ImageFormatKind.Keep;

    public int Quality { get; set; } = 88;

    public CollisionPolicy CollisionPolicy { get; set; } = CollisionPolicy.Rename;

    public bool PreserveMetadata { get; set; } = true;

    public bool ResizeEnabled { get; set; }

    public ResizeMode ResizeMode { get; set; } = ResizeMode.Fit;

    public int ResizeWidth { get; set; } = 1920;

    public int ResizeHeight { get; set; } = 1080;

    public int ResizePercent { get; set; } = 50;

    public bool DontUpscale { get; set; } = true;

    public bool CropEnabled { get; set; }

    public CropMode CropMode { get; set; }

    public CropUnit CropUnit { get; set; }

    public double CropAspectWidth { get; set; } = 1;

    public double CropAspectHeight { get; set; } = 1;

    public Anchor CropAnchor { get; set; } = Anchor.Center;

    public double CropMarginLeft { get; set; }

    public double CropMarginTop { get; set; }

    public double CropMarginRight { get; set; }

    public double CropMarginBottom { get; set; }

    public double CropRectangleX { get; set; }

    public double CropRectangleY { get; set; }

    public double CropRectangleWidth { get; set; }

    public double CropRectangleHeight { get; set; }

    public int RotationDegrees { get; set; }

    public bool FlipHorizontal { get; set; }

    public bool FlipVertical { get; set; }

    public bool ChromaKeyEnabled { get; set; }

    public string ChromaKeyColor { get; set; } = "#00FF00";

    public double ChromaKeyTolerance { get; set; } = 35;

    public double ChromaKeySoftness { get; set; } = 8;

    public bool ChromaKeyDespill { get; set; } = true;

    public string ChromaKeyBackground { get; set; } = "#FFFFFF";

    public bool WatermarkEnabled { get; set; }

    public WatermarkKind WatermarkKind { get; set; } = WatermarkKind.Text;

    public string WatermarkText { get; set; } = "imgTool";

    public string WatermarkFont { get; set; } = "Segoe UI";

    public float WatermarkFontSize { get; set; } = 42f;

    public string WatermarkColor { get; set; } = "#FFFFFF";

    public float WatermarkOpacity { get; set; } = 0.65f;

    public Anchor WatermarkPosition { get; set; } = Anchor.BottomRight;

    public float WatermarkMargin { get; set; } = 24f;

    public bool WatermarkShadow { get; set; } = true;

    public string WatermarkLogoPath { get; set; } = string.Empty;

    public float WatermarkLogoWidth { get; set; } = 18f;

    public bool RecursiveFolders { get; set; }
}

/// <summary>Настройки сборки коллажа.</summary>
public sealed class CollageSettings
{
    public int Columns { get; set; } = 3;

    /// <summary><see langword="null"/> — ширина рассчитывается автоматически.</summary>
    public int? Width { get; set; } = 1920;

    public double CellAspect { get; set; } = 1.0;

    public int Spacing { get; set; } = 12;

    public int Padding { get; set; } = 16;

    public string BackgroundHex { get; set; } = "#12161F";

    public bool TransparentBackground { get; set; }

    public CollageLayout Layout { get; set; } = CollageLayout.Grid;

    public CollageFit Fit { get; set; } = CollageFit.Cover;

    public ImageFormatKind Format { get; set; } = ImageFormatKind.Jpeg;

    public int Quality { get; set; } = 90;

    public string OutputPath { get; set; } = string.Empty;
}

/// <summary>Настройки раздела метаданных.</summary>
public sealed class MetadataSettings
{
    public string RenamePattern { get; set; } = "{date}_{time}";
}
