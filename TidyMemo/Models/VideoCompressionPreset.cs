namespace TidyMemo.Models;

public class VideoCompressionPreset
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int Crf { get; set; }
    public required string FfmpegPreset { get; set; }
    /// <summary>When true, FFmpeg chooses the encoder's CRF and speed preset defaults.</summary>
    public bool UseEncoderDefaults { get; set; }
    /// <summary>
    /// Optional FFmpeg video encoder override. Null preserves the historical preset behavior:
    /// Auto lets FFmpeg keep its default, while configured presets use H.264.
    /// </summary>
    public string? VideoEncoder { get; set; }
    /// <summary>ffmpeg scale filter value, e.g. "1920:-2". Null keeps the original resolution.</summary>
    public string? ScaleFilter { get; set; }
}

public sealed class VideoCodecOption
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string? FfmpegEncoder { get; init; }
}
