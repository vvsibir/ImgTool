using System.Reflection;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using ImgTool.Core.Models;
using SkiaSharp;

namespace ImgTool.Core.Services;

public static class VideoFrameExtractor
{
    private static bool _ffmpegReady;
    private static readonly object _lock = new();

    private static void EnsureFfmpeg()
    {
        if (_ffmpegReady) return;
        lock (_lock)
        {
            if (_ffmpegReady) return;
            try
            {
                string? dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(dir))
                    dir = AppContext.BaseDirectory;
                string toolsDir = Path.Combine(dir, "ffmpeg");
                Directory.CreateDirectory(toolsDir);
                string ffmpeg = Path.Combine(toolsDir, "ffmpeg.exe");
                string ffprobe = Path.Combine(toolsDir, "ffprobe.exe");
                if (!File.Exists(ffmpeg) || !File.Exists(ffprobe))
                {
                    try
                    {
                        var asm = typeof(FFMpegCore.Extensions.Downloader.FFMpegDownloader).Assembly;
                        var type = asm.GetType("FFMpegCore.Extensions.Downloader.FFMpegDownloader");
                        var method = type?.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name.IndexOf("Download", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (method != null)
                        {
                            var pars = method.GetParameters();
                            object? res = pars.Length == 1 ? method.Invoke(null, [toolsDir]) : method.Invoke(null, [toolsDir, Enum.ToObject(pars[1].ParameterType, 0)]);
                            if (res is System.Threading.Tasks.Task t) t.GetAwaiter().GetResult();
                        }
                    }
                    catch { }
                }
                if (File.Exists(ffmpeg) && File.Exists(ffprobe))
                {
                    GlobalFFOptions.Configure(new FFOptions { BinaryFolder = toolsDir, TemporaryFilesFolder = Path.GetTempPath() });
                }
                else if (File.Exists(Path.Combine(dir, "ffmpeg.exe")) && File.Exists(Path.Combine(dir, "ffprobe.exe")))
                {
                    GlobalFFOptions.Configure(new FFOptions { BinaryFolder = dir, TemporaryFilesFolder = Path.GetTempPath() });
                }
            }
            catch { }
            _ffmpegReady = true;
        }
    }

    public static VideoSourceInfo ReadInfo(string sourcePath)
    {
        EnsureFfmpeg();
        IMediaAnalysis media = FFProbe.Analyse(sourcePath);
        double fps = 0;
        if (media.VideoStreams.Count > 0)
        {
            var video = media.VideoStreams[0];
            fps = video.AvgFrameRate > 0 ? video.AvgFrameRate : (video.FrameRate > 0 ? video.FrameRate : 0);
            return new VideoSourceInfo
            {
                Width = video.Width,
                Height = video.Height,
                DurationSeconds = media.Duration.TotalSeconds,
                Fps = fps > 0 ? fps : null
            };
        }

        return new VideoSourceInfo
        {
            Width = 0,
            Height = 0,
            DurationSeconds = media.Duration.TotalSeconds,
            Fps = null
        };
    }

