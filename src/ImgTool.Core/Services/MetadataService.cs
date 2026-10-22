using System.Globalization;
using ImgTool.Core.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using SkiaSharp;
using MetadataDir = MetadataExtractor.Directory;

namespace ImgTool.Core.Services;

public sealed record ExifEntry(string Name, string Value);

public sealed record ImageSummary(
    string FilePath,
    int Width,
    int Height,
    string Format,
    long FileSize,
    DateTime? DateTaken,
    bool HasMetadata)
{
    public string Dimensions => $"{Width} × {Height}";
}

public sealed class ImageDetails
{
    public required ImageSummary Summary { get; init; }

    public required IReadOnlyList<ExifEntry> ExifEntries { get; init; }
}

public sealed record RenameResult(string SourcePath, string? TargetPath, string? Error)
{
    public bool Success => Error is null && TargetPath is not null;
}

/// <summary>Чтение, удаление и переименование по метаданным.</summary>
public static class MetadataService
{
    private static readonly Dictionary<string, string> TagLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Make"] = "Производитель",
        ["Model"] = "Камера",
        ["Lens Model"] = "Объектив",
        ["Date/Time Original"] = "Дата съёмки",
        ["Date/Time Digitized"] = "Дата оцифровки",
        ["Date/Time"] = "Дата изменения",
        ["Time Zone Original"] = "Часовой пояс",
        ["Exposure Time"] = "Выдержка",
        ["F-Number"] = "Диафрагма",
        ["ISO Speed Ratings"] = "ISO",
        ["Photographic Sensitivity"] = "ISO",
        ["Focal Length"] = "Фокусное расстояние",
        ["Focal Length 35"] = "ФР (35 мм)",
        ["Exposure Program"] = "Программа съёмки",
        ["Metering Mode"] = "Режим замера",
        ["Flash"] = "Вспышка",
        ["White Balance"] = "Баланс белого",
        ["Orientation"] = "Ориентация",
        ["Software"] = "Программа",
        ["Artist"] = "Автор",
        ["Copyright"] = "Авторские права",
        ["Image Width"] = "Ширина",
        ["Image Height"] = "Высота",
        ["GPS Latitude"] = "Широта",
        ["GPS Longitude"] = "Долгота",
        ["GPS Altitude"] = "Высота над уровнем моря",
        ["GPS Latitude Ref"] = "Направление широты",
        ["GPS Longitude Ref"] = "Направление долготы",
        ["Exposure Bias"] = "Экспокоррекция",
        ["Max Aperture Value"] = "Максимальная диафрагма",
        ["Color Space"] = "Цветовое пространство",
        ["X Resolution"] = "Разрешение по X",
        ["Y Resolution"] = "Разрешение по Y",
        ["Resolution Unit"] = "Единица разрешения"
    };

    public static ImageSummary ReadSummary(string path)
    {
        var file = new FileInfo(path);
        IReadOnlyList<MetadataDir> directories = ReadDirectories(path);
        (int width, int height) = SkiaImage.ReadOrientedSize(path);

        return new ImageSummary(
            path,
            width,
            height,
            ImageFormatHelper.DescribeFormat(path),
            file.Exists ? file.Length : 0,
            ReadDateTaken(directories),
            HasMetadata(directories));
    }

    public static ImageDetails ReadDetails(string path)
    {
        var file = new FileInfo(path);
        IReadOnlyList<MetadataDir> directories = ReadDirectories(path);
        (int width, int height) = SkiaImage.ReadOrientedSize(path);

        var entries = new List<ExifEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (MetadataDir directory in directories)
        {
            if (directory is not (ExifSubIfdDirectory or ExifIfd0Directory or GpsDirectory))
            {
                continue;
            }

            foreach (Tag tag in directory.Tags)
            {
                string value = directory.GetDescription(tag.Type) ?? tag.Description ?? string.Empty;
                value = value.Trim();
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                string name = TagLabels.TryGetValue(tag.Name, out string? label) ? label : tag.Name;
                if (seen.Add(name + "\u0000" + value))
                {
                    entries.Add(new ExifEntry(name, value));
                }
            }
        }

        entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

        var summary = new ImageSummary(
            path,
            width,
            height,
            ImageFormatHelper.DescribeFormat(path),
            file.Exists ? file.Length : 0,
            ReadDateTaken(directories),
            HasMetadata(directories));

        return new ImageDetails { Summary = summary, ExifEntries = entries };
    }

    /// <summary>Удаляет метаданные перекодированием изображения.</summary>
    public static void RemoveMetadata(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string extension = Path.GetExtension(fullPath);
        if (ImageFormatHelper.FromExtension(fullPath) == ImageFormatKind.Keep)
        {
            throw new NotSupportedException($"Формат «{extension}» не поддерживается для удаления метаданных.");
        }

        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Не удалось определить папку файла.");
        string tempPath = Path.Combine(directory, $".imgtool-{Guid.NewGuid():N}{extension}");

        try
        {
            using (SKBitmap bitmap = SkiaImage.LoadOriented(fullPath))
            {
                SkiaImage.SaveByExtension(bitmap, tempPath, 92);
            }

            if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                // Перекодирование уже удалило метаданные, но EXIF мог быть добавлен
                // сторонним кодеком — на всякий случай не переносим ничего.
            }

            File.Move(tempPath, fullPath, true);
            File.SetLastWriteTimeUtc(fullPath, DateTime.UtcNow);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>Переименовывает файлы по дате съёмки (или дате файла, если EXIF нет).</summary>
    public static IReadOnlyList<RenameResult> RenameByDate(IEnumerable<string> paths, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("Шаблон имени не может быть пустым.", nameof(pattern));
        }

        var results = new List<RenameResult>();
        foreach (string path in paths)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                string directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
                string extension = Path.GetExtension(fullPath);
                string nameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);

                DateTime date = GetDateForRename(fullPath, out _);
                string targetName = pattern
                    .Replace("{date}", date.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
                    .Replace("{time}", date.ToString("HH-mm-ss"), StringComparison.OrdinalIgnoreCase)
                    .Replace("{name}", nameWithoutExtension, StringComparison.OrdinalIgnoreCase)
                    .Replace("{ext}", extension.TrimStart('.'), StringComparison.OrdinalIgnoreCase);

                targetName = OutputPathBuilder.Sanitize(targetName);
                if (string.IsNullOrWhiteSpace(targetName))
                {
                    targetName = nameWithoutExtension;
                }

                if (string.IsNullOrEmpty(Path.GetExtension(targetName)))
                {
                    targetName += extension;
                }

                string targetPath = Path.Combine(directory, targetName);
                if (string.Equals(targetPath, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new RenameResult(fullPath, null, "Имя уже соответствует шаблону."));
                    continue;
                }

                targetPath = MakeUniquePath(targetPath);
                File.Move(fullPath, targetPath);
                results.Add(new RenameResult(fullPath, targetPath, null));
            }
            catch (Exception ex)
            {
                results.Add(new RenameResult(path, null, ex.Message));
            }
        }

        return results;
    }

    public static DateTime GetDateForRename(string path, out bool fromExif)
    {
        try
        {
            DateTime? date = ReadDateTaken(ReadDirectories(path));
            if (date is not null)
            {
                fromExif = true;
                return date.Value;
            }
        }
        catch
        {
            // Если файл не читается, используем дату файловой системы.
        }

        fromExif = false;
        return File.GetLastWriteTime(path);
    }

    private static IReadOnlyList<MetadataDir> ReadDirectories(string path) =>
        ImageMetadataReader.ReadMetadata(path);

    private static string MakeUniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        for (int i = 2; i < 100_000; i++)
        {
            string candidate = Path.Combine(directory, $"{name} ({i}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"Не удалось подобрать свободное имя для «{path}».");
    }

    private static bool HasMetadata(IReadOnlyList<MetadataDir> directories) =>
        directories.Any(directory => directory.Tags.Any());

    private static DateTime? ReadDateTaken(IReadOnlyList<MetadataDir> directories)
    {
        var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        if (subIfd is not null)
        {
            foreach (int tag in new[]
                     {
                         ExifDirectoryBase.TagDateTimeOriginal,
                         ExifDirectoryBase.TagDateTimeDigitized
                     })
            {
                if (subIfd.TryGetDateTime(tag, out DateTime parsed) && parsed.Year > 1900)
                {
                    return parsed;
                }
            }
        }

        var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        if (ifd0 is not null &&
            ifd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out DateTime modified) &&
            modified.Year > 1900)
        {
            return modified;
        }

        return null;
    }

    /// <summary>Форматирует дату для интерфейса.</summary>
    public static string FormatDate(DateTime? date) =>
        date?.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.CurrentCulture) ?? "—";
}
