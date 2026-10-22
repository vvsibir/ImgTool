using System.Text;
using ImgTool.Core.Models;

namespace ImgTool.Core.Services;

/// <summary>Формирование имён и путей выходных файлов.</summary>
public static class OutputPathBuilder
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Возвращает путь результата или <c>null</c>, если файл нужно пропустить из-за коллизии.
    /// </summary>
    public static string? Build(
        string sourcePath,
        ProcessingOptions options,
        int index,
        ImageFormatKind format,
        int width,
        int height)
    {
        string directory = string.IsNullOrWhiteSpace(options.OutputDirectory)
            ? Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory
            : Environment.ExpandEnvironmentVariables(options.OutputDirectory.Trim());

        string pattern = string.IsNullOrWhiteSpace(options.FileNamePattern)
            ? "{name}"
            : options.FileNamePattern.Trim();

        string extension = ImageFormatHelper.GetExtension(format);
        string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        DateTime date = File.GetLastWriteTime(sourcePath);

        string name = pattern
            .Replace("{name}", sourceName, StringComparison.OrdinalIgnoreCase)
            .Replace("{ext}", extension.TrimStart('.'), StringComparison.OrdinalIgnoreCase)
            .Replace("{format}", extension.TrimStart('.'), StringComparison.OrdinalIgnoreCase)
            .Replace("{index}", index.ToString("D3"), StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", date.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{width}", width.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{height}", height.ToString(), StringComparison.OrdinalIgnoreCase);

        name = Sanitize(name);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = sourceName;
        }

        string? existingExtension = Path.GetExtension(name);
        if (string.IsNullOrEmpty(existingExtension))
        {
            name += extension;
        }

        string candidate = Path.Combine(directory, name);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        switch (options.CollisionPolicy)
        {
            case CollisionPolicy.Overwrite:
                return candidate;
            case CollisionPolicy.Skip:
                return null;
        }

        string baseName = Path.GetFileNameWithoutExtension(candidate);
        string ext = Path.GetExtension(candidate);
        for (int i = 2; i < 100_000; i++)
        {
            string next = Path.Combine(directory, $"{baseName} ({i}){ext}");
            if (!File.Exists(next))
            {
                return next;
            }
        }

        throw new IOException($"Не удалось подобрать свободное имя для «{candidate}».");
    }

    public static string Sanitize(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (char ch in name)
        {
            builder.Append(Array.IndexOf(InvalidChars, ch) >= 0 ? '_' : ch);
        }

        string result = builder.ToString().Trim();
        return result.TrimEnd('.');
    }
}
