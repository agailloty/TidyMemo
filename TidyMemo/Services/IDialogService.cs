using System.Threading.Tasks;
using TidyMemo.ViewModels;

namespace TidyMemo.Services;

public interface IDialogService
{
    Task<string?> ShowFolderBrowserDialogAsync();
    Task<string?> ShowFilePickerAsync(string title, string[] patterns);
    Task<ExifMetadataDialogResult> ShowExifMetadataDialogAsync(ExifInput exifInput);
}