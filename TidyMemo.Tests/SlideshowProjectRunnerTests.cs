using System;
using System.IO;
using TidyMemo.Models;
using TidyMemo.Services;
using Xunit;

namespace TidyMemo.Tests;

public sealed class SlideshowProjectRunnerTests
{
    [Fact]
    public void BuildOptionsResolvesProjectPathsAndAllRenderSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tidymemo-runner-{Guid.NewGuid():N}");
        var projectPath = Path.Combine(directory, "show.slidetune");
        var project = new SlideshowProject
        {
            Slides =
            [
                new SlideshowSlide { Path = Path.Combine("photos", "one.jpg") },
                new SlideshowSlide { Path = Path.Combine("photos", "disabled.jpg"), Enabled = false }
            ],
            Presentation = new SlideshowPresentationSettings
            {
                Width = 3840, Height = 2160, ImageDuration = 4, Type = SlideshowType.Background,
                BackgroundType = SlideshowBackgroundType.Image, BackgroundImage = "background.jpg",
                TransitionMode = TransitionMode.Native, TransitionId = "dissolve", TransitionDuration = 1,
                MotionMode = PhotoMotionMode.Preset, MotionId = "slow-zoom-in",
                MotionIntensity = MotionIntensity.Strong, MotionEasing = MotionEasing.EaseOut, RandomSeed = 42
            },
            Audio = new SlideshowAudioSettings { Path = "soundtrack.mp3", Volume = .75 },
            Export = new SlideshowExportSettings
            {
                OutputFile = Path.Combine("output", "show.mp4"), FrameRate = 60, Quality = 16, EncoderPreset = "slow"
            }
        };

        var options = SlideshowProjectRunner.BuildOptions(project, projectPath, "ffmpeg.exe");

        Assert.Single(options.Images);
        Assert.Equal(Path.GetFullPath(Path.Combine(directory, "photos", "one.jpg")), options.Images[0]);
        Assert.Equal(Path.GetFullPath(Path.Combine(directory, "output", "show.mp4")), options.OutputFile);
        Assert.Equal(Path.GetFullPath(Path.Combine(directory, "background.jpg")), options.BackgroundImage);
        Assert.Equal(Path.GetFullPath(Path.Combine(directory, "soundtrack.mp3")), options.AudioFile);
        Assert.Equal(3840, options.Width);
        Assert.Equal(60, options.FrameRate);
        Assert.Equal("dissolve", options.TransitionId);
        Assert.Equal("slow-zoom-in", options.MotionId);
        Assert.Equal(42, options.RandomSeed);
    }

    [Fact]
    public void BuildOptionsRequiresOutputUnlessOverridden()
    {
        var project = new SlideshowProject();
        var projectPath = Path.Combine(Path.GetTempPath(), "show.slidetune");

        Assert.Throws<InvalidDataException>(() =>
            SlideshowProjectRunner.BuildOptions(project, projectPath, "ffmpeg.exe"));

        var options = SlideshowProjectRunner.BuildOptions(project, projectPath, "ffmpeg.exe", "override.mp4");
        Assert.Equal(Path.GetFullPath("override.mp4"), options.OutputFile);
    }
}
