using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TidyMemo.Models;

namespace TidyMemo.Services;

public sealed class SlideshowService
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp" };

    public bool IsSupportedImage(string path) => Extensions.Contains(Path.GetExtension(path));

    public IReadOnlyList<string> GetImages(string folder, bool includeSubfolders) =>
        Directory.EnumerateFiles(folder, "*", includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(IsSupportedImage).ToArray();

    public async Task<SlideshowResult> CreateAsync(
        SlideshowOptions options, IProgress<SlideshowProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(options);
        if (validation is not null) return SlideshowResult.Failed(validation);

        var tempRoot = Path.Combine(Path.GetTempPath(), "TidyMemo", $"slidetune-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            IReadOnlyList<string> images = options.Images;
            var useImageMagick = options.Type == SlideshowType.Background &&
                                 options.BackgroundType == SlideshowBackgroundType.Image &&
                                 options.UseEnhancedBackgroundProcessing && options.PreferImageMagick &&
                                 !options.EnableBorder && !options.EnableShadow;
            if (useImageMagick)
            {
                progress?.Report(new SlideshowProgress(0, "Compositing images with ImageMagick..."));
                images = await CompositeWithImageMagickAsync(options, tempRoot, progress, cancellationToken);
            }
            else
            {
                progress?.Report(new SlideshowProgress(0, "Preparing images..."));
                images = await NormalizeImagesAsync(options, tempRoot, progress, cancellationToken);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(options.OutputFile)!);

            ProcessStartInfo startInfo;
            double totalDuration;
            var hasMotion = options.MotionMode != PhotoMotionMode.None;
            if (!hasMotion && (options.TransitionMode == TransitionMode.None || images.Count == 1))
            {
                var manifest = Path.Combine(tempRoot, "images.ffconcat");
                await WriteManifestAsync(manifest, images, options.ImageDuration, cancellationToken);
                startInfo = BuildFfmpegStartInfo(options, manifest, useImageMagick);
                totalDuration = images.Count * options.ImageDuration;
            }
            else
            {
                progress?.Report(new SlideshowProgress(20, "Checking FFmpeg motion and transition support..."));
                var capabilities = await new FfmpegCapabilitiesService().DetectAsync(options.FfmpegPath, cancellationToken);
                if (options.TransitionMode != TransitionMode.None && images.Count > 1 && !capabilities.HasXfade)
                    return SlideshowResult.Failed("This FFmpeg executable does not provide the xfade video filter. " + LastUsefulError(capabilities.Diagnostic));
                if (hasMotion && !capabilities.HasZoompan)
                    return SlideshowResult.Failed("This FFmpeg executable does not provide the zoompan video filter required by Photo Motion.");
                var selected = TransitionCatalog.Find(options.TransitionId) ?? TransitionCatalog.Fade;
                if (images.Count > 1 && options.TransitionMode == TransitionMode.Native && !capabilities.XfadeTransitions.Contains(selected.FfmpegName))
                    return SlideshowResult.Failed($"FFmpeg does not support the '{selected.DisplayName}' xfade transition ({selected.FfmpegName}).");

                var slides = await RenderFinalSlidesAsync(options, images, tempRoot, useImageMagick, progress, cancellationToken);
                var transitions = options.TransitionMode == TransitionMode.None || slides.Count == 1
                    ? Array.Empty<TransitionDefinition>()
                    : TransitionGraphBuilder.SelectTransitions(slides.Count - 1, options.TransitionMode,
                        selected, capabilities.XfadeTransitions, options.RandomSeed);
                var motion = PhotoMotionCatalog.Find(options.MotionId) ?? PhotoMotionCatalog.None;
                var motions = PhotoMotionSelector.Select(slides.Count, options.MotionMode, motion, options.RandomSeed);
                IReadOnlyList<string> renderInputs = slides;
                if (hasMotion)
                {
                    progress?.Report(new SlideshowProgress(25, "Rendering motion segments..."));
                    renderInputs = await RenderMotionSegmentsAsync(options, slides, motions, tempRoot,
                        progress, cancellationToken);
                }
                totalDuration = transitions.Count == 0
                    ? slides.Count * options.ImageDuration
                    : SlideshowTimeline.Create(slides.Count, options.ImageDuration,
                        Enumerable.Repeat(options.TransitionDuration, slides.Count - 1).ToArray()).TotalDuration;
                startInfo = hasMotion
                    ? BuildSegmentAssemblyStartInfo(options, renderInputs, transitions, totalDuration)
                    : BuildTransitionStartInfo(options, slides, transitions, totalDuration);
            }
            return await RunFfmpegAsync(startInfo, options.OutputFile, totalDuration, progress, cancellationToken,
                hasMotion ? 70 : 0);
        }
        catch (OperationCanceledException)
        {
            TryDeleteIncompleteOutput(options.OutputFile);
            return SlideshowResult.Failed("Slideshow creation cancelled.");
        }
        catch (Exception ex)
        {
            TryDeleteIncompleteOutput(options.OutputFile);
            return SlideshowResult.Failed(ex.Message);
        }
        finally
        {
            TryDeleteTemporaryFolder(tempRoot);
        }
    }

    private static string? Validate(SlideshowOptions o)
    {
        if (!File.Exists(o.FfmpegPath)) return "FFmpeg is not configured or cannot be found.";
        if (o.Images.Count == 0) return "Add at least one image.";
        if (o.Images.Any(path => !File.Exists(path))) return "One or more source images no longer exist.";
        if (o.ImageDuration <= 0) return "Image duration must be greater than zero.";
        if (o.Width <= 0 || o.Height <= 0 || o.FrameRate <= 0) return "Resolution and frame rate must be positive.";
        if (o.Quality is < 0 or > 51) return "CRF quality must be between 0 and 51.";
        if (o.Volume is < 0 or > 1) return "Audio volume must be between 0 and 1.";
        if (o.ImageScaling is <= 0 or > 1) return "Image scaling must be between 0 and 1.";
        if (o.BorderWidth is < 0 or > 200) return "Border width must be between 0 and 200 pixels.";
        if (o.ShadowOffsetX is < -200 or > 200 || o.ShadowOffsetY is < -200 or > 200)
            return "Shadow offsets must be between -200 and 200 pixels.";
        if (o.ShadowBlur is < 0 or > 100) return "Shadow blur must be between 0 and 100 pixels.";
        if (o.ShadowOpacity is < 0 or > 1) return "Shadow opacity must be between 0 and 1.";
        if (o.TransitionMode != TransitionMode.None && (o.TransitionDuration is < 0.1 or > 3))
            return "Transition duration must be between 0.1 and 3 seconds.";
        if (o.TransitionMode != TransitionMode.None && o.Images.Count > 1 && o.TransitionDuration >= o.ImageDuration)
            return "Transition duration must be shorter than the duration of each image.";
        if (o.EnableBorder && !IsValidColor(o.BorderColor)) return "Border color must use the #RRGGBB format.";
        if (!string.IsNullOrWhiteSpace(o.AudioFile) && !File.Exists(o.AudioFile)) return "The selected audio file cannot be found.";
        if (o.Type == SlideshowType.Background && o.BackgroundType == SlideshowBackgroundType.Image && !File.Exists(o.BackgroundImage))
            return "Select a valid background image.";
        return null;
    }

    private static async Task WriteManifestAsync(string path, IReadOnlyList<string> images, double duration, CancellationToken token)
    {
        var seconds = duration.ToString("0.###", CultureInfo.InvariantCulture);
        var lines = new List<string> { "ffconcat version 1.0" };
        foreach (var image in images)
        {
            lines.Add($"file '{EscapeConcatPath(Path.GetFullPath(image))}'");
            lines.Add($"duration {seconds}");
        }
        lines.Add($"file '{EscapeConcatPath(Path.GetFullPath(images[^1]))}'");
        await File.WriteAllLinesAsync(path, lines, new UTF8Encoding(false), token);
    }

    private static string EscapeConcatPath(string path) => path.Replace("\\", "/").Replace("'", "'\\''");

    private static ProcessStartInfo BuildFfmpegStartInfo(SlideshowOptions o, string manifest, bool alreadyComposited)
    {
        var psi = NewProcess(o.FfmpegPath);
        Add(psi, "-hide_banner", "-f", "concat", "-safe", "0", "-i", manifest);
        var backgroundInput = o.Type == SlideshowType.Background &&
                              o.BackgroundType == SlideshowBackgroundType.Image && !alreadyComposited;
        if (backgroundInput) Add(psi, "-loop", "1", "-i", o.BackgroundImage!);
        var audioInputIndex = backgroundInput ? 2 : 1;
        if (!string.IsNullOrWhiteSpace(o.AudioFile)) Add(psi, "-stream_loop", "-1", "-i", o.AudioFile!);

        Add(psi, "-filter_complex", BuildFilter(o, backgroundInput, alreadyComposited));
        Add(psi, "-map", "[video]");
        if (!string.IsNullOrWhiteSpace(o.AudioFile))
            Add(psi, "-map", $"{audioInputIndex}:a:0", "-af", $"volume={o.Volume.ToString("0.###", CultureInfo.InvariantCulture)}", "-c:a", "aac", "-b:a", "192k", "-shortest");
        Add(psi, "-r", o.FrameRate.ToString(CultureInfo.InvariantCulture), "-c:v", "libx264", "-crf",
            o.Quality.ToString(CultureInfo.InvariantCulture), "-preset", o.EncoderPreset,
            "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-progress", "pipe:1", "-nostats", "-y", o.OutputFile);
        return psi;
    }

    private static ProcessStartInfo BuildSegmentAssemblyStartInfo(SlideshowOptions o,
        IReadOnlyList<string> segments, IReadOnlyList<TransitionDefinition> transitions, double totalDuration)
    {
        var psi = NewProcess(o.FfmpegPath);
        Add(psi, "-hide_banner");
        foreach (var segment in segments)
            Add(psi, "-i", segment);
        if (!string.IsNullOrWhiteSpace(o.AudioFile)) Add(psi, "-stream_loop", "-1", "-i", o.AudioFile!);
        var graph = transitions.Count == 0
            ? TransitionGraphBuilder.BuildSegmentConcat(segments.Count, o.FrameRate, o.Width, o.Height)
            : TransitionGraphBuilder.Build(segments.Count, o.ImageDuration, o.TransitionDuration,
                o.FrameRate, transitions);
        Add(psi, "-filter_complex", graph, "-map", "[video]");
        if (!string.IsNullOrWhiteSpace(o.AudioFile))
            Add(psi, "-map", $"{segments.Count}:a:0", "-af", $"volume={o.Volume.ToString("0.###", CultureInfo.InvariantCulture)}",
                "-c:a", "aac", "-b:a", "192k");
        Add(psi, "-t", totalDuration.ToString("0.###", CultureInfo.InvariantCulture), "-c:v", "libx264", "-crf",
            o.Quality.ToString(CultureInfo.InvariantCulture), "-preset", o.EncoderPreset, "-pix_fmt", "yuv420p",
            "-movflags", "+faststart", "-progress", "pipe:1", "-nostats", "-y", o.OutputFile);
        return psi;
    }

    private static async Task<IReadOnlyList<string>> RenderMotionSegmentsAsync(SlideshowOptions o,
        IReadOnlyList<string> slides, IReadOnlyList<PhotoMotionDefinition> motions, string tempRoot,
        IProgress<SlideshowProgress>? progress, CancellationToken token)
    {
        var output = Path.Combine(tempRoot, "motion-segments");
        Directory.CreateDirectory(output);
        var result = new string[slides.Count];
        var frames = Math.Max(1, (int)Math.Round(o.ImageDuration * o.FrameRate));
        var parallelism = RecommendedParallelism(slides.Count, maximum: 2);
        var completed = 0;
        await Parallel.ForEachAsync(Enumerable.Range(0, slides.Count),
            new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = token }, async (i, itemToken) =>
        {
            var target = Path.Combine(output, $"MOTION_{i + 1:D6}.mp4");
            var filter = MotionExpressionBuilder.Build(motions[i], o.MotionIntensity, o.MotionEasing,
                o.ImageDuration, o.FrameRate, o.Width, o.Height);
            if (string.IsNullOrEmpty(filter))
                filter = $"fps={o.FrameRate},scale={o.Width}:{o.Height}";
            filter += ",setsar=1,format=yuv420p";
            var psi = NewProcess(o.FfmpegPath);
            var encoderThreads = Math.Max(1, Environment.ProcessorCount / parallelism);
            Add(psi, "-hide_banner", "-threads", encoderThreads.ToString(CultureInfo.InvariantCulture), "-framerate",
                o.FrameRate.ToString(CultureInfo.InvariantCulture), "-loop", "1", "-i", slides[i],
                "-vf", filter, "-frames:v", frames.ToString(CultureInfo.InvariantCulture),
                "-an", "-c:v", "libx264", "-preset", "veryfast", "-crf", "12",
                "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-y", target);
            var run = await RunToolAsync(psi, itemToken);
            if (run.ExitCode != 0 || !File.Exists(target))
                throw new InvalidOperationException($"Could not render motion {i + 1}/{slides.Count}: {LastUsefulError(run.Error)}");
            result[i] = target;
            var completedCount = Interlocked.Increment(ref completed);
            progress?.Report(new SlideshowProgress(25 + completedCount / (double)slides.Count * 45,
                $"Rendered motion {completedCount}/{slides.Count}"));
        });
        return result;
    }

    private static ProcessStartInfo BuildTransitionStartInfo(SlideshowOptions o, IReadOnlyList<string> slides,
        IReadOnlyList<TransitionDefinition> transitions, double totalDuration)
    {
        var psi = NewProcess(o.FfmpegPath);
        Add(psi, "-hide_banner");
        foreach (var slide in slides)
            Add(psi, "-framerate", o.FrameRate.ToString(CultureInfo.InvariantCulture), "-loop", "1", "-t",
                o.ImageDuration.ToString("0.###", CultureInfo.InvariantCulture), "-i", slide);
        if (!string.IsNullOrWhiteSpace(o.AudioFile)) Add(psi, "-stream_loop", "-1", "-i", o.AudioFile!);
        Add(psi, "-filter_complex", TransitionGraphBuilder.Build(slides.Count, o.ImageDuration,
            o.TransitionDuration, o.FrameRate, transitions), "-map", "[video]");
        if (!string.IsNullOrWhiteSpace(o.AudioFile))
            Add(psi, "-map", $"{slides.Count}:a:0", "-af", $"volume={o.Volume.ToString("0.###", CultureInfo.InvariantCulture)}",
                "-c:a", "aac", "-b:a", "192k");
        Add(psi, "-t", totalDuration.ToString("0.###", CultureInfo.InvariantCulture), "-c:v", "libx264", "-crf",
            o.Quality.ToString(CultureInfo.InvariantCulture), "-preset", o.EncoderPreset, "-pix_fmt", "yuv420p",
            "-movflags", "+faststart", "-progress", "pipe:1", "-nostats", "-y", o.OutputFile);
        return psi;
    }

    private static string BuildFilter(SlideshowOptions o, bool backgroundInput, bool alreadyComposited)
    {
        var w = o.Width; var h = o.Height;
        if (o.Type == SlideshowType.Basic || alreadyComposited)
            return $"[0:v]fps={o.FrameRate},scale={w}:{h}:force_original_aspect_ratio=decrease," +
                   $"pad={w}:{h}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1[video]";

        var sw = Math.Max(2, (int)(w * o.ImageScaling) / 2 * 2);
        var sh = Math.Max(2, (int)(h * o.ImageScaling) / 2 * 2);
        // Keep the foreground at its actual aspect-ratio-preserving size. Padding it to the
        // scale box here would make that padding part of the overlay (and used to produce the
        // black bars visible even when a white/gradient/image background was selected).
        // The fps filter turns the concat demuxer's sparse still-image timestamps into a
        // continuous foreground stream, avoiding background-only frames at image boundaries.
        var foreground = $"[0:v]setpts=PTS-STARTPTS,fps={o.FrameRate},scale={sw}:{sh}:force_original_aspect_ratio=decrease,setsar=1[fg]";
        if (backgroundInput)
            return $"{foreground};[1:v]scale={w}:{h}:force_original_aspect_ratio=increase,crop={w}:{h},setsar=1[bg];[bg][fg]overlay=(W-w)/2:(H-h)/2:shortest=1[video]";
        if (o.BackgroundType == SlideshowBackgroundType.SolidColor)
            return $"{foreground};color=c={CleanColor(o.BackgroundColor)}:s={w}x{h}:r={o.FrameRate}[bg];[bg][fg]overlay=(W-w)/2:(H-h)/2:shortest=1[video]";

        var end = ParseColor(o.GradientEndColor);
        var factor = o.GradientDirection switch
        {
            SlideshowGradientDirection.LeftToRight => "X/W",
            SlideshowGradientDirection.Diagonal => "(X+Y)/(W+H)",
            SlideshowGradientDirection.Radial => "min(1,sqrt(pow((X-W/2)/(W/2),2)+pow((Y-H/2)/(H/2),2)))",
            _ => "Y/H"
        };
        var escaped = factor.Replace(",", "\\,");
        return $"{foreground};color=c={CleanColor(o.BackgroundColor)}:s={w}x{h}:r={o.FrameRate}," +
               $"geq=r='lerp(r(X,Y),{end.R},{escaped})':g='lerp(g(X,Y),{end.G},{escaped})':b='lerp(b(X,Y),{end.B},{escaped})'[bg];" +
               "[bg][fg]overlay=(W-w)/2:(H-h)/2:shortest=1[video]";
    }

    private static string CleanColor(string color) => "0x" + color.Trim().TrimStart('#');
    private static bool IsValidColor(string value)
    {
        var hex = value.Trim().TrimStart('#');
        return hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
    }
    private static (int R, int G, int B) ParseColor(string value)
    {
        var hex = value.Trim().TrimStart('#');
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            throw new ArgumentException($"Invalid color: {value}");
        return ((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
    }

    private static async Task<IReadOnlyList<string>> CompositeWithImageMagickAsync(
        SlideshowOptions o, string tempRoot, IProgress<SlideshowProgress>? progress, CancellationToken token)
    {
        var output = Path.Combine(tempRoot, "composited");
        Directory.CreateDirectory(output);
        var result = new string[o.Images.Count];
        var sw = Math.Max(2, (int)(o.Width * o.ImageScaling));
        var sh = Math.Max(2, (int)(o.Height * o.ImageScaling));
        var completed = 0;
        await Parallel.ForEachAsync(Enumerable.Range(0, o.Images.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = RecommendedParallelism(o.Images.Count),
                CancellationToken = token
            }, async (i, itemToken) =>
        {
            var target = Path.Combine(output, $"IMG_{i + 1:D6}.jpg");
            var psi = NewProcess(o.ImageMagickPath);
            Add(psi, o.BackgroundImage!, "-resize", $"{o.Width}x{o.Height}^", "-gravity", "center", "-extent", $"{o.Width}x{o.Height}",
                "(", o.Images[i], "-resize", $"{sw}x{sh}>", ")", "-gravity", "center", "-composite", target);
            var run = await RunToolAsync(psi, itemToken);
            if (run.ExitCode != 0 || !File.Exists(target)) throw new InvalidOperationException($"ImageMagick failed: {run.Error}");
            result[i] = target;
            var completedCount = Interlocked.Increment(ref completed);
            progress?.Report(new SlideshowProgress(completedCount / (double)o.Images.Count * 25,
                $"Composited {completedCount}/{o.Images.Count} images"));
        });
        return result;
    }

    private static async Task<IReadOnlyList<string>> NormalizeImagesAsync(
        SlideshowOptions o, string tempRoot, IProgress<SlideshowProgress>? progress, CancellationToken token)
    {
        // The concat demuxer expects every file to describe the same kind of video stream.
        // Feeding mixed JPEG/PNG/WebP/TIFF files directly can make FFmpeg decode only some
        // entries, leaving a background-only (white, for example) slide. Convert each source
        // to an identically sized RGBA PNG first; transparent padding lets the selected
        // slideshow background show through around portrait or landscape photos.
        var output = Path.Combine(tempRoot, "normalized");
        Directory.CreateDirectory(output);
        var isBackground = o.Type == SlideshowType.Background;
        var width = isBackground
            ? Math.Max(2, (int)(o.Width * o.ImageScaling) / 2 * 2)
            : o.Width;
        var height = isBackground
            ? Math.Max(2, (int)(o.Height * o.ImageScaling) / 2 * 2)
            : o.Height;
        var paddingColor = isBackground ? "black@0" : "black";
        var result = new string[o.Images.Count];
        var completed = 0;
        await Parallel.ForEachAsync(Enumerable.Range(0, o.Images.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = RecommendedParallelism(o.Images.Count),
                CancellationToken = token
            }, async (i, itemToken) =>
        {
            var target = Path.Combine(output, $"IMG_{i + 1:D6}.png");
            var psi = NewProcess(o.FfmpegPath);
            Add(psi, "-hide_banner", "-i", o.Images[i], "-filter_complex",
                BuildImageEffectFilter(o, width, height, paddingColor),
                "-map", "[prepared]", "-frames:v", "1", "-compression_level", "1", "-y", target);
            var run = await RunToolAsync(psi, itemToken);
            if (run.ExitCode != 0 || !File.Exists(target))
                throw new InvalidOperationException($"Could not prepare {Path.GetFileName(o.Images[i])}: {LastUsefulError(run.Error)}");
            result[i] = target;
            var completedCount = Interlocked.Increment(ref completed);
            progress?.Report(new SlideshowProgress(completedCount / (double)o.Images.Count * 20,
                $"Prepared {completedCount}/{o.Images.Count} images"));
        });

        return result;
    }

    private static async Task<IReadOnlyList<string>> RenderFinalSlidesAsync(SlideshowOptions o,
        IReadOnlyList<string> preparedImages, string tempRoot, bool alreadyComposited,
        IProgress<SlideshowProgress>? progress, CancellationToken token)
    {
        if (o.Type == SlideshowType.Basic || alreadyComposited) return preparedImages;
        var output = Path.Combine(tempRoot, "slides");
        Directory.CreateDirectory(output);
        var result = new string[preparedImages.Count];
        var completed = 0;
        await Parallel.ForEachAsync(Enumerable.Range(0, preparedImages.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = RecommendedParallelism(preparedImages.Count),
                CancellationToken = token
            }, async (i, itemToken) =>
        {
            var target = Path.Combine(output, $"SLIDE_{i + 1:D6}.png");
            var psi = NewProcess(o.FfmpegPath);
            Add(psi, "-hide_banner", "-i", preparedImages[i]);
            var backgroundInput = o.BackgroundType == SlideshowBackgroundType.Image;
            if (backgroundInput) Add(psi, "-i", o.BackgroundImage!);
            Add(psi, "-filter_complex", BuildFilter(o, backgroundInput, false), "-map", "[video]",
                "-frames:v", "1", "-compression_level", "1", "-y", target);
            var run = await RunToolAsync(psi, itemToken);
            if (run.ExitCode != 0 || !File.Exists(target))
                throw new InvalidOperationException($"Could not render slide {i + 1}: {LastUsefulError(run.Error)}");
            result[i] = target;
            var completedCount = Interlocked.Increment(ref completed);
            progress?.Report(new SlideshowProgress(20 + completedCount / (double)preparedImages.Count * 15,
                $"Rendered {completedCount}/{preparedImages.Count} slides"));
        });
        return result;
    }

    public static int RecommendedParallelism(int itemCount, int maximum = 4) =>
        Math.Max(1, Math.Min(itemCount, Math.Min(maximum, Math.Max(1, Environment.ProcessorCount / 2))));

    private static string BuildImageEffectFilter(SlideshowOptions o, int width, int height, string paddingColor)
    {
        var border = o.EnableBorder ? o.BorderWidth : 0;
        var shadowMargin = o.EnableShadow ? o.ShadowBlur * 3 : 0;
        var offsetX = o.EnableShadow ? o.ShadowOffsetX : 0;
        var offsetY = o.EnableShadow ? o.ShadowOffsetY : 0;
        var left = shadowMargin + Math.Max(0, -offsetX);
        var right = shadowMargin + Math.Max(0, offsetX);
        var top = shadowMargin + Math.Max(0, -offsetY);
        var bottom = shadowMargin + Math.Max(0, offsetY);
        var availableWidth = Math.Max(2, width - left - right);
        var availableHeight = Math.Max(2, height - top - bottom);
        var filters = new StringBuilder($"[0:v]scale={availableWidth}:{availableHeight}:force_original_aspect_ratio=decrease,format=rgba");

        if (border > 0)
            filters.Append($",drawbox=x=0:y=0:w=iw:h=ih:color={CleanColor(o.BorderColor)}:t={border}");

        if (!o.EnableShadow)
        {
            filters.Append($",pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:color={paddingColor}[prepared]");
            return filters.ToString();
        }

        var opacity = o.ShadowOpacity.ToString("0.###", CultureInfo.InvariantCulture);
        var blur = Math.Max(0.01, o.ShadowBlur).ToString("0.###", CultureInfo.InvariantCulture);
        var canvasWidth = $"iw+{left + right}";
        var canvasHeight = $"ih+{top + bottom}";
        filters.Append("[photo];[photo]split[foreground][shadow]");
        filters.Append($";[shadow]colorchannelmixer=rr=0:gg=0:bb=0:aa={opacity}," +
                       $"pad={canvasWidth}:{canvasHeight}:{left + offsetX}:{top + offsetY}:color=black@0," +
                       $"gblur=sigma={blur}:steps=2[softshadow]");
        filters.Append($";[foreground]pad={canvasWidth}:{canvasHeight}:{left}:{top}:color=black@0[card]");
        filters.Append($";[softshadow][card]overlay=0:0:format=auto," +
                       $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:color={paddingColor}[prepared]");
        return filters.ToString();
    }

    private static async Task<SlideshowResult> RunFfmpegAsync(ProcessStartInfo psi, string output, double totalSeconds,
        IProgress<SlideshowProgress>? progress, CancellationToken token, double progressStart = 0)
    {
        using var process = new Process { StartInfo = psi };
        var errorLines = new Queue<string>();
        process.Start();
        using var registration = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
        var stderrTask = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync(token) is { } line)
            {
                errorLines.Enqueue(line);
                while (errorLines.Count > 200) errorLines.Dequeue();
            }
        }, token);
        while (await process.StandardOutput.ReadLineAsync(token) is { } line)
        {
            if (!line.StartsWith("out_time_ms=", StringComparison.Ordinal) ||
                !long.TryParse(line[12..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var micros)) continue;
            var encodingProgress = totalSeconds <= 0 ? 0 : Math.Clamp(micros / 1_000_000d / totalSeconds, 0, .99);
            var percent = progressStart + encodingProgress * (100 - progressStart);
            progress?.Report(new SlideshowProgress(percent, $"Assembling final video... {percent:0}%"));
        }
        await process.WaitForExitAsync(token);
        await stderrTask;
        if (process.ExitCode != 0)
        {
            TryDeleteIncompleteOutput(output);
            return SlideshowResult.Failed(ExplainFfmpegError(string.Join(Environment.NewLine, errorLines), process.ExitCode));
        }
        progress?.Report(new SlideshowProgress(100, "Slideshow created successfully."));
        return new SlideshowResult(true, null, output);
    }

    private static async Task<(int ExitCode, string Error)> RunToolAsync(ProcessStartInfo psi, CancellationToken token)
    {
        using var process = new Process { StartInfo = psi };
        process.Start();
        using var registration = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
        var error = await process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        return (process.ExitCode, error);
    }

    private static ProcessStartInfo NewProcess(string executable) => new()
    {
        FileName = executable, UseShellExecute = false, RedirectStandardOutput = true,
        RedirectStandardError = true, CreateNoWindow = true
    };
    private static void Add(ProcessStartInfo psi, params string[] args) { foreach (var arg in args) psi.ArgumentList.Add(arg); }
    private static string LastUsefulError(string error) => string.Join(Environment.NewLine,
        error.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).TakeLast(8));
    private static string ExplainFfmpegError(string error, int exitCode)
    {
        var detail = LastUsefulError(error);
        if (exitCode is -1073741819 or -1073740791)
            return $"FFmpeg crashed unexpectedly (exit code 0x{unchecked((uint)exitCode):X8}). " +
                   "The incomplete output file was removed. Try a newer FFmpeg build or fewer simultaneous effects.\n" + detail;
        if (error.Contains("No such filter", StringComparison.OrdinalIgnoreCase))
            return "FFmpeg reported an unavailable filter. Verify that this build includes xfade.\n" + detail;
        if (error.Contains("timebase", StringComparison.OrdinalIgnoreCase) || error.Contains("time base", StringComparison.OrdinalIgnoreCase))
            return "FFmpeg rejected incompatible video timebases.\n" + detail;
        if (error.Contains("size", StringComparison.OrdinalIgnoreCase) && error.Contains("match", StringComparison.OrdinalIgnoreCase))
            return "FFmpeg rejected incompatible slide dimensions.\n" + detail;
        if (error.Contains("xfade", StringComparison.OrdinalIgnoreCase))
            return "FFmpeg could not apply the selected transition. Check its duration and the slide timeline.\n" + detail;
        return detail;
    }
    private static void TryDeleteIncompleteOutput(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
    private static void TryDeleteTemporaryFolder(string path)
    {
        try
        {
            var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "TidyMemo")) + Path.DirectorySeparatorChar;
            var target = Path.GetFullPath(path);
            if (target.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(target)) Directory.Delete(target, true);
        }
        catch { }
    }
}
