using ImgTool.Core.Services;

namespace ImgTool.Core.Models;

public sealed class VideoFrameExtractionOptions
{
    public string SourcePath { get; set; } = string.Empty;

    public string OutputDirectory { get; set; } = string.Empty;

    public string FileNamePattern { get; set; } = "{name}_frame_{index}";

    public ImageFormatKind Format { get; set; } = ImageFormatKind.Png;

    public int Quality { get; set; } = 90;

    public CollisionPolicy CollisionPolicy { get; set; } = CollisionPolicy.Rename;

    public double FrameIntervalSeconds { get; set; } = 1.0;

    public int? StartFrame { get; set; }

    public int? EndFrame { get; set; }

    public bool TakeEveryFrame { get; set; }

    public int EveryNthFrame { get; set; } = 1;
}
