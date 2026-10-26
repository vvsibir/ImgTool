using ImgTool.Core.Models;
using ImgTool.Core.Services;
using SkiaSharp;

namespace ImgTool.Core.Tests;

public sealed class ColorParserTests
{
    [Theory]
    [InlineData("#FFFFFF", 255, 255, 255)]
    [InlineData("#112233", 0x11, 0x22, 0x33)]
    [InlineData("#000000", 0, 0, 0)]
    public void Parse_ReturnsExpectedColor(string hex, byte r, byte g, byte b)
    {
        SKColor color = ColorParser.Parse(hex);

        Assert.Equal(r, color.Red);
        Assert.Equal(g, color.Green);
        Assert.Equal(b, color.Blue);
    }

    [Fact]
    public void Parse_InvalidValue_ReturnsFallback()
    {
        SKColor fallback = new(1, 2, 3);

        Assert.Equal(fallback, ColorParser.Parse("not-a-color", fallback));
    }

    [Fact]
    public void WithOpacity_SetsAlpha()
    {
        SKColor color = ColorParser.WithOpacity("#FF0000", 0.5f);

        Assert.Equal(255, color.Red);
        Assert.InRange(color.Alpha, 127, 128);
    }

    [Fact]
    public void ToHex_RoundTrips()
    {
        SKColor color = new(0x12, 0x34, 0x56);

        Assert.Equal("#123456", ColorParser.ToHex(color));
    }

    [Fact]
    public void Parse_SupportsAlphaChannel()
    {
        SKColor color = ColorParser.Parse("#80FF0000");

        Assert.Equal(0x80, color.Alpha);
        Assert.Equal(255, color.Red);
    }

    [Fact]
    public void Parse_SixDigitHex_IsOpaque()
    {
        SKColor color = ColorParser.Parse("#12161F");

        Assert.Equal(255, color.Alpha);
    }
}

public sealed class ChromaKeyTests
{
    private static SKBitmap CreateBitmap(params SKColor[] colors)
    {
        var bitmap = new SKBitmap(new SKImageInfo(
            colors.Length, 1, SKColorType.Bgra8888, SKAlphaType.Premul));
        for (int i = 0; i < colors.Length; i++)
        {
            bitmap.SetPixel(i, 0, colors[i]);
        }

        return bitmap;
    }

    [Fact]
    public void Apply_RemovesKeyColorAndKeepsOtherColors()
    {
        using SKBitmap bitmap = CreateBitmap(new SKColor(0, 255, 0), SKColors.Red);

        ChromaKeyRemover.Apply(bitmap, new ChromaKeyOptions
        {
            Enabled = true,
            KeyColorHex = "#00FF00",
            Tolerance = 0,
            Softness = 0,
            Despill = false
        });

        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
        Assert.Equal(255, bitmap.GetPixel(1, 0).Alpha);
    }

    [Fact]
    public void Apply_RemovesDarkGreenWithinTolerance()
    {
        using SKBitmap bitmap = CreateBitmap(new SKColor(0, 0x88, 0));

        ChromaKeyRemover.Apply(bitmap, new ChromaKeyOptions
        {
            Enabled = true,
            KeyColorHex = "#00FF00",
            Tolerance = 40,
            Softness = 0,
            Despill = false
        });

        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
    }

    [Fact]
    public void Apply_SoftnessProducesPartialAlpha()
    {
        using SKBitmap bitmap = CreateBitmap(new SKColor(0, 0xCC, 0));

        ChromaKeyRemover.Apply(bitmap, new ChromaKeyOptions
        {
            Enabled = true,
            KeyColorHex = "#00FF00",
            Tolerance = 5,
            Softness = 10,
            Despill = false
        });

        Assert.InRange(bitmap.GetPixel(0, 0).Alpha, 130, 160);
    }

    [Fact]
    public void Apply_DespillReducesGreenFringe()
    {
        using SKBitmap bitmap = CreateBitmap(new SKColor(0x80, 0xC0, 0x80));

        ChromaKeyRemover.Apply(bitmap, new ChromaKeyOptions
        {
            Enabled = true,
            KeyColorHex = "#00FF00",
            Tolerance = 35,
            Softness = 10,
            Despill = true
        });

        SKColor result = bitmap.GetPixel(0, 0);
        Assert.InRange(result.Alpha, 100, 160);
        Assert.True(result.Green < 175, $"Green channel is {result.Green}.");
    }
}

