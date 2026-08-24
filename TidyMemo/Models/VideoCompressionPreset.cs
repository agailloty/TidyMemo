namespace TidyMemo.Models;

public class VideoCompressionPreset
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int Crf { get; set; }
    public required string FfmpegPreset { get; set; }
    /// <summary>When true, FFmpeg chooses the encoder's CRF and speed preset defaults.</summary>
    public bool UseEncoderDefaults { get; set; }
    /// <summary>ffmpeg scale filter value, e.g. "1920:-2". Null keeps the original resolution.</summary>
    public string? ScaleFilter { get; set; }
}
