using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TidyMemo.Services;

public sealed record FfmpegCapabilities(bool HasXfade, bool HasZoompan,
    IReadOnlySet<string> XfadeTransitions, string Diagnostic);

public sealed partial class FfmpegCapabilitiesService
{
    private static readonly ConcurrentDictionary<string, FfmpegCapabilities> Cache = new(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"^\s+[a-z][a-z0-9]*\s+-?\d+\s+\.\.", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex TransitionLineRegex();

    public async Task<FfmpegCapabilities> DetectAsync(string ffmpegPath, CancellationToken token = default)
    {
        var cacheKey = GetCacheKey(ffmpegPath);
        if (Cache.TryGetValue(cacheKey, out var cached)) return cached;
        var detected = await DetectUncachedAsync(ffmpegPath, token);
        Cache[cacheKey] = detected;
        return detected;
    }

    private static async Task<FfmpegCapabilities> DetectUncachedAsync(string ffmpegPath, CancellationToken token)
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
            if (process is null) return new(false, false, new HashSet<string>(), "FFmpeg could not be started.");
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
            var hasXfade = process.ExitCode == 0 && text.Contains("Filter xfade", StringComparison.OrdinalIgnoreCase);
            var zoompan = await HasFilterAsync(ffmpegPath, "zoompan", token);
            return new(hasXfade, zoompan, names,
                process.ExitCode == 0 ? text : $"FFmpeg exited with code {process.ExitCode}: {text}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new(false, false, new HashSet<string>(), exception.Message);
        }
    }

    private static string GetCacheKey(string ffmpegPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(ffmpegPath);
            return $"{fullPath}|{File.GetLastWriteTimeUtc(fullPath).Ticks}|{new FileInfo(fullPath).Length}";
        }
        catch
        {
            return ffmpegPath;
        }
    }

    private static async Task<bool> HasFilterAsync(string ffmpegPath, string filter, CancellationToken token)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        psi.ArgumentList.Add("-hide_banner"); psi.ArgumentList.Add("-h"); psi.ArgumentList.Add($"filter={filter}");
        using var process = Process.Start(psi);
        if (process is null) return false;
        var stdout = await process.StandardOutput.ReadToEndAsync(token);
        var stderr = await process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        return process.ExitCode == 0 && (stdout + stderr).Contains($"Filter {filter}", StringComparison.OrdinalIgnoreCase);
    }
}