public sealed class GifFrameExtractorTests
{
    private const string SimpleGifBase64 =
        "R0lGODlhBAAEAHAAACH5BAEAAAEALAAAAAAEAAQAgf8AAAAAAAAAAAAAAAIEhI8JBQAh+QQBAAABACwAAAAABAAEAIEAAP8AAAAAAAAAAAACBISPCQUAOw==";

    private static string WriteGif(TempDirectory temp, string name, string base64)
    {
        string path = Path.Combine(temp.Path, name);
        byte[] bytes = Convert.FromBase64String(base64);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void Extract_SimpleGif_ExtractsAllFrames()
    {
        using var temp = new TempDirectory();
        string source = WriteGif(temp, "anim.gif", SimpleGifBase64);

        var options = new GifExtractionOptions
        {
            OutputDirectory = temp.Path,
            FileNamePattern = "{name}_f{index}",
            Format = ImageFormatKind.Png,
            CollisionPolicy = CollisionPolicy.Overwrite
        };

        GifExtractionResult result = GifFrameExtractor.Extract(source, options);

        Assert.True(result.Success);
        Assert.Equal(2, result.FrameCount);
        Assert.Equal(2, result.SavedCount);
        Assert.True(File.Exists(result.Frames[0].OutputPath!));
        Assert.True(File.Exists(result.Frames[1].OutputPath!));
        Assert.EndsWith("_f001.png", result.Frames[0].OutputPath);
        Assert.EndsWith("_f002.png", result.Frames[1].OutputPath);

        using SKBitmap first = SkiaImage.LoadOriented(result.Frames[0].OutputPath!);
        using SKBitmap second = SkiaImage.LoadOriented(result.Frames[1].OutputPath!);
        Assert.Equal(4, first.Width);
        Assert.Equal(4, first.Height);
        Assert.Equal(SKColors.Red, first.GetPixel(0, 0));
        Assert.Equal(SKColors.Blue, second.GetPixel(0, 0));
    }

    [Fact]
    public void Extract_JpegFlattensOnWhite()
    {
        using var temp = new TempDirectory();
        string source = WriteGif(temp, "anim.gif", SimpleGifBase64);

        var options = new GifExtractionOptions
        {
            OutputDirectory = temp.Path,
            FileNamePattern = "{name}_f{index}",
            Format = ImageFormatKind.Jpeg,
            CollisionPolicy = CollisionPolicy.Overwrite
        };

        GifExtractionResult result = GifFrameExtractor.Extract(source, options);

        Assert.True(result.Success);
        Assert.All(result.Frames, frame => Assert.EndsWith(".jpg", frame.OutputPath));
        using SKBitmap output = SkiaImage.LoadOriented(result.Frames[0].OutputPath!);
        Assert.Equal(4, output.Width);
        Assert.Equal(4, output.Height);
    }

    [Fact]
    public void Extract_CollisionSkip_SkipsFrame()
    {
        using var temp = new TempDirectory();
        string source = WriteGif(temp, "anim.gif", SimpleGifBase64);
        string existing = Path.Combine(temp.Path, "anim_f001.png");
        File.WriteAllText(existing, "x");

        var options = new GifExtractionOptions
        {
            OutputDirectory = temp.Path,
            FileNamePattern = "{name}_f{index}",
            Format = ImageFormatKind.Png,
            CollisionPolicy = CollisionPolicy.Skip
        };

        GifExtractionResult result = GifFrameExtractor.Extract(source, options);

        Assert.Equal(1, result.SavedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.True(result.Frames[0].Skipped);
        Assert.True(result.Frames[1].Success);
    }

    [Fact]
    public void ReadInfo_ReturnsFrameCount()
    {
        using var temp = new TempDirectory();
        string source = WriteGif(temp, "anim.gif", SimpleGifBase64);

        GifSourceInfo info = GifFrameExtractor.ReadInfo(source);

        Assert.Equal(4, info.Width);
        Assert.Equal(4, info.Height);
        Assert.Equal(2, info.FrameCount);
    }
}

public sealed class ImageProcessorTests
{
    private static SKBitmap CreateBitmap(int width, int height, SKColor color)
    {
        var bitmap = new SKBitmap(new SKImageInfo(
            width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        bitmap.Erase(color);
        return bitmap;
    }

    [Theory]
    [InlineData(ImageFormatKind.Jpeg, ".jpg")]
    [InlineData(ImageFormatKind.Png, ".png")]
    [InlineData(ImageFormatKind.WebP, ".webp")]
    [InlineData(ImageFormatKind.Bmp, ".bmp")]
    public void SaveAndLoad_RoundTripsDimensions(ImageFormatKind format, string extension)
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "test" + extension);

        using (SKBitmap bitmap = CreateBitmap(64, 48, new SKColor(10, 20, 30)))
        {
            SkiaImage.Save(bitmap, path, format, 90);
        }

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);

        (int width, int height) = SkiaImage.ReadOrientedSize(path);
        Assert.Equal(64, width);
        Assert.Equal(48, height);
    }

    [Fact]
    public void ProcessFile_ResizeFit_KeepsAspectRatio()
    {
        using var temp = new TempDirectory();
        string source = Path.Combine(temp.Path, "source.png");
        using (SKBitmap bitmap = CreateBitmap(200, 100, SKColors.Red))
        {
            SkiaImage.Save(bitmap, source, ImageFormatKind.Png);
        }

        var options = new ProcessingOptions
        {
            ResizeEnabled = true,
            ResizeMode = ResizeMode.Fit,
            ResizeWidth = 100,
            ResizeHeight = 100,
            FileNamePattern = "{name}_small"
        };

        ProcessingResult result = new ImageProcessor().ProcessFile(source, options);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.OutputPath);
        (int width, int height) = SkiaImage.ReadOrientedSize(result.OutputPath!);
        Assert.Equal(100, width);
        Assert.Equal(50, height);
    }

