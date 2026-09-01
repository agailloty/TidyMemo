using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using TidyMemo.Services;

namespace TidyMemo;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        if (TryParseCommandLine(args, out var command, out var parseError))
            return RunProjectAsync(command!).GetAwaiter().GetResult();
        if (parseError is not null)
        {
            Console.Error.WriteLine(parseError);
            PrintUsage();
            return 2;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
        return 0;
    }

    private static async Task<int> RunProjectAsync(RenderCommand command)
    {
        if (command.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        var settings = new SettingsService().Load();
        var ffmpegPath = command.FfmpegPath ?? settings.FfmpegPath;
        if (string.IsNullOrWhiteSpace(ffmpegPath))
        {
            Console.Error.WriteLine("FFmpeg is not configured. Configure it in TidyMemo or use --ffmpeg <path>.");
            return 2;
        }

        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;
        try
        {
            var started = DateTimeOffset.UtcNow;
            var progress = new Progress<Models.SlideshowProgress>(value =>
                Console.WriteLine($"[{value.Percentage,6:0.0}%] {value.Message}"));
            var result = await new SlideshowProjectRunner().RunAsync(command.ProjectPath!, ffmpegPath,
                command.OutputPath, progress, cancellation.Token);
            var elapsed = DateTimeOffset.UtcNow - started;
            if (!result.Success)
            {
                Console.Error.WriteLine($"Export failed after {elapsed}: {result.ErrorMessage}");
                return cancellation.IsCancellationRequested ? 130 : 1;
            }

            Console.WriteLine($"Created: {result.OutputFile}");
            Console.WriteLine($"Elapsed: {elapsed}");
            return 0;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static bool TryParseCommandLine(string[] args, out RenderCommand? command, out string? error)
    {
        command = null;
        error = null;
        if (args.Length == 0) return false;
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            command = new RenderCommand(null, null, null, true);
            return true;
        }

        var values = new List<string>(args);
        if (values.Count > 0 && values[0] == "--render") values.RemoveAt(0);
        string? project = null;
        string? output = null;
        string? ffmpeg = null;
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (value is "--output" or "-o" or "--ffmpeg")
            {
                if (++i >= values.Count)
                {
                    error = $"Missing value after {value}.";
                    return false;
                }
                if (value == "--ffmpeg") ffmpeg = values[i]; else output = values[i];
            }
            else if (value.StartsWith('-'))
            {
                error = $"Unknown option: {value}";
                return false;
            }
            else if (project is null) project = value;
            else
            {
                error = $"Unexpected argument: {value}";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(project))
        {
            error = "A .slidetune project path is required.";
            return false;
        }
        command = new RenderCommand(project, output, ffmpeg, false);
        return true;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("TidyMemo headless slideshow renderer");
        Console.WriteLine("Usage: TidyMemo.exe <project.slidetune> [--output <video.mp4>] [--ffmpeg <path>]");
        Console.WriteLine("       TidyMemo.exe --render <project.slidetune> [options]");
        Console.WriteLine("With no arguments, TidyMemo starts the graphical interface.");
    }

    private sealed record RenderCommand(string? ProjectPath, string? OutputPath, string? FfmpegPath, bool ShowHelp);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
