namespace TidyMemo.Models;

public enum VideoProcessingOperation
{
    Compress,
    SpeedUp,
    Convert,
    ExportGif,
    SpeedUpAndExportGif
}

public sealed class VideoProcessingOperationOption
{
    public required VideoProcessingOperation Operation { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
}

public sealed class VideoProcessingOptions
{
    public VideoProcessingOperation Operation { get; init; }
    public VideoCompressionPreset? CompressionPreset { get; init; }
    public double SpeedMultiplier { get; init; } = 2;
    public string OutputFormat { get; init; } = "mp4";
    public int GifWidth { get; init; } = 640;
    public int GifFps { get; init; } = 12;
}
