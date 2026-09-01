namespace TidyMemo.Models;

public class AppSettings
{
    public string FfmpegPath { get; set; } = string.Empty;
    public string OutputSubfolderName { get; set; } = "Final";
    public bool IsVideoCompressionEnabled { get; set; } = false;
    public TransitionMode SlideshowTransitionMode { get; set; } = TransitionMode.None;
    public string SlideshowTransitionId { get; set; } = "fade";
    public double SlideshowTransitionDuration { get; set; } = 0.8;
}
