using System;
using System.Globalization;
using TidyMemo.Models;

namespace TidyMemo.Services;

public static class MotionExpressionBuilder
{
    public static string Build(PhotoMotionDefinition motion, MotionIntensity intensity,
        MotionEasing easing, double duration, int frameRate, int width, int height)
    {
        if (motion.Id == PhotoMotionCatalog.None.Id) return string.Empty;
        if (duration <= 0 || frameRate <= 0 || width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(duration));
        var frames = Math.Max(1, (int)Math.Round(duration * frameRate));
        var (start, end) = ApplyIntensity(motion, intensity);
        var progress = EasedProgress(easing, frames);
        var zoom = Lerp(start.Zoom, end.Zoom, progress);
        var fx = Lerp(start.Focus.X, end.Focus.X, progress);
        var fy = Lerp(start.Focus.Y, end.Focus.Y, progress);
        var x = $"(iw-iw/zoom)*({fx})";
        var y = $"(ih-ih/zoom)*({fy})";
        return $"zoompan=z='{Escape(zoom)}':x='{Escape(x)}':y='{Escape(y)}':d={frames}:s={width}x{height}:fps={frameRate}";
    }

    public static (PhotoTransform Start, PhotoTransform End) ApplyIntensity(
        PhotoMotionDefinition motion, MotionIntensity intensity)
    {
        var factor = intensity switch { MotionIntensity.Subtle => .55, MotionIntensity.Strong => 1.45, _ => 1 };
        PhotoTransform Scale(PhotoTransform value)
        {
            var zoom = 1 + (value.Zoom - 1) * factor;
            var focus = new NormalizedPoint(.5 + (value.Focus.X - .5) * factor,
                .5 + (value.Focus.Y - .5) * factor).Clamp();
            return new(Math.Max(1, zoom), focus);
        }
        return (Scale(motion.Start), Scale(motion.End));
    }

    private static string EasedProgress(MotionEasing easing, int frames)
    {
        var p = frames <= 1 ? "1" : $"min(1,max(0,on/{frames - 1}.0))";
        return easing switch
        {
            MotionEasing.EaseIn => $"pow({p},2)",
            MotionEasing.EaseOut => $"1-pow(1-({p}),2)",
            MotionEasing.EaseInOut => $"3*pow({p},2)-2*pow({p},3)",
            _ => p
        };
    }

    private static string Lerp(double start, double end, string p) =>
        Math.Abs(end - start) < 0.0000001 ? F(start) : $"{F(start)}+({F(end - start)})*({p})";
    private static string F(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    private static string Escape(string expression) => expression.Replace(",", "\\,");
}
