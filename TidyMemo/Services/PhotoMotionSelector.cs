using System;
using System.Collections.Generic;
using System.Linq;
using TidyMemo.Models;

namespace TidyMemo.Services;

public static class PhotoMotionSelector
{
    public static IReadOnlyList<PhotoMotionDefinition> Select(int count, PhotoMotionMode mode,
        PhotoMotionDefinition selected, int seed)
    {
        if (count <= 0) return [];
        if (mode == PhotoMotionMode.None) return Enumerable.Repeat(PhotoMotionCatalog.None, count).ToArray();
        if (mode == PhotoMotionMode.Preset) return Enumerable.Repeat(selected, count).ToArray();
        var choices = PhotoMotionCatalog.All.Where(x => x.Id != "none" && mode switch
        {
            PhotoMotionMode.RandomSoft => x.IsSoft,
            PhotoMotionMode.RandomKenBurns => x.IsKenBurns,
            _ => true
        }).ToArray();
        var random = new Random(seed);
        var result = new PhotoMotionDefinition[count];
        for (var i = 0; i < count; i++)
        {
            var candidates = i == 0 || choices.Length == 1
                ? choices
                : choices.Where(x => x.Id != result[i - 1].Id && x.Category != result[i - 1].Category).ToArray();
            if (candidates.Length == 0) candidates = choices.Where(x => x.Id != result[i - 1].Id).ToArray();
            result[i] = candidates[random.Next(candidates.Length)];
        }
        return result;
    }
}
