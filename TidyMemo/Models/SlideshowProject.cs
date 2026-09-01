using System;
using System.Collections.Generic;

namespace TidyMemo.Models;

public sealed class SlideshowProject
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Untitled slideshow";
    public List<SlideshowSource> Sources { get; set; } = [];
    public List<SlideshowSlide> Slides { get; set; } = [];
    public SlideshowPresentationSettings Presentation { get; set; } = new();
    public SlideshowAudioSettings Audio { get; set; } = new();
    public SlideshowExportSettings Export { get; set; } = new();
}

public sealed class SlideshowSource
{
    public string Path { get; set; } = string.Empty;
    public bool IncludeSubfolders { get; set; }
}

public sealed class SlideshowSlide
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Path { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public double? Duration { get; set; }
    public string? TransitionId { get; set; }
    public string? MotionId { get; set; }
}

public sealed class SlideshowPresentationSettings
{
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public double ImageDuration { get; set; } = 3;
    public SlideshowSortMode SortMode { get; set; } = SlideshowSortMode.NaturalFileName;
    public bool IncludeSubfolders { get; set; } = true;
    public SlideshowType Type { get; set; }
    public SlideshowBackgroundType BackgroundType { get; set; }
    public string BackgroundColor { get; set; } = "#000000";
    public string GradientEndColor { get; set; } = "#303060";
    public SlideshowGradientDirection GradientDirection { get; set; }
    public string? BackgroundImage { get; set; }
    public double ImageScaling { get; set; } = 0.8;
    public bool EnableBorder { get; set; }
    public int BorderWidth { get; set; } = 6;
    public string BorderColor { get; set; } = "#FFFFFF";
    public bool EnableShadow { get; set; }
    public int ShadowOffsetX { get; set; } = 12;
    public int ShadowOffsetY { get; set; } = 12;
    public int ShadowBlur { get; set; } = 18;
    public double ShadowOpacity { get; set; } = 0.45;
    public bool UseEnhancedBackgroundProcessing { get; set; } = true;
    public bool PreferImageMagick { get; set; }
    public string ImageMagickPath { get; set; } = "magick";
    public TransitionMode TransitionMode { get; set; }
    public string TransitionId { get; set; } = "fade";
    public double TransitionDuration { get; set; } = 0.8;
    public PhotoMotionMode MotionMode { get; set; }
    public string MotionId { get; set; } = "none";
    public MotionIntensity MotionIntensity { get; set; } = MotionIntensity.Normal;
    public MotionEasing MotionEasing { get; set; } = MotionEasing.EaseInOut;
    public int RandomSeed { get; set; } = 1;
}

public sealed class SlideshowAudioSettings
{
    public string? Path { get; set; }
    public double Volume { get; set; } = 0.6;
}

public sealed class SlideshowExportSettings
{
    public string? OutputFile { get; set; }
    public int FrameRate { get; set; } = 30;
    public int Quality { get; set; } = 18;
    public string EncoderPreset { get; set; } = "medium";
}
