namespace TidyMemo.Models;

public class AppSettings
{
    public string FfmpegPath { get; set; } = string.Empty;
    public string OutputSubfolderName { get; set; } = "Final";
    public bool IsVideoCompressionEnabled { get; set; } = false;
    public TransitionMode SlideshowTransitionMode { get; set; } = TransitionMode.None;
    public string SlideshowTransitionId { get; set; } = "fade";
    public double SlideshowTransitionDuration { get; set; } = 0.8;
    public PhotoMotionMode SlideshowMotionMode { get; set; } = PhotoMotionMode.None;
    public string SlideshowMotionId { get; set; } = "none";
    public MotionIntensity SlideshowMotionIntensity { get; set; } = MotionIntensity.Normal;
    public MotionEasing SlideshowMotionEasing { get; set; } = MotionEasing.EaseInOut;
    public int SlideshowMotionRandomSeed { get; set; } = 1;
}