    [Fact]
    public void ProcessFile_CropAndRotate_ProducesExpectedDimensions()
    {
        using var temp = new TempDirectory();
        string source = Path.Combine(temp.Path, "source.png");
        using (SKBitmap bitmap = CreateBitmap(200, 100, SKColors.Blue))
        {
            SkiaImage.Save(bitmap, source, ImageFormatKind.Png);
        }

        var options = new ProcessingOptions
        {
            CropEnabled = true,
            CropAspectWidth = 1,
            CropAspectHeight = 1,
            RotationDegrees = 90,
            FileNamePattern = "{name}_rotated"
        };

        ProcessingResult result = new ImageProcessor().ProcessFile(source, options);

        Assert.True(result.Success, result.Error);
        (int width, int height) = SkiaImage.ReadOrientedSize(result.OutputPath!);
        Assert.Equal(100, width);
        Assert.Equal(100, height);
    }

    [Fact]
    public void ProcessFile_MarginCrop_UsesPixels()
    {
        using var temp = new TempDirectory();
        string source = Path.Combine(temp.Path, "source.png");
        using (SKBitmap bitmap = CreateBitmap(200, 100, SKColors.Blue))
        {
            SkiaImage.Save(bitmap, source, ImageFormatKind.Png);
        }

        var options = new ProcessingOptions
        {
            CropEnabled = true,
            CropMode = CropMode.Margins,
            CropUnit = CropUnit.Pixels,
            CropMarginLeft = 10,
            CropMarginTop = 5,
            CropMarginRight = 20,
            CropMarginBottom = 15,
            FileNamePattern = "{name}_margins"
        };

        ProcessingResult result = new ImageProcessor().ProcessFile(source, options);

        Assert.True(result.Success, result.Error);
        (int width, int height) = SkiaImage.ReadOrientedSize(result.OutputPath!);
        Assert.Equal(170, width);
        Assert.Equal(80, height);
    }

    [Fact]
    public void ProcessFile_MarginCrop_UsesPercent()
    {
        using var temp = new TempDirectory();
        string source = Path.Combine(temp.Path, "source.png");
        using (SKBitmap bitmap = CreateBitmap(200, 100, SKColors.Blue))
        {
            SkiaImage.Save(bitmap, source, ImageFormatKind.Png);
        }

        var options = new ProcessingOptions
        {
            CropEnabled = true,
            CropMode = CropMode.Margins,
            CropUnit = CropUnit.Percent,
            CropMarginLeft = 10,
            CropMarginTop = 10,
            CropMarginRight = 10,
            CropMarginBottom = 10,
            FileNamePattern = "{name}_margins_percent"
        };

        ProcessingResult result = new ImageProcessor().ProcessFile(source, options);

        Assert.True(result.Success, result.Error);
        Assert.Equal(new SKRectI(20, 10, 180, 90), ImageProcessor.CalculateCrop(200, 100, options));
        (int width, int height) = SkiaImage.ReadOrientedSize(result.OutputPath!);
        Assert.Equal(160, width);
        Assert.Equal(80, height);
    }

