namespace TidyMemo.Models;

public class AppSettings
{
    public string FfmpegPath { get; set; } = string.Empty;
    public string OutputSubfolderName { get; set; } = "Final";
    public bool IsVideoCompressionEnabled { get; set; } = false;
}
