using System;
using System.Collections.Generic;
using System.Linq;
using TidyMemo.Models;
using TidyMemo.Services;
using Xunit;

namespace TidyMemo.Tests;

public sealed class TransitionTests
{
    [Fact]
    public void FadeMapsToNativeFfmpegName()
    {
        Assert.Equal("fade", TransitionCatalog.Fade.FfmpegName);
        Assert.Equal(58, TransitionCatalog.All.Count);
    }

    [Fact]
    public void TimelineCalculatesOffsetsAndTotalOverlap()
    {
        var timeline = SlideshowTimeline.Create(3, 5, new[] { 1d, 1d });

        Assert.Equal(new[] { 4d, 8d }, timeline.TransitionOffsets);
        Assert.Equal(13d, timeline.TotalDuration);
    }

    [Fact]
    public void TenFiveSecondPhotosWithOneSecondTransitionsLastFortyOneSeconds()
    {
        var timeline = SlideshowTimeline.Create(10, 5, Enumerable.Repeat(1d, 9).ToArray());

        Assert.Equal(41d, timeline.TotalDuration);
    }

    [Fact]
    public void GraphContainsChainedXfadeParameters()
    {
        var graph = TransitionGraphBuilder.Build(3, 5, 1, 30,
            new[] { TransitionCatalog.Fade, TransitionCatalog.Find("dissolve")! });

        Assert.Contains("[0:v][1:v]xfade=transition=fade:duration=1:offset=4[xf1]", graph);
        Assert.Contains("[xf1][2:v]xfade=transition=dissolve:duration=1:offset=8[video]", graph);
    }

    [Fact]
    public void RandomDoesNotRepeatImmediately()
    {
        var available = new HashSet<string>(TransitionCatalog.All.Select(x => x.FfmpegName));
        var selected = TransitionGraphBuilder.SelectTransitions(100, TransitionMode.Random,
            TransitionCatalog.Fade, available, seed: 42);

        Assert.All(selected.Zip(selected.Skip(1)), pair => Assert.NotEqual(pair.First.Id, pair.Second.Id));
    }

    [Fact]
    public void ExistingOptionsDefaultToNone()
    {
        var options = new SlideshowOptions
        {
            Images = new[] { "photo.jpg" }, OutputFile = "video.mp4", FfmpegPath = "ffmpeg"
        };

        Assert.Equal(TransitionMode.None, options.TransitionMode);
        Assert.DoesNotContain("xfade", string.Empty);
    }

    [Fact]
    public void TransitionMustBeShorterThanPhoto()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SlideshowTimeline.Create(2, 1, new[] { 1d }));
    }
}