    [Fact]
    public void ProcessFile_RectangleCrop_UsesSameArea()
    {
        using var temp = new TempDirectory();
        string source = Path.Combine(temp.Path, "source.png");
        using (SKBitmap bitmap = CreateBitmap(200, 100, SKColors.Blue))
        {
            SkiaImage.Save(bitmap, source, ImageFormatKind.Png);
        }

        var options = new ProcessingOptions
        {
            CropEnabled = true,
            CropMode = CropMode.Rectangle,
            CropUnit = CropUnit.Pixels,
            CropRectangleX = 30,
            CropRectangleY = 20,
            CropRectangleWidth = 100,
            CropRectangleHeight = 60,
            FileNamePattern = "{name}_rectangle"
        };

        ProcessingResult result = new ImageProcessor().ProcessFile(source, options);

        Assert.True(result.Success, result.Error);
        Assert.Equal(new SKRectI(30, 20, 130, 80), ImageProcessor.CalculateCrop(200, 100, options));
        (int width, int height) = SkiaImage.ReadOrientedSize(result.OutputPath!);
        Assert.Equal(100, width);
        Assert.Equal(60, height);
    }

    [Fact]
    public void CalculateCrop_RectangleOutsideImage_Throws()
    {
        var options = new ProcessingOptions
        {
            CropMode = CropMode.Rectangle,
            CropUnit = CropUnit.Pixels,
            CropRectangleX = 90,
            CropRectangleY = 0,
            CropRectangleWidth = 20,
            CropRectangleHeight = 20
        };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => ImageProcessor.CalculateCrop(100, 100, options));

