using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Platform.Storage;
using TidyMemo.ViewModels;
using TidyMemo.Views;

namespace TidyMemo.Services;

public class DialogService(MainWindow owner) : IDialogService
{
    public async Task<string?> ShowFolderBrowserDialogAsync()
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });
        
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    public async Task<string?> ShowFilePickerAsync(string title, string[] patterns)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Supported files") { Patterns = patterns }
            }
        });
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async Task<IReadOnlyList<string>> ShowFilePickerMultipleAsync(string title, string[] patterns)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
            FileTypeFilter = new[] { new FilePickerFileType("Supported files") { Patterns = patterns } }
        });
        return files.Select(file => file.Path.LocalPath).ToArray();
    }

    public async Task<string?> ShowSaveFilePickerAsync(string title, string suggestedFileName, string extension)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = extension.TrimStart('.'),
            FileTypeChoices = new[]
            {
                new FilePickerFileType($"{extension.TrimStart('.').ToUpperInvariant()} file")
                    { Patterns = new[] { $"*.{extension.TrimStart('.')}" } }
            }
        });
        return file?.Path.LocalPath;
    }

    public async Task<ExifMetadataDialogResult> ShowExifMetadataDialogAsync(ExifInput exifInput)
    {
        owner.ShowOverlay();
        var dialog = new ExifMetadataExplorerDialog(exifInput);
        var result = await dialog.ShowDialog<ExifMetadataDialogResult>(owner);
        owner.HideOverlay();
        return result;
    }
}
