namespace ImgTool.Core.Services;

/// <summary>Поиск изображений на диске.</summary>
public static class FileCollector
{
    public static IReadOnlyList<string> FromFolder(string folder, bool recursive)
    {
        if (!Directory.Exists(folder))
        {
            return Array.Empty<string>();
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(folder, "*", searchOption)
            .Where(ImageFormatHelper.IsSupportedSource)
            .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static bool IsSupported(string path) => File.Exists(path) && ImageFormatHelper.IsSupportedSource(path);
}
