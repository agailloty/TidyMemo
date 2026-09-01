using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TidyMemo.Models;

namespace TidyMemo.Services;

public sealed class SlideshowProjectRunner(
    ISlideshowProjectStore? projectStore = null,
    SlideshowService? slideshowService = null)
{
    private readonly ISlideshowProjectStore _projectStore = projectStore ?? new JsonSlideshowProjectStore();
    private readonly SlideshowService _slideshowService = slideshowService ?? new SlideshowService();

    public async Task<SlideshowResult> RunAsync(string projectPath, string ffmpegPath,
        string? outputOverride = null, IProgress<SlideshowProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var fullProjectPath = Path.GetFullPath(projectPath);
        if (!File.Exists(fullProjectPath))
            return SlideshowResult.Failed($"Project file not found: {fullProjectPath}");

        try
        {
            var project = await _projectStore.LoadAsync(fullProjectPath, cancellationToken);
            var options = BuildOptions(project, fullProjectPath, ffmpegPath, outputOverride);
            return await _slideshowService.CreateAsync(options, progress, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return SlideshowResult.Failed("Slideshow creation cancelled.");
        }
        catch (Exception exception)
        {
            return SlideshowResult.Failed(exception.Message);
        }
    }

    public static SlideshowOptions BuildOptions(SlideshowProject project, string projectPath,
        string ffmpegPath, string? outputOverride = null)
    {
        var presentation = project.Presentation;
        var export = project.Export;
        var output = string.IsNullOrWhiteSpace(outputOverride)
            ? SlideshowProjectPaths.ToAbsolutePath(export.OutputFile, projectPath)
            : Path.GetFullPath(outputOverride);
        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidDataException("The project does not define an output file. Use --output to specify one.");

        return new SlideshowOptions
        {
            Images = project.Slides.Where(slide => slide.Enabled)
                .Select(slide => SlideshowProjectPaths.ToAbsolutePath(slide.Path, projectPath) ?? string.Empty)
                .Where(path => !string.IsNullOrWhiteSpace(path)).ToArray(),
            OutputFile = output,
            FfmpegPath = ffmpegPath,
            AudioFile = SlideshowProjectPaths.ToAbsolutePath(project.Audio.Path, projectPath),
            ImageDuration = presentation.ImageDuration,
            FrameRate = export.FrameRate,
            Width = presentation.Width,
            Height = presentation.Height,
            Quality = export.Quality,
            EncoderPreset = export.EncoderPreset,
            Volume = project.Audio.Volume,
            Type = presentation.Type,
            BackgroundType = presentation.BackgroundType,
            BackgroundColor = presentation.BackgroundColor,
            GradientEndColor = presentation.GradientEndColor,
            GradientDirection = presentation.GradientDirection,
            BackgroundImage = SlideshowProjectPaths.ToAbsolutePath(presentation.BackgroundImage, projectPath),
            ImageScaling = presentation.ImageScaling,
            EnableBorder = presentation.EnableBorder,
            BorderWidth = presentation.BorderWidth,
            BorderColor = presentation.BorderColor,
            EnableShadow = presentation.EnableShadow,
            ShadowOffsetX = presentation.ShadowOffsetX,
            ShadowOffsetY = presentation.ShadowOffsetY,
            ShadowBlur = presentation.ShadowBlur,
            ShadowOpacity = presentation.ShadowOpacity,
            UseEnhancedBackgroundProcessing = presentation.UseEnhancedBackgroundProcessing,
            PreferImageMagick = presentation.PreferImageMagick,
            ImageMagickPath = presentation.ImageMagickPath,
            TransitionMode = presentation.TransitionMode,
            TransitionId = presentation.TransitionId,
            TransitionDuration = presentation.TransitionDuration,
            MotionMode = presentation.MotionMode,
            MotionId = presentation.MotionId,
            MotionIntensity = presentation.MotionIntensity,
            MotionEasing = presentation.MotionEasing,
            RandomSeed = presentation.RandomSeed
        };
    }
}
