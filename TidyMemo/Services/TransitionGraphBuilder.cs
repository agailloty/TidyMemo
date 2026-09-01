using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TidyMemo.Models;

namespace TidyMemo.Services;

public static class TransitionGraphBuilder
{
    public static string Build(int imageCount, double imageDuration, double transitionDuration,
        int frameRate, IReadOnlyList<TransitionDefinition> transitions)
    {
        if (transitions.Count != imageCount - 1) throw new ArgumentException("A transition is required for every image pair.");
        var timeline = SlideshowTimeline.Create(imageCount, imageDuration,
            Enumerable.Repeat(transitionDuration, Math.Max(0, imageCount - 1)).ToArray());
        var graph = new StringBuilder();
        var duration = F(transitionDuration);
        for (var i = 0; i < transitions.Count; i++)
        {
            if (i > 0) graph.Append(';');
            var left = i == 0 ? "0:v" : $"xf{i}";
            var output = i == transitions.Count - 1 ? "video" : $"xf{i + 1}";
            graph.Append($"[{left}][{i + 1}:v]xfade=transition={transitions[i].FfmpegName}:duration={duration}:offset={F(timeline.TransitionOffsets[i])}[{output}]");
        }
        return graph.ToString();
    }

    public static string BuildWithMotions(int imageCount, double imageDuration, double transitionDuration,
        int frameRate, int width, int height, IReadOnlyList<TransitionDefinition> transitions,
        IReadOnlyList<PhotoMotionDefinition> motions, MotionIntensity intensity, MotionEasing easing)
    {
        if (motions.Count != imageCount) throw new ArgumentException("A motion is required for every image.");
        if (transitions.Count != 0 && transitions.Count != imageCount - 1)
            throw new ArgumentException("A transition is required for every image pair.");
        var graph = new StringBuilder();
        for (var i = 0; i < imageCount; i++)
        {
            if (i > 0) graph.Append(';');
            var expression = MotionExpressionBuilder.Build(motions[i], intensity, easing,
                imageDuration, frameRate, width, height);
            graph.Append($"[{i}:v]");
            if (!string.IsNullOrEmpty(expression)) graph.Append(expression).Append(',');
            graph.Append($"fps={frameRate},scale={width}:{height},setsar=1,format=yuv420p[m{i}]");
        }
        if (imageCount == 1)
        {
            graph.Append(";[m0]null[video]");
            return graph.ToString();
        }
        if (transitions.Count == 0)
        {
            graph.Append(';');
            for (var i = 0; i < imageCount; i++) graph.Append($"[m{i}]");
            graph.Append($"concat=n={imageCount}:v=1:a=0[video]");
            return graph.ToString();
        }
        var timeline = SlideshowTimeline.Create(imageCount, imageDuration,
            Enumerable.Repeat(transitionDuration, imageCount - 1).ToArray());
        for (var i = 0; i < transitions.Count; i++)
        {
            graph.Append(';');
            var left = i == 0 ? "m0" : $"xf{i}";
            var output = i == transitions.Count - 1 ? "video" : $"xf{i + 1}";
            graph.Append($"[{left}][m{i + 1}]xfade=transition={transitions[i].FfmpegName}:duration={F(transitionDuration)}:offset={F(timeline.TransitionOffsets[i])}[{output}]");
        }
        return graph.ToString();
    }

    public static string BuildSegmentConcat(int segmentCount, int frameRate, int width, int height)
    {
        if (segmentCount < 1) throw new ArgumentOutOfRangeException(nameof(segmentCount));
        var graph = new StringBuilder();
        for (var i = 0; i < segmentCount; i++)
        {
            if (i > 0) graph.Append(';');
            graph.Append($"[{i}:v]fps={frameRate},scale={width}:{height},setsar=1,format=yuv420p[s{i}]");
        }
        graph.Append(';');
        for (var i = 0; i < segmentCount; i++) graph.Append($"[s{i}]");
        graph.Append($"concat=n={segmentCount}:v=1:a=0[video]");
        return graph.ToString();
    }

    public static IReadOnlyList<TransitionDefinition> SelectTransitions(int count,
        TransitionMode mode, TransitionDefinition selected, IReadOnlyCollection<string> available, int? seed = null)
    {
        if (count <= 0) return Array.Empty<TransitionDefinition>();
        if (mode == TransitionMode.Native) return Enumerable.Repeat(selected, count).ToArray();
        var choices = TransitionCatalog.All.Where(x => available.Contains(x.FfmpegName, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (choices.Length == 0) throw new InvalidOperationException("No supported xfade transitions were reported by FFmpeg.");
        var random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        var result = new TransitionDefinition[count];
        for (var i = 0; i < count; i++)
        {
            var candidates = i == 0 || choices.Length == 1 ? choices : choices.Where(x => x.Id != result[i - 1].Id).ToArray();
            result[i] = candidates[random.Next(candidates.Length)];
        }
        return result;
    }

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
