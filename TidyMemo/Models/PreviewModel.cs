using System.IO;

namespace TidyMemo.Models;

public class PreviewModel
{
    public string? FolderPath { get; set; }
    public string? OldFilename { get; set; }
    public string? NewFilename { get; set; }
    public string? Extension { get; set; }
    public string? DestinationFolderPath { get; set; }
    public string NewNameWithExtension => $"{NewFilename}{Extension}";
    public string DestinationPath => Path.Combine(DestinationFolderPath ?? FolderPath ?? string.Empty, NewNameWithExtension);
}
