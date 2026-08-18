namespace TidyMemo.Models;

public class PreviewModel
{
    public string? FolderPath { get; set; }
    public string? OldFilename { get; set; }
    public string? NewFilename { get; set; }
    public string? Extension { get; set; }
    public string NewNameWithExtension => $"{NewFilename}{Extension}";
}