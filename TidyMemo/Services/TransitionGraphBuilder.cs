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
