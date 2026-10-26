namespace ImgTool.Core.Services;

/// <summary>Поиск изображений на диске.</summary>
public static class FileCollector
{
    public static IReadOnlyList<string> FromFolder(
        string folder,
        bool recursive,
        Func<string, bool>? predicate = null)
    {
        if (!Directory.Exists(folder))
        {
            return Array.Empty<string>();
        }

        predicate ??= ImageFormatHelper.IsSupportedSource;
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(folder, "*", searchOption)
            .Where(predicate)
            .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static bool IsSupported(string path) => File.Exists(path) && ImageFormatHelper.IsSupportedSource(path);
}
