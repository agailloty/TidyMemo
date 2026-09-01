using System;
using System.Collections.Generic;
using System.Linq;

namespace TidyMemo.Models;

public enum TransitionMode { None, Native, Random }

public sealed record TransitionDefinition(
    string Id,
    string DisplayName,
    string FfmpegName,
    string Category,
    string Description,
    bool SupportsDuration = true)
{
    public string DisplayLabel => $"{Category} — {DisplayName}";
    public override string ToString() => DisplayLabel;
}

public static class TransitionCatalog
{
    private static TransitionDefinition T(string ffmpeg, string name, string category, string description) =>
        new(ffmpeg, name, ffmpeg, category, description);

    public static IReadOnlyList<TransitionDefinition> All { get; } = new[]
    {
        T("fade", "Fade", "Basic", "A classic cross-fade."),
        T("dissolve", "Dissolve", "Basic", "Dissolves one image into the next."),
        T("fadeblack", "Fade through black", "Basic", "Fades through black."),
        T("fadewhite", "Fade through white", "Basic", "Fades through white."),
        T("fadegrays", "Fade through grayscale", "Basic", "Fades through grayscale tones."),
        T("fadefast", "Fast fade", "Basic", "A fast-shaped fade."),
        T("fadeslow", "Slow fade", "Basic", "A slow-shaped fade."),
        T("slideleft", "Slide left", "Slide", "Slides toward the left."),
        T("slideright", "Slide right", "Slide", "Slides toward the right."),
        T("slideup", "Slide up", "Slide", "Slides upward."),
        T("slidedown", "Slide down", "Slide", "Slides downward."),
        T("smoothleft", "Smooth left", "Slide", "A smooth left slide."),
        T("smoothright", "Smooth right", "Slide", "A smooth right slide."),
        T("smoothup", "Smooth up", "Slide", "A smooth upward slide."),
        T("smoothdown", "Smooth down", "Slide", "A smooth downward slide."),
        T("wipeleft", "Wipe left", "Wipe", "Wipes toward the left."),
        T("wiperight", "Wipe right", "Wipe", "Wipes toward the right."),
        T("wipeup", "Wipe up", "Wipe", "Wipes upward."),
        T("wipedown", "Wipe down", "Wipe", "Wipes downward."),
        T("wipetl", "Wipe top-left", "Wipe", "Diagonal wipe from the top-left."),
        T("wipetr", "Wipe top-right", "Wipe", "Diagonal wipe from the top-right."),
        T("wipebl", "Wipe bottom-left", "Wipe", "Diagonal wipe from the bottom-left."),
        T("wipebr", "Wipe bottom-right", "Wipe", "Diagonal wipe from the bottom-right."),
        T("circlecrop", "Circle crop", "Geometric", "Circular crop transition."),
        T("rectcrop", "Rectangle crop", "Geometric", "Rectangular crop transition."),
        T("distance", "Distance", "Geometric", "Distance-field transition."),
        T("radial", "Radial", "Geometric", "Radial sweep transition."),
        T("circleopen", "Circle open", "Geometric", "Opens a circular reveal."),
        T("circleclose", "Circle close", "Geometric", "Closes a circular reveal."),
        T("vertopen", "Vertical open", "Geometric", "Opens vertically."),
        T("vertclose", "Vertical close", "Geometric", "Closes vertically."),
        T("horzopen", "Horizontal open", "Geometric", "Opens horizontally."),
        T("horzclose", "Horizontal close", "Geometric", "Closes horizontally."),
        T("diagtl", "Diagonal top-left", "Geometric", "Diagonal transition toward top-left."),
        T("diagtr", "Diagonal top-right", "Geometric", "Diagonal transition toward top-right."),
        T("diagbl", "Diagonal bottom-left", "Geometric", "Diagonal transition toward bottom-left."),
        T("diagbr", "Diagonal bottom-right", "Geometric", "Diagonal transition toward bottom-right."),
        T("pixelize", "Pixelize", "Dynamic", "Pixelates during the transition."),
        T("hblur", "Horizontal blur", "Dynamic", "Blurs horizontally."),
        T("squeezeh", "Horizontal squeeze", "Dynamic", "Squeezes horizontally."),
        T("squeezev", "Vertical squeeze", "Dynamic", "Squeezes vertically."),
        T("zoomin", "Zoom in", "Dynamic", "Zooms into the next image."),
        T("hlslice", "Horizontal slice left", "Dynamic", "Horizontal sliced movement left."),
        T("hrslice", "Horizontal slice right", "Dynamic", "Horizontal sliced movement right."),
        T("vuslice", "Vertical slice up", "Dynamic", "Vertical sliced movement up."),
        T("vdslice", "Vertical slice down", "Dynamic", "Vertical sliced movement down."),
        T("hlwind", "Horizontal wind left", "Dynamic", "Wind effect toward the left."),
        T("hrwind", "Horizontal wind right", "Dynamic", "Wind effect toward the right."),
        T("vuwind", "Vertical wind up", "Dynamic", "Wind effect upward."),
        T("vdwind", "Vertical wind down", "Dynamic", "Wind effect downward."),
        T("coverleft", "Cover left", "Reveal / Cover", "The next image covers from the right."),
        T("coverright", "Cover right", "Reveal / Cover", "The next image covers from the left."),
        T("coverup", "Cover up", "Reveal / Cover", "The next image covers upward."),
        T("coverdown", "Cover down", "Reveal / Cover", "The next image covers downward."),
        T("revealleft", "Reveal left", "Reveal / Cover", "Reveals toward the left."),
        T("revealright", "Reveal right", "Reveal / Cover", "Reveals toward the right."),
        T("revealup", "Reveal up", "Reveal / Cover", "Reveals upward."),
        T("revealdown", "Reveal down", "Reveal / Cover", "Reveals downward.")
    };

    public static TransitionDefinition Fade => All.First(x => x.Id == "fade");
    public static TransitionDefinition? Find(string id) =>
        All.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
