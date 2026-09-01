using System.Linq;
using TidyMemo.Models;
using TidyMemo.Services;
using Xunit;

namespace TidyMemo.Tests;

public sealed class PhotoMotionTests
{
    [Fact]
    public void ExistingOptionsDefaultToNoMotion()
    {
        var options = new SlideshowOptions { Images = ["photo.jpg"], OutputFile = "out.mp4", FfmpegPath = "ffmpeg" };
        Assert.Equal(PhotoMotionMode.None, options.MotionMode);
        Assert.Equal("none", options.MotionId);
    }

    [Fact]
    public void PanAndKenBurnsPresetsMapToGenericTransforms()
    {
        var pan = PhotoMotionCatalog.Find("pan-left-right")!;
        var kenBurns = PhotoMotionCatalog.Find("kb-in-right")!;
        Assert.True(pan.Start.Focus.X < pan.End.Focus.X);
        Assert.Equal(pan.Start.Zoom, pan.End.Zoom);
        Assert.NotEqual(kenBurns.Start.Zoom, kenBurns.End.Zoom);
        Assert.NotEqual(kenBurns.Start.Focus, kenBurns.End.Focus);
    }

    [Fact]
    public void StrongIntensityHasMoreZoomThanNormalAndSubtle()
    {
        var motion = PhotoMotionCatalog.Find("slow-zoom-in")!;
        var subtle = MotionExpressionBuilder.ApplyIntensity(motion, MotionIntensity.Subtle);
        var normal = MotionExpressionBuilder.ApplyIntensity(motion, MotionIntensity.Normal);
        var strong = MotionExpressionBuilder.ApplyIntensity(motion, MotionIntensity.Strong);
        Assert.True(strong.End.Zoom - strong.Start.Zoom > normal.End.Zoom - normal.Start.Zoom);
        Assert.True(normal.End.Zoom - normal.Start.Zoom > subtle.End.Zoom - subtle.Start.Zoom);
    }

    [Theory]
    [InlineData(2, 24, "on/47.0")]
    [InlineData(5, 30, "on/149.0")]
    [InlineData(15, 60, "on/899.0")]
    public void ExpressionsUseDurationAndFrameRate(double duration, int fps, string expectedProgress)
    {
        var graph = MotionExpressionBuilder.Build(PhotoMotionCatalog.Find("slow-zoom-in")!,
            MotionIntensity.Normal, MotionEasing.Linear, duration, fps, 1920, 1080);
        Assert.Contains(expectedProgress, graph);
        Assert.Contains($"d={(int)(duration * fps)}", graph);
        Assert.Contains($"fps={fps}", graph);
    }

    [Fact]
    public void RandomIsRepeatableAndAvoidsImmediatePresetAndCategoryRepetition()
    {
        var selected = PhotoMotionCatalog.Find("slow-zoom-in")!;
        var first = PhotoMotionSelector.Select(100, PhotoMotionMode.Random, selected, 42);
        var second = PhotoMotionSelector.Select(100, PhotoMotionMode.Random, selected, 42);
        Assert.Equal(first.Select(x => x.Id), second.Select(x => x.Id));
        Assert.All(first.Zip(first.Skip(1)), pair =>
        {
            Assert.NotEqual(pair.First.Id, pair.Second.Id);
            Assert.NotEqual(pair.First.Category, pair.Second.Category);
        });
    }

    [Fact]
    public void RandomSoftAndKenBurnsStayInTheirProfiles()
    {
        var selected = PhotoMotionCatalog.None;
        Assert.All(PhotoMotionSelector.Select(30, PhotoMotionMode.RandomSoft, selected, 7), x => Assert.True(x.IsSoft));
        Assert.All(PhotoMotionSelector.Select(30, PhotoMotionMode.RandomKenBurns, selected, 7), x => Assert.True(x.IsKenBurns));
    }

    [Fact]
    public void MotionBranchesAreBuiltBeforeXfade()
    {
        var graph = TransitionGraphBuilder.BuildWithMotions(2, 5, 1, 30, 1920, 1080,
            [TransitionCatalog.Find("dissolve")!],
            [PhotoMotionCatalog.Find("slow-zoom-in")!, PhotoMotionCatalog.Find("pan-right-left")!],
            MotionIntensity.Normal, MotionEasing.EaseInOut);
        Assert.Contains("[0:v]zoompan=", graph);
        Assert.Contains("[1:v]zoompan=", graph);
        Assert.Contains("[m0][m1]xfade=transition=dissolve:duration=1:offset=4[video]", graph);
        Assert.True(graph.IndexOf("zoompan=", System.StringComparison.Ordinal) <
                    graph.IndexOf("xfade=", System.StringComparison.Ordinal));
    }

    [Fact]
    public void PreRenderedMotionSegmentsCanBeConcatenatedWithoutZoompan()
    {
        var graph = TransitionGraphBuilder.BuildSegmentConcat(3, 30, 1920, 1080);
        Assert.DoesNotContain("zoompan", graph);
        Assert.Contains("[s0][s1][s2]concat=n=3:v=1:a=0[video]", graph);
    }
}
