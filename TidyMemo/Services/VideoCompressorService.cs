using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TidyMemo.Models;

namespace TidyMemo.Services;

public class VideoCompressionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long InputSize { get; set; }
    public long OutputSize { get; set; }
}

public class VideoCompressorService
{
    private static readonly string[] VideoExtensions =
        { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".m4v", ".webm" };

    public bool IsSupportedVideo(string path) =>
        File.Exists(path) && VideoExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public string[] GetVideoFiles(IEnumerable<string> folderPaths, bool includeSubfolders = false)
    {
        var option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = new List<string>();
        foreach (var folder in folderPaths)
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var file in Directory.GetFiles(folder, "*", option))
            {
                if (VideoExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    files.Add(file);
            }
        }
        return files.ToArray();
    }

    public async Task<VideoCompressionResult> CompressAsync(
        string inputPath,
        string outputPath,
        VideoCompressionPreset preset,
        string ffmpegPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var inputFile = new FileInfo(inputPath);
        if (!inputFile.Exists)
            return new VideoCompressionResult { Success = false, ErrorMessage = "Source file not found." };

        var inputSize = inputFile.Length;
        var args = BuildArguments(inputPath, outputPath, preset);

        var processInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(processInfo)
                ?? throw new InvalidOperationException("Failed to start ffmpeg.");

            // Read stderr for progress lines without blocking
            _ = Task.Run(async () =>
            {
                string? line;
                while ((line = await process.StandardError.ReadLineAsync(cancellationToken)) != null)
                    progress?.Report(line);
            }, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            if (File.Exists(outputPath))
            {
                var outputFile = new FileInfo(outputPath);
                // Preserve original file system dates
                outputFile.CreationTime = inputFile.CreationTime;
                outputFile.LastWriteTime = inputFile.LastWriteTime;

                return new VideoCompressionResult
                {
                    Success = true,
                    InputSize = inputSize,
                    OutputSize = outputFile.Length
                };
            }

            return new VideoCompressionResult
            {
                Success = false,
                ErrorMessage = "Output file was not created."
            };
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            throw;
        }
        catch (Exception ex)
        {
            return new VideoCompressionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<VideoCompressionResult> ProcessAsync(
        string inputPath,
        string outputPath,
        VideoProcessingOptions options,
        string ffmpegPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (options.Operation == VideoProcessingOperation.Compress)
        {
            if (options.CompressionPreset is null)
                return new VideoCompressionResult { Success = false, ErrorMessage = "A compression preset is required." };
            return await CompressAsync(inputPath, outputPath, options.CompressionPreset,
                ffmpegPath, progress, cancellationToken);
        }

        var inputFile = new FileInfo(inputPath);
        if (!inputFile.Exists)
            return new VideoCompressionResult { Success = false, ErrorMessage = "Source file not found." };

        var processInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = BuildProcessingArguments(inputPath, outputPath, options),
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(processInfo)
                ?? throw new InvalidOperationException("Failed to start ffmpeg.");
            _ = Task.Run(async () =>
            {
                string? line;
                while ((line = await process.StandardError.ReadLineAsync(cancellationToken)) != null)
                    progress?.Report(line);
            }, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0 || !File.Exists(outputPath))
                return new VideoCompressionResult { Success = false, ErrorMessage = "FFmpeg could not create the output file." };

            var outputFile = new FileInfo(outputPath);
            outputFile.CreationTime = inputFile.CreationTime;
            outputFile.LastWriteTime = inputFile.LastWriteTime;
            return new VideoCompressionResult
            {
                Success = true,
                InputSize = inputFile.Length,
                OutputSize = outputFile.Length
            };
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
            throw;
        }
        catch (Exception exception)
        {
            return new VideoCompressionResult { Success = false, ErrorMessage = exception.Message };
        }
    }

    private static string BuildArguments(string inputPath, string outputPath, VideoCompressionPreset preset)
    {
        var parts = new List<string>
        {
            "-y",
            "-i", $"\"{inputPath}\"",
            "-acodec", "copy",
            "-threads", "4",
            "-loglevel", "error",
            // Preserve all metadata: global tags, chapter markers, and per-stream tags
            "-map_metadata", "0",
            "-map_chapters", "0",
            "-map_metadata:s:v", "0:s:v",
            "-map_metadata:s:a", "0:s:a"
        };

        var videoEncoder = preset.VideoEncoder
            ?? (preset.UseEncoderDefaults ? null : "libx264");

        if (videoEncoder is not null)
        {
            var encoderArguments = new List<string>
            {
                "-vcodec", videoEncoder
            };

            if (!preset.UseEncoderDefaults)
            {
                encoderArguments.AddRange(new[]
                {
                    "-crf", preset.Crf.ToString(),
                    "-preset", preset.FfmpegPreset
                });
            }

            // hvc1 improves H.265 playback compatibility in Apple software and devices.
            if (videoEncoder == "libx265" &&
                Path.GetExtension(outputPath).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                encoderArguments.AddRange(new[] { "-tag:v", "hvc1" });
            }

            parts.InsertRange(3, encoderArguments);
        }

        if (!string.IsNullOrEmpty(preset.ScaleFilter))
        {
            parts.Add("-vf");
            parts.Add($"scale={preset.ScaleFilter}");
        }

        parts.Add($"\"{outputPath}\"");
        return string.Join(" ", parts);
    }

    private static string BuildProcessingArguments(
        string inputPath, string outputPath, VideoProcessingOptions options)
    {
        var speed = options.SpeedMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var common = $"-y -i \"{inputPath}\" ";

        if (options.Operation is VideoProcessingOperation.ExportGif or VideoProcessingOperation.SpeedUpAndExportGif)
        {
            var speedFilter = options.Operation == VideoProcessingOperation.SpeedUpAndExportGif
                ? $"setpts=PTS/{speed},"
                : string.Empty;
            var filter = $"[0:v]{speedFilter}fps={options.GifFps}," +
                         $"scale='min({options.GifWidth},iw)':-2:flags=lanczos,split[s0][s1];" +
                         "[s0]palettegen=max_colors=256[p];[s1][p]paletteuse=dither=sierra2_4a";
            return common + $"-filter_complex \"{filter}\" -an -loop 0 \"{outputPath}\"";
        }

        if (options.Operation == VideoProcessingOperation.SpeedUp)
        {
            return common + $"-map 0:v:0 -map 0:a? -vf \"setpts=PTS/{speed}\" -af \"atempo={speed}\" " +
                   $"-c:v libx264 -preset veryfast -crf 23 -c:a aac -movflags +faststart \"{outputPath}\"";
        }

        return options.OutputFormat.ToLowerInvariant() switch
        {
            "webm" => common + $"-c:v libvpx-vp9 -crf 31 -b:v 0 -c:a libopus \"{outputPath}\"",
            "avi" => common + $"-c:v mpeg4 -q:v 4 -c:a libmp3lame \"{outputPath}\"",
            "mkv" => common + $"-c:v libx264 -crf 23 -preset veryfast -c:a aac \"{outputPath}\"",
            "mov" => common + $"-c:v libx264 -crf 23 -preset veryfast -c:a aac \"{outputPath}\"",
            _ => common + $"-c:v libx264 -crf 23 -preset veryfast -c:a aac -movflags +faststart \"{outputPath}\""
        };
    }
}
