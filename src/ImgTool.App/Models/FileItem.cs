using System.IO;
using ImgTool.App.Infrastructure;
using ImgTool.Core.Services;
using IOPath = System.IO.Path;

namespace ImgTool.App.Models;

/// <summary>Элемент списка изображений.</summary>
public sealed class FileItem : BindableBase
{
    private string _path;
    private long _size;

    public FileItem(string path)
    {
        _path = IOPath.GetFullPath(path);
        RefreshSize();
    }

    public string Path
    {
        get => _path;
        set
        {
            string full = IOPath.GetFullPath(value);
            if (!Set(ref _path, full))
            {
                return;
            }

            RefreshSize();
            RaisePropertyChanged(nameof(Name), nameof(Folder), nameof(SizeText), nameof(Extension));
        }
    }

    public string Name => IOPath.GetFileName(_path);

    public string Folder => IOPath.GetDirectoryName(_path) ?? string.Empty;

    public string Extension => IOPath.GetExtension(_path).TrimStart('.').ToUpperInvariant();

    public string SizeText => FormatSize(_size);

    public void RefreshSize()
    {
        try
        {
            _size = File.Exists(_path) ? new FileInfo(_path).Length : 0;
        }
        catch
        {
            _size = 0;
        }
    }

    public void NotifySizeChanged()
    {
        RefreshSize();
        RaisePropertyChanged(nameof(SizeText));
    }

    public static string FormatSize(long bytes)
    {
        string[] units = ["Б", "КБ", "МБ", "ГБ"];
        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{value:0} {units[unit]}"
            : $"{value:0.#} {units[unit]}";
    }

    public static IReadOnlyList<string> ExpandPaths(IEnumerable<string> paths, bool recursive = false)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                foreach (string file in FileCollector.FromFolder(path, recursive))
                {
                    if (seen.Add(file))
                    {
                        result.Add(file);
                    }
                }
            }
            else if (File.Exists(path) && ImageFormatHelper.IsSupportedSource(path))
            {
                string full = IOPath.GetFullPath(path);
                if (seen.Add(full))
                {
                    result.Add(full);
                }
            }
        }

        return result;
    }
}
