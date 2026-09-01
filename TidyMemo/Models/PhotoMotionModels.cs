using System;
using System.Collections.Generic;
using System.Linq;

namespace TidyMemo.Models;

public enum MotionIntensity { Subtle, Normal, Strong }
public enum MotionEasing { Linear, EaseIn, EaseOut, EaseInOut }
public enum PhotoMotionMode { None, Preset, Random, RandomSoft, RandomKenBurns }

public readonly record struct NormalizedPoint(double X, double Y)
{
    public NormalizedPoint Clamp() => new(Math.Clamp(X, 0, 1), Math.Clamp(Y, 0, 1));
}

public readonly record struct PhotoTransform(double Zoom, NormalizedPoint Focus);

public sealed record PhotoMotionDefinition(
    string Id,
    string DisplayName,
    string Description,
    string Category,
    PhotoTransform Start,
    PhotoTransform End,
    IReadOnlyList<string> RequiredFilters,
    bool IsSoft = false,
    bool IsKenBurns = false)
{
    public string DisplayLabel => $"{Category} — {DisplayName}";
    public override string ToString() => DisplayLabel;
}

public static class PhotoMotionCatalog
{
    private static readonly NormalizedPoint Center = new(.5, .5);
    private static PhotoMotionDefinition M(string id, string name, string category, string description,
        double startZoom, double endZoom, double sx, double sy, double ex, double ey,
        bool soft = false, bool kenBurns = false) =>
        new(id, name, description, category,
            new(startZoom, new(sx, sy)), new(endZoom, new(ex, ey)), ["zoompan"], soft, kenBurns);

    public static PhotoMotionDefinition None { get; } =
        new("none", "None", "No movement.", "None", new(1, Center), new(1, Center), []);

    public static IReadOnlyList<PhotoMotionDefinition> All { get; } = new[]
    {
        None,
        M("slow-zoom-in", "Slow Zoom In", "Zoom", "A gentle centered zoom in.", 1, 1.08, .5, .5, .5, .5, true),
        M("slow-zoom-out", "Slow Zoom Out", "Zoom", "A gentle centered zoom out.", 1.08, 1, .5, .5, .5, .5, true),
        M("push-in", "Push In", "Zoom", "A more cinematic push toward the subject.", 1, 1.13, .5, .5, .5, .5),
        M("pull-out", "Pull Out", "Zoom", "A cinematic pull away from the subject.", 1.13, 1, .5, .5, .5, .5),
        M("pan-left-right", "Left to Right", "Pan", "Moves the viewport from left to right.", 1.12, 1.12, 0, .5, 1, .5),
        M("pan-right-left", "Right to Left", "Pan", "Moves the viewport from right to left.", 1.12, 1.12, 1, .5, 0, .5),
        M("pan-top-bottom", "Top to Bottom", "Pan", "Moves the viewport downward.", 1.12, 1.12, .5, 0, .5, 1),
        M("pan-bottom-top", "Bottom to Top", "Pan", "Moves the viewport upward.", 1.12, 1.12, .5, 1, .5, 0),
        M("pan-tl-br", "Top Left to Bottom Right", "Diagonal", "Diagonal pan.", 1.12, 1.12, 0, 0, 1, 1),
        M("pan-tr-bl", "Top Right to Bottom Left", "Diagonal", "Diagonal pan.", 1.12, 1.12, 1, 0, 0, 1),
        M("pan-bl-tr", "Bottom Left to Top Right", "Diagonal", "Diagonal pan.", 1.12, 1.12, 0, 1, 1, 0),
        M("pan-br-tl", "Bottom Right to Top Left", "Diagonal", "Diagonal pan.", 1.12, 1.12, 1, 1, 0, 0),
        M("kb-in-left", "Zoom In + Left", "Ken Burns", "Zooms in while moving left.", 1, 1.12, .75, .5, .2, .5, true, true),
        M("kb-in-right", "Zoom In + Right", "Ken Burns", "Zooms in while moving right.", 1, 1.12, .25, .5, .8, .5, true, true),
        M("kb-in-up", "Zoom In + Up", "Ken Burns", "Zooms in while moving upward.", 1, 1.12, .5, .75, .5, .2, true, true),
        M("kb-in-down", "Zoom In + Down", "Ken Burns", "Zooms in while moving downward.", 1, 1.12, .5, .25, .5, .8, true, true),
        M("kb-out-left", "Zoom Out + Left", "Ken Burns", "Zooms out while moving left.", 1.12, 1, .8, .5, .2, .5, true, true),
        M("kb-out-right", "Zoom Out + Right", "Ken Burns", "Zooms out while moving right.", 1.12, 1, .2, .5, .8, .5, true, true),
        M("kb-out-up", "Zoom Out + Up", "Ken Burns", "Zooms out while moving upward.", 1.12, 1, .5, .8, .5, .2, true, true),
        M("kb-out-down", "Zoom Out + Down", "Ken Burns", "Zooms out while moving downward.", 1.12, 1, .5, .2, .5, .8, true, true),
        M("kb-in-tl-br", "Zoom In — Top Left to Bottom Right", "Ken Burns", "Diagonal Ken Burns move.", 1, 1.12, .15, .15, .85, .85, false, true),
        M("kb-in-tr-bl", "Zoom In — Top Right to Bottom Left", "Ken Burns", "Diagonal Ken Burns move.", 1, 1.12, .85, .15, .15, .85, false, true),
        M("kb-out-bl-tr", "Zoom Out — Bottom Left to Top Right", "Ken Burns", "Diagonal Ken Burns move.", 1.12, 1, .15, .85, .85, .15, false, true),
        M("kb-out-br-tl", "Zoom Out — Bottom Right to Top Left", "Ken Burns", "Diagonal Ken Burns move.", 1.12, 1, .85, .85, .15, .15, false, true),
        M("drift-left", "Drift Left", "Subtle", "A very small movement to the left.", 1.06, 1.06, .65, .5, .35, .5, true),
        M("drift-right", "Drift Right", "Subtle", "A very small movement to the right.", 1.06, 1.06, .35, .5, .65, .5, true),
        M("drift-up", "Drift Up", "Subtle", "A very small upward movement.", 1.06, 1.06, .5, .65, .5, .35, true),
        M("drift-down", "Drift Down", "Subtle", "A very small downward movement.", 1.06, 1.06, .5, .35, .5, .65, true)
    };

    public static PhotoMotionDefinition? Find(string? id) =>
        All.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