        Assert.Contains("выходит за пределы", error.Message);
    }

    [Fact]
    public void CalculateCrop_MarginsInPercentCannotCoverWholeImage()
    {
        var options = new ProcessingOptions
        {
            CropMode = CropMode.Margins,
            CropUnit = CropUnit.Percent,
            CropMarginLeft = 50,
            CropMarginRight = 50
        };

        Assert.Throws<InvalidOperationException>(
            () => ImageProcessor.CalculateCrop(100, 100, options));
    }

    [Fact]
    public void ProcessFile_ChromaKey_PngKeepsTransparency()
    {
        using var temp = new TempDirectory();
        string source = Path.Combine(temp.Path, "source.png");
        using (SKBitmap bitmap = CreateBitmap(2, 1, SKColors.Lime))
        {
            bitmap.SetPixel(1, 0, SKColors.Red);
            SkiaImage.Save(bitmap, source, ImageFormatKind.Png);
        }

        var options = new ProcessingOptions
        {
            Format = ImageFormatKind.Png,
            FileNamePattern = "{name}_key",
            ChromaKey = new ChromaKeyOptions
            {
                Enabled = true,
                KeyColorHex = "#00FF00",
                Tolerance = 0,
                Softness = 0,
                Despill = false
            }
        };

        ProcessingResult result = new ImageProcessor().ProcessFile(source, options);

        Assert.True(result.Success, result.Error);
        using SKBitmap output = SkiaImage.LoadOriented(result.OutputPath!);
        Assert.Equal(0, output.GetPixel(0, 0).Alpha);
        Assert.Equal(255, output.GetPixel(1, 0).Alpha);
    }

    [Fact]
    public void ProcessFile_ChromaKey_WebPKeepsTransparency()
    {
        using var temp = new TempDirectory();
        string source = Path.Combine(temp.Path, "source.png");
        using (SKBitmap bitmap = CreateBitmap(2, 1, SKColors.Lime))
        {
            bitmap.SetPixel(1, 0, SKColors.Red);
            SkiaImage.Save(bitmap, source, ImageFormatKind.Png);
        }

        var options = new ProcessingOptions
        {
            Format = ImageFormatKind.WebP,
            FileNamePattern = "{name}_key",
            Quality = 100,
            ChromaKey = new ChromaKeyOptions
            {
                Enabled = true,
                KeyColorHex = "#00FF00",
                Tolerance = 0,
                Softness = 0,
                Despill = false
            }
        };

        ProcessingResult result = new ImageProcessor().ProcessFile(source, options);

        Assert.True(result.Success, result.Error);
        using SKBitmap output = SkiaImage.LoadOriented(result.OutputPath!);
        Assert.Equal(0, output.GetPixel(0, 0).Alpha);
        Assert.Equal(255, output.GetPixel(1, 0).Alpha);
    }

    [Fact]
    public void ProcessFile_ChromaKey_BmpUsesSelectedBackground()
    {
        using var temp = new TempDirectory();
        string source = Path.Combine(temp.Path, "source.png");
        using (SKBitmap bitmap = CreateBitmap(2, 1, SKColors.Lime))
        {
            bitmap.SetPixel(1, 0, SKColors.Red);
            SkiaImage.Save(bitmap, source, ImageFormatKind.Png);
        }

        var options = new ProcessingOptions
        {
            Format = ImageFormatKind.Bmp,
            FileNamePattern = "{name}_key",
            ChromaKey = new ChromaKeyOptions
            {
                Enabled = true,
                KeyColorHex = "#00FF00",
                Tolerance = 0,
                Softness = 0,
                Despill = false,
                BackgroundHex = "#0000FF"
            }
        };

        ProcessingResult result = new ImageProcessor().ProcessFile(source, options);

        Assert.True(result.Success, result.Error);
        using SKBitmap output = SkiaImage.LoadOriented(result.OutputPath!);
        SKColor corner = output.GetPixel(0, 0);
        Assert.True(corner.Blue > 240, $"Blue channel is {corner.Blue}.");
        Assert.True(corner.Red < 16, $"Red channel is {corner.Red}.");
        Assert.True(corner.Green < 16, $"Green channel is {corner.Green}.");
    }

    [Fact]
    public void ProcessFile_TextWatermark_Succeeds()
    {
        using var temp = new TempDirectory();
        string source = Path.Combine(temp.Path, "source.jpg");
        using (SKBitmap bitmap = CreateBitmap(300, 200, SKColors.DarkSlateGray))
        {
            SkiaImage.Save(bitmap, source, ImageFormatKind.Jpeg);
        }

        var options = new ProcessingOptions
        {
            FileNamePattern = "{name}_wm",
            Watermark = new WatermarkOptions
            {
                Enabled = true,
                Kind = WatermarkKind.Text,
                Text = "imgTool",
                FontSize = 32,
                ColorHex = "#FFFFFF",
                Opacity = 0.7f,
                Position = Anchor.BottomRight,
                Margin = 12,
                Shadow = true
            }
        };

        ProcessingResult result = new ImageProcessor().ProcessFile(source, options);

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(result.OutputPath!));
    }

    [Fact]
    public void CalculateCrop_CenterAndAnchors()
    {
        SKRectI center = ImageProcessor.CalculateCrop(400, 200, 1, 1, Anchor.Center);
        Assert.Equal(new SKRectI(100, 0, 300, 200), center);

        SKRectI left = ImageProcessor.CalculateCrop(400, 200, 1, 1, Anchor.Left);
        Assert.Equal(0, left.Left);

        SKRectI right = ImageProcessor.CalculateCrop(400, 200, 1, 1, Anchor.Right);
        Assert.Equal(200, right.Left);
    }
}

public sealed class CollageBuilderTests
{
    [Fact]
    public void Build_CreatesImageWithExpectedSize()
    {
        using var temp = new TempDirectory();
        string first = Path.Combine(temp.Path, "a.png");
        string second = Path.Combine(temp.Path, "b.png");
        string output = Path.Combine(temp.Path, "collage.jpg");

        using (var bitmap = new SKBitmap(new SKImageInfo(100, 100, SKColorType.Bgra8888, SKAlphaType.Premul)))
        {
            bitmap.Erase(SKColors.Red);
            SkiaImage.Save(bitmap, first, ImageFormatKind.Png);
            bitmap.Erase(SKColors.Green);
            SkiaImage.Save(bitmap, second, ImageFormatKind.Png);
        }

        var options = new CollageOptions
        {
            Columns = 2,
            Width = 420,
            CellAspect = 1,
            Spacing = 10,
            Padding = 10,
            Fit = CollageFit.Cover,
            Format = ImageFormatKind.Jpeg
        };

        CollageResult result = new CollageBuilder().Build([first, second], options, output);

        Assert.True(File.Exists(output));
        Assert.Equal(420, result.Width);
        Assert.Equal(2, result.ImageCount);
        (int width, int height) = SkiaImage.ReadOrientedSize(output);
        Assert.Equal(420, width);
        Assert.Equal(result.Height, height);
    }

