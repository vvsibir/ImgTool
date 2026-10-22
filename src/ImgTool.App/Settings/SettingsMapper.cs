using ImgTool.App.ViewModels;
using ImgTool.Core.Models;

namespace ImgTool.App.Settings;

/// <summary>Перенос значений между настройками и ViewModel.</summary>
public static class SettingsMapper
{
    public static void Apply(MainViewModel viewModel, AppSettings settings)
    {
        Apply(viewModel.Batch, settings.Batch);
        Apply(viewModel.Collage, settings.Collage);
        Apply(viewModel.Metadata, settings.Metadata);
        viewModel.ActiveSection = EnumOr(settings.ActiveSection, AppSection.Batch);
    }

    public static void Capture(MainViewModel viewModel, AppSettings settings)
    {
        settings.ActiveSection = viewModel.ActiveSection;
        settings.Batch = Capture(viewModel.Batch);
        settings.Collage = Capture(viewModel.Collage);
        settings.Metadata = Capture(viewModel.Metadata);
    }

    public static void Apply(BatchViewModel viewModel, BatchSettings? settings)
    {
        if (settings is null)
        {
            return;
        }

        viewModel.OutputDirectory = settings.OutputDirectory ?? string.Empty;
        viewModel.FileNamePattern = settings.FileNamePattern ?? "{name}_out";
        viewModel.Format = EnumOr(settings.Format, ImageFormatKind.Keep);
        viewModel.Quality = settings.Quality;
        viewModel.CollisionPolicy = EnumOr(settings.CollisionPolicy, CollisionPolicy.Rename);
        viewModel.PreserveMetadata = settings.PreserveMetadata;
        viewModel.ResizeEnabled = settings.ResizeEnabled;
        viewModel.ResizeMode = EnumOr(settings.ResizeMode, ResizeMode.Fit);
        viewModel.ResizeWidth = settings.ResizeWidth;
        viewModel.ResizeHeight = settings.ResizeHeight;
        viewModel.ResizePercent = settings.ResizePercent;
        viewModel.DontUpscale = settings.DontUpscale;
        viewModel.CropEnabled = settings.CropEnabled;
        viewModel.CropMode = EnumOr(settings.CropMode, CropMode.AspectRatio);
        viewModel.CropUnit = EnumOr(settings.CropUnit, CropUnit.Pixels);
        viewModel.CropAspectWidth = settings.CropAspectWidth;
        viewModel.CropAspectHeight = settings.CropAspectHeight;
        viewModel.CropAnchor = EnumOr(settings.CropAnchor, Anchor.Center);
        viewModel.CropMarginLeft = settings.CropMarginLeft;
        viewModel.CropMarginTop = settings.CropMarginTop;
        viewModel.CropMarginRight = settings.CropMarginRight;
        viewModel.CropMarginBottom = settings.CropMarginBottom;
        viewModel.CropRectangleX = settings.CropRectangleX;
        viewModel.CropRectangleY = settings.CropRectangleY;
        viewModel.CropRectangleWidth = settings.CropRectangleWidth;
        viewModel.CropRectangleHeight = settings.CropRectangleHeight;
        viewModel.RotationDegrees = settings.RotationDegrees is 0 or 90 or 180 or 270
            ? settings.RotationDegrees
            : 0;
        viewModel.FlipHorizontal = settings.FlipHorizontal;
        viewModel.FlipVertical = settings.FlipVertical;
        viewModel.WatermarkEnabled = settings.WatermarkEnabled;
        viewModel.WatermarkKind = EnumOr(settings.WatermarkKind, WatermarkKind.Text);
        viewModel.WatermarkText = settings.WatermarkText ?? "imgTool";
        viewModel.WatermarkFont = settings.WatermarkFont ?? "Segoe UI";
        viewModel.WatermarkFontSize = settings.WatermarkFontSize;
        viewModel.WatermarkColor = settings.WatermarkColor ?? "#FFFFFF";
        viewModel.WatermarkOpacity = settings.WatermarkOpacity;
        viewModel.WatermarkPosition = EnumOr(settings.WatermarkPosition, Anchor.BottomRight);
        viewModel.WatermarkMargin = settings.WatermarkMargin;
        viewModel.WatermarkShadow = settings.WatermarkShadow;
        viewModel.WatermarkLogoPath = settings.WatermarkLogoPath ?? string.Empty;
        viewModel.WatermarkLogoWidth = settings.WatermarkLogoWidth;
        viewModel.RecursiveFolders = settings.RecursiveFolders;
    }

