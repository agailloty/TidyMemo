using System.Threading.Tasks;
using System.Collections.Generic;
using TidyMemo.ViewModels;

namespace TidyMemo.Services;

public interface IDialogService
{
    Task<string?> ShowFolderBrowserDialogAsync();
    Task<string?> ShowFilePickerAsync(string title, string[] patterns);
    Task<IReadOnlyList<string>> ShowFilePickerMultipleAsync(string title, string[] patterns);
    Task<string?> ShowSaveFilePickerAsync(string title, string suggestedFileName, string extension);
    Task<ExifMetadataDialogResult> ShowExifMetadataDialogAsync(ExifInput exifInput);
}