    [Fact]
    public void Build_WithoutWidth_UsesFirstImageWidthAndColumns()
    {
        using var temp = new TempDirectory();
        string first = Path.Combine(temp.Path, "a.png");
        string second = Path.Combine(temp.Path, "b.png");
        string output = Path.Combine(temp.Path, "collage.png");

        using (var bitmap = new SKBitmap(new SKImageInfo(100, 100, SKColorType.Bgra8888, SKAlphaType.Premul)))
        {
            bitmap.Erase(SKColors.Red);
            SkiaImage.Save(bitmap, first, ImageFormatKind.Png);
            bitmap.Erase(SKColors.Green);
            SkiaImage.Save(bitmap, second, ImageFormatKind.Png);
        }

        var options = new CollageOptions
        {
            Columns = 2,
            Width = null,
            CellAspect = 1,
            Spacing = 10,
            Padding = 10,
            Fit = CollageFit.Cover,
            Format = ImageFormatKind.Png
        };

        CollageResult result = new CollageBuilder().Build([first, second], options, output);

        // 10 + 100 + 10 + 100 + 10 = 230 px.
        Assert.Equal(230, result.Width);
        (int width, _) = SkiaImage.ReadOrientedSize(output);
        Assert.Equal(230, width);
    }

    [Fact]
    public void Build_TransparentBackground_WritesPngWithTransparentPadding()
    {
        using var temp = new TempDirectory();
        string source = Path.Combine(temp.Path, "a.png");
        string output = Path.Combine(temp.Path, "collage.png");

        using (var bitmap = new SKBitmap(new SKImageInfo(100, 100, SKColorType.Bgra8888, SKAlphaType.Premul)))
        {
            bitmap.Erase(SKColors.Red);
            SkiaImage.Save(bitmap, source, ImageFormatKind.Png);
        }

        var options = new CollageOptions
        {
            Columns = 1,
            Width = 200,
            CellAspect = 1,
            Spacing = 0,
            Padding = 10,
            Fit = CollageFit.Cover,
            Format = ImageFormatKind.Png,
            TransparentBackground = true
        };

        new CollageBuilder().Build([source], options, output);

        using SKBitmap decoded = SKBitmap.Decode(output);
        Assert.Equal(SKColorType.Bgra8888, decoded.ColorType);
        Assert.Equal(0, decoded.GetPixel(0, 0).Alpha);
        Assert.Equal(0, decoded.GetPixel(199, 199).Alpha);
        Assert.Equal(255, decoded.GetPixel(100, 100).Alpha);
    }

    [Fact]
    public void Build_VerticalStrip_KeepsImagesAsIsAndUsesMaxWidth()
    {
        using var temp = new TempDirectory();
        string first = Path.Combine(temp.Path, "a.png");
        string second = Path.Combine(temp.Path, "b.png");
        string output = Path.Combine(temp.Path, "strip.png");

        using (var bitmap = new SKBitmap(new SKImageInfo(100, 80, SKColorType.Bgra8888, SKAlphaType.Premul)))
        {
            bitmap.Erase(SKColors.Red);
            SkiaImage.Save(bitmap, first, ImageFormatKind.Png);
        }

        using (var bitmap = new SKBitmap(new SKImageInfo(60, 120, SKColorType.Bgra8888, SKAlphaType.Premul)))
        {
            bitmap.Erase(SKColors.Green);
            SkiaImage.Save(bitmap, second, ImageFormatKind.Png);
        }

        var options = new CollageOptions
        {
            Layout = CollageLayout.VerticalStrip,
            Spacing = 7,
            Padding = 5,
            BackgroundHex = "#000000",
            Format = ImageFormatKind.Png
        };

        CollageResult result = new CollageBuilder().Build([first, second], options, output);

        // Ширина: 5 + max(100, 60) + 5 = 110; высота: 5 + 80 + 7 + 120 + 5 = 217.
        Assert.Equal(110, result.Width);
        Assert.Equal(217, result.Height);

        using SKBitmap decoded = SKBitmap.Decode(output);
        Assert.Equal(110, decoded.Width);
        Assert.Equal(217, decoded.Height);

        // Кадры не растягиваются: справа от узкого кадра остаётся фон.
        Assert.Equal(SKColors.Red, decoded.GetPixel(50, 40));
        Assert.Equal(SKColors.Green, decoded.GetPixel(55, 150));
        Assert.Equal(SKColors.Black, decoded.GetPixel(100, 150));
    }

