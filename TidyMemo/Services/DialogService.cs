using System.Threading.Tasks;
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

    public async Task<ExifMetadataDialogResult> ShowExifMetadataDialogAsync(ExifInput exifInput)
    {
        owner.ShowOverlay();
        var dialog = new ExifMetadataExplorerDialog(exifInput);
        var result = await dialog.ShowDialog<ExifMetadataDialogResult>(owner);
        owner.HideOverlay();
        return result;
    }
}
