namespace ImgTool.Core.Models;

/// <summary>Результат обработки одного файла.</summary>
public sealed record ProcessingResult(
    string SourcePath,
    string? OutputPath,
    bool Skipped,
    string? Error)
{
    public bool Success => Error is null && !Skipped;

    public static ProcessingResult Ok(string source, string output) => new(source, output, false, null);

    public static ProcessingResult Skip(string source) => new(source, null, true, null);

    public static ProcessingResult Fail(string source, string error) => new(source, null, false, error);
}

/// <summary>Состояние выполнения пакетной обработки.</summary>
public sealed record BatchProgress(
    int Completed,
    int Total,
    string FileName,
    ProcessingResult? Result);
