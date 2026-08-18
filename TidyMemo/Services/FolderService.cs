using System.IO;
using System.Linq;

namespace TidyMemo.Services;

public class FolderService
{
    public int GetImageFilesCount(string folderPath, bool includeSubfolders = false)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff" };
        var option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetFiles(folderPath, "*", option)
            .Count(file => imageExtensions.Contains(Path.GetExtension(file).ToLower()));
    }

    public string[] GetImageFiles(string folderPath, bool includeSubfolders = false)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff" };
        var option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetFiles(folderPath, "*", option)
            .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLower()))
            .ToArray();
    }
}