    [Fact]
    public void Build_HorizontalStrip_KeepsImagesAsIsAndUsesMaxHeight()
    {
        using var temp = new TempDirectory();
        string first = Path.Combine(temp.Path, "a.png");
        string second = Path.Combine(temp.Path, "b.png");
        string output = Path.Combine(temp.Path, "strip.png");

        using (var bitmap = new SKBitmap(new SKImageInfo(100, 80, SKColorType.Bgra8888, SKAlphaType.Premul)))
        {
            bitmap.Erase(SKColors.Red);
            SkiaImage.Save(bitmap, first, ImageFormatKind.Png);
        }

        using (var bitmap = new SKBitmap(new SKImageInfo(60, 120, SKColorType.Bgra8888, SKAlphaType.Premul)))
        {
            bitmap.Erase(SKColors.Green);
            SkiaImage.Save(bitmap, second, ImageFormatKind.Png);
        }

        var options = new CollageOptions
        {
            Layout = CollageLayout.HorizontalStrip,
            Spacing = 7,
            Padding = 5,
            BackgroundHex = "#000000",
            Format = ImageFormatKind.Png
        };

        CollageResult result = new CollageBuilder().Build([first, second], options, output);

        // Ширина: 5 + 100 + 7 + 60 + 5 = 177; высота: 5 + max(80, 120) + 5 = 130.
        Assert.Equal(177, result.Width);
        Assert.Equal(130, result.Height);

        using SKBitmap decoded = SKBitmap.Decode(output);
        Assert.Equal(177, decoded.Width);
        Assert.Equal(130, decoded.Height);

        // Кадры не растягиваются: под первым кадром остаётся фон.
        Assert.Equal(SKColors.Red, decoded.GetPixel(55, 65));
        Assert.Equal(SKColors.Green, decoded.GetPixel(140, 65));
        Assert.Equal(SKColors.Black, decoded.GetPixel(55, 120));
    }
}

public sealed class MetadataServiceTests
{
    [Fact]
    public void ReadSummary_ReportsDimensionsAndFormat()
    {
        using var temp = new TempDirectory();
        string source = Path.Combine(temp.Path, "photo.jpg");
        using (var bitmap = new SKBitmap(new SKImageInfo(120, 80, SKColorType.Bgra8888, SKAlphaType.Premul)))
        {
            bitmap.Erase(SKColors.Orange);
            SkiaImage.Save(bitmap, source, ImageFormatKind.Jpeg);
        }

        ImageSummary summary = MetadataService.ReadSummary(source);

        Assert.Equal(120, summary.Width);
        Assert.Equal(80, summary.Height);
        Assert.Equal("JPEG", summary.Format);
        Assert.True(summary.FileSize > 0);
    }

    [Fact]
    public void RenameByDate_UsesPattern()
    {
        using var temp = new TempDirectory();
        string source = Path.Combine(temp.Path, "photo.png");
        using (var bitmap = new SKBitmap(new SKImageInfo(20, 20, SKColorType.Bgra8888, SKAlphaType.Premul)))
        {
            bitmap.Erase(SKColors.White);
            SkiaImage.Save(bitmap, source, ImageFormatKind.Png);
        }

        var results = MetadataService.RenameByDate([source], "{name}_renamed");

        RenameResult result = Assert.Single(results);
        Assert.True(result.Success, result.Error);
        Assert.Equal("photo_renamed.png", Path.GetFileName(result.TargetPath));
    }
}

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "imgtool-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
        catch
        {
            // Временная папка будет очищена системой.
        }
    }
}
