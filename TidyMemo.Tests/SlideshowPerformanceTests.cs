using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TidyMemo.Models;
using TidyMemo.Services;
using Xunit;

namespace TidyMemo.Tests;

[CollectionDefinition("Slideshow performance", DisableParallelization = true)]
public sealed class SlideshowPerformanceCollection;

[Collection("Slideshow performance")]
public sealed class SlideshowPerformanceTests
{
    [Theory]
    [InlineData("basic")]
    [InlineData("effects-transitions")]
    [InlineData("motion-transitions")]
    [Trait("Category", "Performance")]
    public async Task GeneratedImagesRenderPerformanceScenario(string scenario)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("TIDYMEMO_RUN_PERF_TESTS"), "1",
                StringComparison.Ordinal))
            return;

        var ffmpeg = FindFfmpeg();
        Assert.True(ffmpeg is not null,
            "Set TIDYMEMO_FFMPEG or add FFmpeg to PATH before running performance tests.");
        var imageCount = ReadPositiveInteger("TIDYMEMO_PERF_IMAGE_COUNT", 12);
        var width = ReadPositiveInteger("TIDYMEMO_PERF_WIDTH", 1280);
        var height = ReadPositiveInteger("TIDYMEMO_PERF_HEIGHT", 720);
        var root = Directory.CreateTempSubdirectory($"tidymemo-perf-{scenario}-");
        try
        {
            var images = GenerateImages(root.FullName, imageCount, width, height);
            var output = Path.Combine(root.FullName, $"{scenario}.mp4");
            var withEffects = scenario != "basic";
            var withMotion = scenario == "motion-transitions";
            var options = new SlideshowOptions
            {
                Images = images, OutputFile = output, FfmpegPath = ffmpeg!,
                Width = width, Height = height, FrameRate = 24, ImageDuration = .6,
                Quality = 23, EncoderPreset = "veryfast",
                Type = withEffects ? SlideshowType.Background : SlideshowType.Basic,
                BackgroundType = SlideshowBackgroundType.Gradient,
                BackgroundColor = "#16213E", GradientEndColor = "#E94560", ImageScaling = .82,
                EnableBorder = withEffects, BorderWidth = 5,
                EnableShadow = withEffects, ShadowBlur = 12, ShadowOpacity = .4,
                TransitionMode = withEffects ? TransitionMode.Random : TransitionMode.None,
                TransitionDuration = .15,
                MotionMode = withMotion ? PhotoMotionMode.Random : PhotoMotionMode.None,
                MotionId = "slow-zoom-in", RandomSeed = 42
            };

            var stopwatch = Stopwatch.StartNew();
            var result = await new SlideshowService().CreateAsync(options, cancellationToken:
                TestContext.Current.CancellationToken);
            stopwatch.Stop();

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(output));
            Assert.True(new FileInfo(output).Length > 0);
            var videoDuration = imageCount * options.ImageDuration -
                                (withEffects ? (imageCount - 1) * options.TransitionDuration : 0);
            var renderedFrames = videoDuration * options.FrameRate;
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"{scenario}: {imageCount} generated {width}x{height} images, " +
                $"{stopwatch.Elapsed.TotalSeconds:0.000}s, " +
                $"{renderedFrames / stopwatch.Elapsed.TotalSeconds:0.0} output frames/s, " +
                $"{new FileInfo(output).Length / 1_048_576d:0.00} MiB");
        }
        finally
        {
            root.Delete(true);
        }
    }

    private static string[] GenerateImages(string directory, int count, int width, int height)
    {
        var paths = new string[count];
        var row = new byte[width * 3];
        for (var imageIndex = 0; imageIndex < count; imageIndex++)
        {
            var path = Path.Combine(directory, $"generated-{imageIndex + 1:D4}.ppm");
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                64 * 1024, FileOptions.SequentialScan);
            stream.Write(Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"));
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var offset = x * 3;
                    row[offset] = (byte)((x * 255 / Math.Max(1, width - 1) + imageIndex * 31) % 256);
                    row[offset + 1] = (byte)((y * 255 / Math.Max(1, height - 1) + imageIndex * 53) % 256);
                    row[offset + 2] = (byte)(((x + y) * 127 / Math.Max(1, width + height - 2) +
                                              imageIndex * 79) % 256);
                }
                stream.Write(row);
            }
            paths[imageIndex] = path;
        }
        return paths;
    }

    private static int ReadPositiveInteger(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.None,
            CultureInfo.InvariantCulture, out var value) && value > 0 ? value : fallback;

    private static string? FindFfmpeg()
    {
        var configured = Environment.GetEnvironmentVariable("TIDYMEMO_FFMPEG");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        var executable = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory.Trim(), executable))
            .FirstOrDefault(File.Exists);
    }
}
