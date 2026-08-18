using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace TidyMemo.Services;

public sealed class FfmpegDownloadService
{
    private const string ReleaseTag = "b6.1.1";
    private const string ReleaseBaseUrl =
        "https://github.com/eugeneware/ffmpeg-static/releases/download";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    public string PlatformDescription => GetPlatform().Description;

    public async Task<string> DownloadAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var platform = GetPlatform();
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
            throw new InvalidOperationException("The local application data folder is unavailable.");

        var installDirectory = Path.Combine(appData, "TidyMemo", "ffmpeg", ReleaseTag);
        var destinationPath = Path.Combine(installDirectory, platform.ExecutableName);
        Directory.CreateDirectory(installDirectory);

        if (await IsFfmpegAsync(destinationPath, cancellationToken))
        {
            progress?.Report(1);
            return destinationPath;
        }

        var temporaryPath = destinationPath + ".download" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty);
        try
        {
            using var response = await HttpClient.GetAsync(
                $"{ReleaseBaseUrl}/{ReleaseTag}/{platform.AssetName}",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = new FileStream(
                temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long downloadedBytes = 0;
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                downloadedBytes += bytesRead;
                if (totalBytes > 0)
                    progress?.Report((double)downloadedBytes / totalBytes.Value);
            }

            await destination.FlushAsync(cancellationToken);
            destination.Close();

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporaryPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            if (!await IsFfmpegAsync(temporaryPath, cancellationToken))
                throw new InvalidDataException("The downloaded file is not a valid ffmpeg executable.");

            File.Move(temporaryPath, destinationPath, true);
            progress?.Report(1);
            return destinationPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static async Task<bool> IsFfmpegAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return false;

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                ArgumentList = { "-version" },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process is null) return false;

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static PlatformDownload GetPlatform()
    {
        var architecture = RuntimeInformation.OSArchitecture;
        if (OperatingSystem.IsWindows() && architecture == Architecture.X64)
            return new("ffmpeg-win32-x64", "ffmpeg.exe", "Windows x64");

        if (OperatingSystem.IsLinux())
        {
            return architecture switch
            {
                Architecture.X64 => new("ffmpeg-linux-x64", "ffmpeg", "Linux x64"),
                Architecture.X86 => new("ffmpeg-linux-ia32", "ffmpeg", "Linux x86"),
                Architecture.Arm => new("ffmpeg-linux-arm", "ffmpeg", "Linux ARM"),
                Architecture.Arm64 => new("ffmpeg-linux-arm64", "ffmpeg", "Linux ARM64"),
                _ => throw UnsupportedPlatform()
            };
        }

        if (OperatingSystem.IsMacOS())
        {
            return architecture switch
            {
                Architecture.X64 => new("ffmpeg-darwin-x64", "ffmpeg", "macOS Intel"),
                Architecture.Arm64 => new("ffmpeg-darwin-arm64", "ffmpeg", "macOS Apple Silicon"),
                _ => throw UnsupportedPlatform()
            };
        }

        throw UnsupportedPlatform();
    }

    private static PlatformNotSupportedException UnsupportedPlatform() =>
        new($"Automatic ffmpeg download is not available for {RuntimeInformation.OSDescription} " +
            $"({RuntimeInformation.OSArchitecture}). You can still select an existing executable.");

    private sealed record PlatformDownload(string AssetName, string ExecutableName, string Description);
}
