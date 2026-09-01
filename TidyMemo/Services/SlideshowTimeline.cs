using System;
using System.Collections.Generic;
using System.Linq;

namespace TidyMemo.Services;

public sealed record SlideshowTimeline(IReadOnlyList<double> TransitionOffsets, double TotalDuration)
{
    public static SlideshowTimeline Create(int imageCount, double imageDuration, IReadOnlyList<double> transitionDurations)
    {
        if (imageCount < 1) throw new ArgumentOutOfRangeException(nameof(imageCount));
        if (!double.IsFinite(imageDuration) || imageDuration <= 0) throw new ArgumentOutOfRangeException(nameof(imageDuration));
        if (transitionDurations.Count != imageCount - 1)
            throw new ArgumentException("There must be one transition duration between adjacent images.", nameof(transitionDurations));

        var offsets = new double[transitionDurations.Count];
        var overlap = 0d;
        for (var i = 0; i < transitionDurations.Count; i++)
        {
            var duration = transitionDurations[i];
            if (!double.IsFinite(duration) || duration <= 0 || duration >= imageDuration)
                throw new ArgumentOutOfRangeException(nameof(transitionDurations), "Transition duration must be positive and shorter than image duration.");
            overlap += duration;
            offsets[i] = (i + 1) * imageDuration - overlap;
        }
        return new(offsets, imageCount * imageDuration - transitionDurations.Sum());
    }
}
