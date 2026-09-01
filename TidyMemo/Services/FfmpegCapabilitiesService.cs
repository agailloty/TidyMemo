using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TidyMemo.Services;

public sealed record FfmpegCapabilities(bool HasXfade, IReadOnlySet<string> XfadeTransitions, string Diagnostic);

public sealed partial class FfmpegCapabilitiesService
{
    [GeneratedRegex(@"^\s+[a-z][a-z0-9]*\s+-?\d+\s+\.\.", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex TransitionLineRegex();

    public async Task<FfmpegCapabilities> DetectAsync(string ffmpegPath, CancellationToken token = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-h");
        psi.ArgumentList.Add("filter=xfade");
        try
        {
            using var process = Process.Start(psi);
            if (process is null) return new(false, new HashSet<string>(), "FFmpeg could not be started.");
            var stdout = await process.StandardOutput.ReadToEndAsync(token);
            var stderr = await process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);
            var text = stdout + Environment.NewLine + stderr;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in TransitionLineRegex().Matches(text))
            {
                var parts = match.Value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && parts[0] != "custom") names.Add(parts[0]);
            }
            return new(process.ExitCode == 0 && text.Contains("Filter xfade", StringComparison.OrdinalIgnoreCase), names,
                process.ExitCode == 0 ? text : $"FFmpeg exited with code {process.ExitCode}: {text}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new(false, new HashSet<string>(), exception.Message);
        }
    }
}
