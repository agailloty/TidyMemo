using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace TidyMemo.Tests;

public sealed class FfmpegMotionIntegrationTests
{
    [Fact]
    public async Task ZoomPanSegmentsCanBeCombinedWithXfadeWhenFfmpegIsAvailable()
    {
        var ffmpeg = FindFfmpeg();
        if (ffmpeg is null) return; // Portable CI: unit graph tests remain authoritative without FFmpeg.
        var output = Path.Combine(Path.GetTempPath(), $"tidymemo-motion-{Guid.NewGuid():N}.mp4");
        try
        {
            var graph =
                "[0:v]zoompan=z='1+(0.08)*(min(1\\,max(0\\,on/59.0)))':x='(iw-iw/zoom)*(0.5)':y='(ih-ih/zoom)*(0.5)':d=60:s=320x180:fps=30,fps=30,scale=320:180,setsar=1,format=yuv420p[m0];" +
                "[1:v]zoompan=z='1.12':x='(iw-iw/zoom)*(1+(-1)*(min(1\\,max(0\\,on/59.0))))':y='(ih-ih/zoom)*(0.5)':d=60:s=320x180:fps=30,fps=30,scale=320:180,setsar=1,format=yuv420p[m1];" +
                "[m0][m1]xfade=transition=dissolve:duration=0.5:offset=1.5[video]";
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true
            };
            Add(psi, "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i",
                "color=c=red:s=320x180:r=30:d=0.034", "-f", "lavfi", "-i",
                "color=c=blue:s=320x180:r=30:d=0.034", "-filter_complex", graph,
                "-map", "[video]", "-t", "3.5", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-y", output);
            using var process = Process.Start(psi)!;
            var error = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.True(process.ExitCode == 0, error);
            Assert.True(new FileInfo(output).Length > 0);
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    private static string? FindFfmpeg()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim(), OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
            if (File.Exists(candidate)) return candidate;
        }
        var commonWindowsPath = @"C:\ffmpeg\bin\ffmpeg.exe";
        return File.Exists(commonWindowsPath) ? commonWindowsPath : null;
    }

    private static void Add(ProcessStartInfo psi, params string[] arguments)
    {
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);
    }
}
