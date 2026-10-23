using System.IO;
using ImgTool.App.Settings;
using ImgTool.App.ViewModels;
using ImgTool.Core.Models;
using ResizeMode = ImgTool.Core.Models.ResizeMode;

namespace ImgTool.App.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void Store_RoundTripsSettings()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "settings.json");
        var store = new SettingsStore(path);
        var expected = new AppSettings
        {
            ActiveSection = AppSection.Collage,
            Window = new WindowSettings
            {
                Left = 120,
                Top = 140,
                Width = 1300,
                Height = 820,
                Maximized = true
            },
            Batch = new BatchSettings
            {
                OutputDirectory = Path.Combine(temp.Path, "out"),
                FileNamePattern = "{name}_done",
                Format = ImageFormatKind.Png,
                Quality = 77,
                CollisionPolicy = CollisionPolicy.Overwrite,
                PreserveMetadata = false,
                ResizeEnabled = true,
                ResizeMode = ResizeMode.Percent,
                ResizePercent = 33,
                WatermarkEnabled = true,
                WatermarkText = "Test",
                RecursiveFolders = true,
                CropEnabled = true,
                CropMode = CropMode.Margins,
                CropUnit = CropUnit.Percent,
                CropMarginLeft = 5,
                CropMarginTop = 6,
                CropMarginRight = 7,
                CropMarginBottom = 8,
                CropRectangleX = 11,
                CropRectangleY = 12,
                CropRectangleWidth = 13,
                CropRectangleHeight = 14,
                ChromaKeyEnabled = true,
                ChromaKeyColor = "#00FF00",
                ChromaKeyTolerance = 31,
                ChromaKeySoftness = 7,
                ChromaKeyDespill = false,
                ChromaKeyBackground = "#112233"
            },
            Collage = new CollageSettings
            {
                Layout = CollageLayout.VerticalStrip,
                Width = null,
                Padding = 21,
                Spacing = 7,
                TransparentBackground = true,
                Format = ImageFormatKind.Png,
                OutputPath = Path.Combine(temp.Path, "collage.png")
            },
            Metadata = new MetadataSettings
            {
                RenamePattern = "{date}-{time}"
            }
        };

        Assert.True(store.TrySave(expected));
        AppSettings actual = store.Load();

        Assert.Equal(expected.ActiveSection, actual.ActiveSection);
        Assert.Equal(expected.Window.Left, actual.Window.Left);
        Assert.Equal(expected.Window.Top, actual.Window.Top);
        Assert.Equal(expected.Window.Width, actual.Window.Width);
        Assert.Equal(expected.Window.Height, actual.Window.Height);
        Assert.True(actual.Window.Maximized);
        Assert.Equal(expected.Batch.OutputDirectory, actual.Batch.OutputDirectory);
        Assert.Equal(expected.Batch.FileNamePattern, actual.Batch.FileNamePattern);
        Assert.Equal(expected.Batch.Format, actual.Batch.Format);
        Assert.Equal(expected.Batch.Quality, actual.Batch.Quality);
        Assert.Equal(expected.Batch.CollisionPolicy, actual.Batch.CollisionPolicy);
        Assert.False(actual.Batch.PreserveMetadata);
        Assert.Equal(expected.Batch.ResizeMode, actual.Batch.ResizeMode);
        Assert.Equal(expected.Batch.ResizePercent, actual.Batch.ResizePercent);
        Assert.Equal(expected.Batch.WatermarkText, actual.Batch.WatermarkText);
        Assert.True(actual.Batch.RecursiveFolders);
        Assert.True(actual.Batch.CropEnabled);
        Assert.Equal(expected.Batch.CropMode, actual.Batch.CropMode);
        Assert.Equal(expected.Batch.CropUnit, actual.Batch.CropUnit);
        Assert.Equal(expected.Batch.CropMarginLeft, actual.Batch.CropMarginLeft);
        Assert.Equal(expected.Batch.CropMarginTop, actual.Batch.CropMarginTop);
        Assert.Equal(expected.Batch.CropMarginRight, actual.Batch.CropMarginRight);
        Assert.Equal(expected.Batch.CropMarginBottom, actual.Batch.CropMarginBottom);
        Assert.Equal(expected.Batch.CropRectangleX, actual.Batch.CropRectangleX);
        Assert.Equal(expected.Batch.CropRectangleY, actual.Batch.CropRectangleY);
        Assert.Equal(expected.Batch.CropRectangleWidth, actual.Batch.CropRectangleWidth);
        Assert.Equal(expected.Batch.CropRectangleHeight, actual.Batch.CropRectangleHeight);
        Assert.True(actual.Batch.ChromaKeyEnabled);
        Assert.Equal(expected.Batch.ChromaKeyColor, actual.Batch.ChromaKeyColor);
        Assert.Equal(expected.Batch.ChromaKeyTolerance, actual.Batch.ChromaKeyTolerance);
        Assert.Equal(expected.Batch.ChromaKeySoftness, actual.Batch.ChromaKeySoftness);
        Assert.False(actual.Batch.ChromaKeyDespill);
        Assert.Equal(expected.Batch.ChromaKeyBackground, actual.Batch.ChromaKeyBackground);
        Assert.Equal(expected.Collage.Layout, actual.Collage.Layout);
        Assert.Null(actual.Collage.Width);
        Assert.Equal(expected.Collage.Padding, actual.Collage.Padding);
        Assert.Equal(expected.Collage.Spacing, actual.Collage.Spacing);
        Assert.True(actual.Collage.TransparentBackground);
        Assert.Equal(expected.Collage.OutputPath, actual.Collage.OutputPath);
        Assert.Equal(expected.Metadata.RenamePattern, actual.Metadata.RenamePattern);
    }

    [Fact]
    public void Store_InvalidJson_ReturnsDefaults()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, "{ not valid json");

        AppSettings settings = new SettingsStore(path).Load();

        Assert.Equal(AppSection.Batch, settings.ActiveSection);
        Assert.Equal(ImageFormatKind.Keep, settings.Batch.Format);
        Assert.Equal(1920, settings.Collage.Width);
        Assert.Equal("{date}_{time}", settings.Metadata.RenamePattern);
    }

    [Fact]
    public void Mapper_RoundTripsViewModelSettings()
    {
        var original = new MainViewModel
        {
            ActiveSection = AppSection.Collage
        };
        original.Batch.OutputDirectory = @"C:\out";
        original.Batch.FileNamePattern = "{name}_done";
        original.Batch.Format = ImageFormatKind.Png;
        original.Batch.Quality = 66;
        original.Batch.ResizeEnabled = true;
        original.Batch.ResizeMode = ResizeMode.Percent;
        original.Batch.ResizePercent = 33;
        original.Batch.CropEnabled = true;
        original.Batch.CropMode = CropMode.Rectangle;
        original.Batch.CropUnit = CropUnit.Percent;
        original.Batch.CropAspectWidth = 4;
        original.Batch.CropAspectHeight = 3;
        original.Batch.CropAnchor = Anchor.TopLeft;
        original.Batch.CropMarginLeft = 1;
        original.Batch.CropMarginTop = 2;
        original.Batch.CropMarginRight = 3;
        original.Batch.CropMarginBottom = 4;
        original.Batch.CropRectangleX = 5;
        original.Batch.CropRectangleY = 6;
        original.Batch.CropRectangleWidth = 7;
        original.Batch.CropRectangleHeight = 8;
        original.Batch.ChromaKeyEnabled = true;
        original.Batch.ChromaKeyColor = "#123456";
        original.Batch.ChromaKeyTolerance = 44;
        original.Batch.ChromaKeySoftness = 9;
        original.Batch.ChromaKeyDespill = false;
        original.Batch.ChromaKeyBackground = "#654321";
        original.Batch.WatermarkEnabled = true;
        original.Batch.WatermarkKind = WatermarkKind.Logo;
        original.Batch.WatermarkLogoPath = @"C:\logo.png";
        original.Collage.Layout = CollageLayout.HorizontalStrip;
        original.Collage.Width = null;
        original.Collage.Padding = 34;
        original.Collage.BackgroundHex = "#112233";
        original.Collage.TransparentBackground = true;
        original.Collage.Format = ImageFormatKind.Png;
        original.Metadata.RenamePattern = "{name}";

        var settings = new AppSettings();
        SettingsMapper.Capture(original, settings);

        var restored = new MainViewModel();
        SettingsMapper.Apply(restored, settings);

        Assert.Equal(AppSection.Collage, restored.ActiveSection);
        Assert.Equal(original.Batch.OutputDirectory, restored.Batch.OutputDirectory);
        Assert.Equal(original.Batch.FileNamePattern, restored.Batch.FileNamePattern);
        Assert.Equal(original.Batch.Format, restored.Batch.Format);
        Assert.Equal(original.Batch.Quality, restored.Batch.Quality);
        Assert.Equal(original.Batch.ResizeMode, restored.Batch.ResizeMode);
        Assert.Equal(original.Batch.ResizePercent, restored.Batch.ResizePercent);
        Assert.Equal(original.Batch.CropMode, restored.Batch.CropMode);
        Assert.Equal(original.Batch.CropUnit, restored.Batch.CropUnit);
        Assert.Equal(original.Batch.CropAspectWidth, restored.Batch.CropAspectWidth);
        Assert.Equal(original.Batch.CropAnchor, restored.Batch.CropAnchor);
        Assert.Equal(original.Batch.CropMarginLeft, restored.Batch.CropMarginLeft);
        Assert.Equal(original.Batch.CropMarginTop, restored.Batch.CropMarginTop);
        Assert.Equal(original.Batch.CropMarginRight, restored.Batch.CropMarginRight);
        Assert.Equal(original.Batch.CropMarginBottom, restored.Batch.CropMarginBottom);
        Assert.Equal(original.Batch.CropRectangleX, restored.Batch.CropRectangleX);
        Assert.Equal(original.Batch.CropRectangleY, restored.Batch.CropRectangleY);
        Assert.Equal(original.Batch.CropRectangleWidth, restored.Batch.CropRectangleWidth);
        Assert.Equal(original.Batch.CropRectangleHeight, restored.Batch.CropRectangleHeight);
        Assert.Equal(original.Batch.ChromaKeyEnabled, restored.Batch.ChromaKeyEnabled);
        Assert.Equal(original.Batch.ChromaKeyColor, restored.Batch.ChromaKeyColor);
        Assert.Equal(original.Batch.ChromaKeyTolerance, restored.Batch.ChromaKeyTolerance);
        Assert.Equal(original.Batch.ChromaKeySoftness, restored.Batch.ChromaKeySoftness);
        Assert.Equal(original.Batch.ChromaKeyDespill, restored.Batch.ChromaKeyDespill);
        Assert.Equal(original.Batch.ChromaKeyBackground, restored.Batch.ChromaKeyBackground);
        Assert.Equal(original.Batch.WatermarkKind, restored.Batch.WatermarkKind);
        Assert.Equal(original.Batch.WatermarkLogoPath, restored.Batch.WatermarkLogoPath);
        Assert.Equal(original.Collage.Layout, restored.Collage.Layout);
        Assert.Null(restored.Collage.Width);
        Assert.Equal(original.Collage.Padding, restored.Collage.Padding);
        Assert.Equal(original.Collage.BackgroundHex, restored.Collage.BackgroundHex);
        Assert.True(restored.Collage.TransparentBackground);
        Assert.Equal(original.Metadata.RenamePattern, restored.Metadata.RenamePattern);
    }

    [Fact]
    public void Mapper_InvalidEnumValues_FallBackToDefaults()
    {
        var settings = new AppSettings
        {
            ActiveSection = (AppSection)999,
            Batch = new BatchSettings { Format = (ImageFormatKind)999, ResizeMode = (ResizeMode)999 },
            Collage = new CollageSettings { Layout = (CollageLayout)999, Format = (ImageFormatKind)999 }
        };

        var viewModel = new MainViewModel();
        SettingsMapper.Apply(viewModel, settings);

        Assert.Equal(AppSection.Batch, viewModel.ActiveSection);
        Assert.Equal(ImageFormatKind.Keep, viewModel.Batch.Format);
        Assert.Equal(ResizeMode.Fit, viewModel.Batch.ResizeMode);
        Assert.Equal(CollageLayout.Grid, viewModel.Collage.Layout);
        Assert.Equal(ImageFormatKind.Jpeg, viewModel.Collage.Format);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "imgtool-app-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Временная папка могла быть уже удалена.
            }
        }
    }
}
