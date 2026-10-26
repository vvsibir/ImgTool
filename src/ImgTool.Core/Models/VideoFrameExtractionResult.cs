namespace ImgTool.Core.Models;

public sealed class VideoFrameExtractionResult
{
    public bool Success { get; set; }

    public string? Error { get; set; }

    public int FrameCount { get; set; }

    public int SavedCount { get; set; }

    public int SkippedCount { get; set; }

    public List<VideoFrameExtractedItem> Frames { get; set; } = [];
}

public sealed class VideoFrameExtractedItem
{
    public int Index { get; set; }

    public string? OutputPath { get; set; }

    public bool Success { get; set; }

    public bool Skipped { get; set; }

    public string? Error { get; set; }
}

public sealed class VideoSourceInfo
{
    public int Width { get; set; }

    public int Height { get; set; }

    public double DurationSeconds { get; set; }

    public double? Fps { get; set; }
}