    public static void Apply(CollageViewModel viewModel, CollageSettings? settings)
    {
        if (settings is null)
        {
            return;
        }

        viewModel.Columns = settings.Columns;
        viewModel.Width = settings.Width is int width && width > 0 ? width : null;
        viewModel.CellAspect = settings.CellAspect;
        viewModel.Spacing = settings.Spacing;
        viewModel.Padding = settings.Padding;
        viewModel.BackgroundHex = settings.BackgroundHex ?? "#12161F";
        viewModel.Layout = EnumOr(settings.Layout, CollageLayout.Grid);
        viewModel.Fit = EnumOr(settings.Fit, CollageFit.Cover);
        viewModel.OutputPath = settings.OutputPath ?? string.Empty;

        ImageFormatKind format = EnumOr(settings.Format, ImageFormatKind.Jpeg);
        viewModel.Format = format == ImageFormatKind.Keep ? ImageFormatKind.Jpeg : format;
        viewModel.Quality = settings.Quality;
        viewModel.TransparentBackground =
            settings.TransparentBackground && viewModel.SupportsTransparentBackground;
    }

    public static void Apply(MetadataViewModel viewModel, MetadataSettings? settings)
    {
        if (settings is null)
        {
            return;
        }

        viewModel.RenamePattern = settings.RenamePattern ?? "{date}_{time}";
    }

    public static BatchSettings Capture(BatchViewModel viewModel) => new()
    {
        OutputDirectory = viewModel.OutputDirectory,
        FileNamePattern = viewModel.FileNamePattern,
        Format = viewModel.Format,
        Quality = viewModel.Quality,
        CollisionPolicy = viewModel.CollisionPolicy,
        PreserveMetadata = viewModel.PreserveMetadata,
        ResizeEnabled = viewModel.ResizeEnabled,
        ResizeMode = viewModel.ResizeMode,
        ResizeWidth = viewModel.ResizeWidth,
        ResizeHeight = viewModel.ResizeHeight,
        ResizePercent = viewModel.ResizePercent,
        DontUpscale = viewModel.DontUpscale,
        CropEnabled = viewModel.CropEnabled,
        CropMode = viewModel.CropMode,
        CropUnit = viewModel.CropUnit,
        CropAspectWidth = viewModel.CropAspectWidth,
        CropAspectHeight = viewModel.CropAspectHeight,
        CropAnchor = viewModel.CropAnchor,
        CropMarginLeft = viewModel.CropMarginLeft,
        CropMarginTop = viewModel.CropMarginTop,
        CropMarginRight = viewModel.CropMarginRight,
        CropMarginBottom = viewModel.CropMarginBottom,
        CropRectangleX = viewModel.CropRectangleX,
        CropRectangleY = viewModel.CropRectangleY,
        CropRectangleWidth = viewModel.CropRectangleWidth,
        CropRectangleHeight = viewModel.CropRectangleHeight,
        RotationDegrees = viewModel.RotationDegrees,
        FlipHorizontal = viewModel.FlipHorizontal,
        FlipVertical = viewModel.FlipVertical,
        WatermarkEnabled = viewModel.WatermarkEnabled,
        WatermarkKind = viewModel.WatermarkKind,
        WatermarkText = viewModel.WatermarkText,
        WatermarkFont = viewModel.WatermarkFont,
        WatermarkFontSize = viewModel.WatermarkFontSize,
        WatermarkColor = viewModel.WatermarkColor,
        WatermarkOpacity = viewModel.WatermarkOpacity,
        WatermarkPosition = viewModel.WatermarkPosition,
        WatermarkMargin = viewModel.WatermarkMargin,
        WatermarkShadow = viewModel.WatermarkShadow,
        WatermarkLogoPath = viewModel.WatermarkLogoPath,
        WatermarkLogoWidth = viewModel.WatermarkLogoWidth,
        RecursiveFolders = viewModel.RecursiveFolders
    };

    public static CollageSettings Capture(CollageViewModel viewModel) => new()
    {
        Columns = viewModel.Columns,
        Width = viewModel.Width,
        CellAspect = viewModel.CellAspect,
        Spacing = viewModel.Spacing,
        Padding = viewModel.Padding,
        BackgroundHex = viewModel.BackgroundHex,
        TransparentBackground = viewModel.TransparentBackground,
        Layout = viewModel.Layout,
        Fit = viewModel.Fit,
        Format = viewModel.Format,
        Quality = viewModel.Quality,
        OutputPath = viewModel.OutputPath
    };

    public static MetadataSettings Capture(MetadataViewModel viewModel) => new()
    {
        RenamePattern = viewModel.RenamePattern
    };

    private static T EnumOr<T>(T value, T fallback) where T : struct, Enum =>
        Enum.IsDefined(typeof(T), value) ? value : fallback;
}
