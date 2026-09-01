using System;
using System.Collections.Generic;

namespace TidyMemo.Models;

public enum SlideshowType { Basic, Background }
public enum SlideshowBackgroundType { SolidColor, Image, Gradient }
public enum SlideshowGradientDirection { TopToBottom, LeftToRight, Diagonal, Radial }
public enum SlideshowSortMode { ExifDate, FileDate, FileName, NaturalFileName }

public sealed record SlideshowResolution(string Name, int Width, int Height)
{
    public override string ToString() => $"{Name} ({Width} x {Height})";
}

public sealed class SlideshowOptions
{
    public required IReadOnlyList<string> Images { get; init; }
    public required string OutputFile { get; init; }
    public required string FfmpegPath { get; init; }
    public string? AudioFile { get; init; }
    public double ImageDuration { get; init; } = 3;
    public int FrameRate { get; init; } = 30;
    public int Width { get; init; } = 1920;
    public int Height { get; init; } = 1080;
    public int Quality { get; init; } = 18;
    public string EncoderPreset { get; init; } = "medium";
    public double Volume { get; init; } = 0.6;
    public SlideshowType Type { get; init; }
    public SlideshowBackgroundType BackgroundType { get; init; }
    public string BackgroundColor { get; init; } = "#000000";
    public string GradientEndColor { get; init; } = "#303060";
    public SlideshowGradientDirection GradientDirection { get; init; }
    public string? BackgroundImage { get; init; }
    public double ImageScaling { get; init; } = 0.8;
    public bool EnableBorder { get; init; }
    public int BorderWidth { get; init; } = 6;
    public string BorderColor { get; init; } = "#FFFFFF";
    public bool EnableShadow { get; init; }
    public int ShadowOffsetX { get; init; } = 12;
    public int ShadowOffsetY { get; init; } = 12;
    public int ShadowBlur { get; init; } = 18;
    public double ShadowOpacity { get; init; } = 0.45;
    public bool UseEnhancedBackgroundProcessing { get; init; } = true;
    public bool PreferImageMagick { get; init; }
    public string ImageMagickPath { get; init; } = "magick";
    public TransitionMode TransitionMode { get; init; } = TransitionMode.None;
    public string TransitionId { get; init; } = "fade";
    public double TransitionDuration { get; init; } = 0.8;
    public PhotoMotionMode MotionMode { get; init; } = PhotoMotionMode.None;
    public string MotionId { get; init; } = "none";
    public MotionIntensity MotionIntensity { get; init; } = MotionIntensity.Normal;
    public MotionEasing MotionEasing { get; init; } = MotionEasing.EaseInOut;
    public int RandomSeed { get; init; } = 1;
}

public sealed record SlideshowProgress(double Percentage, string Message);

public sealed record SlideshowResult(bool Success, string? ErrorMessage, string? OutputFile)
{
    public static SlideshowResult Failed(string message) => new(false, message, null);
}
