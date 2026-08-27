using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TidyMemo.Services;

public class FolderService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".heic", ".heif"
    };

    public int GetImageFilesCount(string folderPath, bool includeSubfolders = false)
    {
        var option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetFiles(folderPath, "*", option)
            .Count(file => ImageExtensions.Contains(Path.GetExtension(file)));
    }

    public string[] GetImageFiles(string folderPath, bool includeSubfolders = false)
    {
        var option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetFiles(folderPath, "*", option)
            .Where(file => ImageExtensions.Contains(Path.GetExtension(file)))
            .ToArray();
    }
}
