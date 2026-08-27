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
                                 options.UseEnhancedBackgroundProcessing && options.PreferImageMagick;
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

            var manifest = Path.Combine(tempRoot, "images.ffconcat");
            await WriteManifestAsync(manifest, images, options.ImageDuration, cancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(options.OutputFile)!);

            var startInfo = BuildFfmpegStartInfo(options, manifest, useImageMagick);
            var totalDuration = images.Count * options.ImageDuration;
            return await RunFfmpegAsync(startInfo, options.OutputFile, totalDuration, progress, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return SlideshowResult.Failed("Slideshow creation cancelled.");
        }
        catch (Exception ex)
        {
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
        var result = new List<string>(o.Images.Count);
        var sw = Math.Max(2, (int)(o.Width * o.ImageScaling));
        var sh = Math.Max(2, (int)(o.Height * o.ImageScaling));
        for (var i = 0; i < o.Images.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var target = Path.Combine(output, $"IMG_{i + 1:D6}.jpg");
            var psi = NewProcess(o.ImageMagickPath);
            Add(psi, o.BackgroundImage!, "-resize", $"{o.Width}x{o.Height}^", "-gravity", "center", "-extent", $"{o.Width}x{o.Height}",
                "(", o.Images[i], "-resize", $"{sw}x{sh}>", ")", "-gravity", "center", "-composite", target);
            var run = await RunToolAsync(psi, token);
            if (run.ExitCode != 0 || !File.Exists(target)) throw new InvalidOperationException($"ImageMagick failed: {run.Error}");
            result.Add(target);
            progress?.Report(new SlideshowProgress((i + 1d) / o.Images.Count * 25, $"Composited {i + 1}/{o.Images.Count} images"));
        }
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
        var result = new List<string>(o.Images.Count);

        for (var i = 0; i < o.Images.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var target = Path.Combine(output, $"IMG_{i + 1:D6}.png");
            var psi = NewProcess(o.FfmpegPath);
            Add(psi, "-hide_banner", "-i", o.Images[i], "-vf",
                $"scale={width}:{height}:force_original_aspect_ratio=decrease,format=rgba," +
                $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:color={paddingColor}",
                "-frames:v", "1", "-y", target);
            var run = await RunToolAsync(psi, token);
            if (run.ExitCode != 0 || !File.Exists(target))
                throw new InvalidOperationException($"Could not prepare {Path.GetFileName(o.Images[i])}: {LastUsefulError(run.Error)}");
            result.Add(target);
            progress?.Report(new SlideshowProgress((i + 1d) / o.Images.Count * 20,
                $"Prepared {i + 1}/{o.Images.Count} images"));
        }

        return result;
    }

    private static async Task<SlideshowResult> RunFfmpegAsync(ProcessStartInfo psi, string output, double totalSeconds,
        IProgress<SlideshowProgress>? progress, CancellationToken token)
    {
        using var process = new Process { StartInfo = psi };
        var errors = new StringBuilder();
        process.Start();
        using var registration = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
        var stderrTask = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync(token) is { } line)
            {
                if (errors.Length < 16000) errors.AppendLine(line);
            }
        }, token);
        while (await process.StandardOutput.ReadLineAsync(token) is { } line)
        {
            if (!line.StartsWith("out_time_ms=", StringComparison.Ordinal) ||
                !long.TryParse(line[12..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var micros)) continue;
            var percent = totalSeconds <= 0 ? 0 : Math.Clamp(micros / 1_000_000d / totalSeconds * 100, 0, 99);
            progress?.Report(new SlideshowProgress(percent, $"Creating slideshow... {percent:0}%"));
        }
        await process.WaitForExitAsync(token);
        await stderrTask;
        if (process.ExitCode != 0) return SlideshowResult.Failed(LastUsefulError(errors.ToString()));
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