    public static VideoFrameExtractionResult Extract(string sourcePath, VideoFrameExtractionOptions options, Action<int, int, string>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = new VideoFrameExtractionResult();
        try
        {
            EnsureFfmpeg();

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                result.Success = false;
                result.Error = "Исходный файл не найден.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(options.OutputDirectory))
                options.OutputDirectory = Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory();

            Directory.CreateDirectory(options.OutputDirectory);

            IMediaAnalysis media = FFProbe.Analyse(sourcePath);
            if (media.VideoStreams.Count == 0)
            {
                result.Success = false;
                result.Error = "В файле не найден видеопоток.";
                return result;
            }

            var videoStream = media.VideoStreams[0];
            double fps = videoStream.AvgFrameRate > 0 ? videoStream.AvgFrameRate : videoStream.FrameRate;
            if (fps <= 0)
                fps = 25;

            double duration = media.Duration.TotalSeconds;
            if (duration <= 0 && videoStream.Duration.TotalSeconds > 0)
                duration = videoStream.Duration.TotalSeconds;

            List<double> timestamps = [];
            if (options.TakeEveryFrame && options.EveryNthFrame > 0)
            {
                int step = options.EveryNthFrame;
                if (duration <= 0)
                {
                    int estimated = (int)(fps * 10);
                    for (int i = 0; i < estimated; i += step)
                        timestamps.Add(i / fps);
                }
                else
                {
                    int count = (int)(duration * fps);
                    for (int i = 0; i < count; i += step)
                    {
                        double t = i / fps;
                        if (t > duration) break;
                        timestamps.Add(t);
                    }
                }
            }
            else
            {
                double interval = options.FrameIntervalSeconds;
                if (interval <= 0)
                    interval = 1.0;

                if (duration <= 0)
                {
                    for (int i = 0; i < 60; i++)
                        timestamps.Add(i * interval);
                }
                else
                {
                    for (double t = 0; t <= duration + 1e-6; t += interval)
                    {
                        if (t > duration)
                            t = duration;
                        timestamps.Add(t);
                        if (Math.Abs(t - duration) < 1e-6) break;
                    }
                }
            }

            if (options.StartFrame.HasValue || options.EndFrame.HasValue)
            {
                int start = options.StartFrame ?? 0;
                int end = options.EndFrame ?? int.MaxValue;
                timestamps = timestamps.Where((_, idx) => idx >= start && idx <= end).ToList();
            }

            result.FrameCount = timestamps.Count;
            string baseName = Path.GetFileNameWithoutExtension(sourcePath);
            string ext = GetExtension(options.Format);

            for (int i = 0; i < timestamps.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double ts = timestamps[i];
                int frameIndex = i + 1;
                progress?.Invoke(i + 1, timestamps.Count, $"Кадр {frameIndex} из {timestamps.Count}");

                string fileName = BuildFileName(options.FileNamePattern, baseName, frameIndex, ext, videoStream.Width, videoStream.Height, DateTime.Now);
                string outputPath = Path.Combine(options.OutputDirectory, fileName);
                outputPath = ResolveCollision(outputPath, options.CollisionPolicy);
                if (outputPath is null)
                {
                    result.Frames.Add(new VideoFrameExtractedItem
                    {
                        Index = frameIndex,
                        Skipped = true,
                        Success = false
                    });
                    result.SkippedCount++;
                    continue;
                }

                try
                {
                    string tempFile = Path.Combine(Path.GetTempPath(), $"imgtool_frame_{Guid.NewGuid():N}.jpg");
                    try
                    {
                        FFMpeg.Snapshot(sourcePath, tempFile, new System.Drawing.Size(videoStream.Width, videoStream.Height), TimeSpan.FromSeconds(ts));
                        using var bitmap = SKBitmap.Decode(tempFile);
                        if (bitmap is null)
                        {
                            result.Frames.Add(new VideoFrameExtractedItem { Index = frameIndex, Success = false, Error = "Не удалось декодировать кадр" });
                            continue;
                        }

                        SaveBitmap(bitmap, outputPath, options.Format, options.Quality);
                        result.Frames.Add(new VideoFrameExtractedItem { Index = frameIndex, OutputPath = outputPath, Success = true });
                        result.SavedCount++;
                    }
                    finally
                    {
                        if (File.Exists(tempFile))
                            File.Delete(tempFile);
                    }
                }
                catch (Exception ex)
                {
                    result.Frames.Add(new VideoFrameExtractedItem { Index = frameIndex, Success = false, Error = ex.Message });
                }
            }

            result.Success = result.SavedCount > 0;
            if (!result.Success && result.SkippedCount == result.FrameCount)
                result.Error = "Все кадры пропущены из-за совпадения имён.";
            else if (!result.Success && result.Frames.Count > 0)
                result.Error = "Не удалось сохранить ни одного кадра.";
            return result;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.Error = "Операция отменена.";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
    }

    private static string GetExtension(ImageFormatKind format) => format switch
    {
        ImageFormatKind.Jpeg => ".jpg",
        ImageFormatKind.Png => ".png",
        ImageFormatKind.WebP => ".webp",
        ImageFormatKind.Bmp => ".bmp",
        _ => ".png"
    };

    private static string BuildFileName(string pattern, string baseName, int index, string ext, int width, int height, DateTime now)
    {
        string name = pattern;
        name = name.Replace("{name}", baseName);
        name = name.Replace("{index}", index.ToString("000"));
        name = name.Replace("{index:000}", index.ToString("000"));
        name = name.Replace("{ext}", ext.TrimStart('.'));
        name = name.Replace("{date}", now.ToString("yyyyMMdd"));
        name = name.Replace("{width}", width.ToString());
        name = name.Replace("{height}", height.ToString());
        if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            name += ext;
        return name;
    }

    private static void SaveBitmap(SKBitmap bitmap, string path, ImageFormatKind format, int quality)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = format switch
        {
            ImageFormatKind.Png => image.Encode(SKEncodedImageFormat.Png, 100),
            ImageFormatKind.WebP => image.Encode(SKEncodedImageFormat.Webp, Math.Clamp(quality, 1, 100)),
            ImageFormatKind.Bmp => FlattenToWhiteData(bitmap, SKEncodedImageFormat.Bmp),
            ImageFormatKind.Jpeg => FlattenToWhiteData(bitmap, SKEncodedImageFormat.Jpeg, Math.Clamp(quality, 1, 100)),
            _ => image.Encode(SKEncodedImageFormat.Png, 100)
        };
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
        data.SaveTo(stream);
    }

    private static SKData FlattenToWhiteData(SKBitmap bitmap, SKEncodedImageFormat format, int quality = 90)
    {
        using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Rgb888x, SKAlphaType.Opaque));
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawBitmap(bitmap, 0, 0);
        using SKImage image = surface.Snapshot();
        return image.Encode(format, quality);
    }

    private static string? ResolveCollision(string path, CollisionPolicy policy)
    {
        if (!File.Exists(path))
            return path;

        if (policy == CollisionPolicy.Overwrite)
            return path;

        if (policy == CollisionPolicy.Skip)
            return null;

        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        string fileName = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        int counter = 2;
        while (true)
        {
            string candidate = Path.Combine(directory, $"{fileName}({counter}){extension}");
            if (!File.Exists(candidate))
                return candidate;
            counter++;
        }
    }
}
