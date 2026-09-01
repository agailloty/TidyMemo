using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TidyMemo.Models;

namespace TidyMemo.Services;

public interface ISlideshowProjectStore
{
    Task<SlideshowProject> LoadAsync(string projectPath, CancellationToken cancellationToken = default);
    Task SaveAsync(string projectPath, SlideshowProject project, CancellationToken cancellationToken = default);
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(SlideshowProject))]
internal partial class SlideshowProjectJsonContext : JsonSerializerContext { }

public sealed class JsonSlideshowProjectStore : ISlideshowProjectStore
{
    public async Task<SlideshowProject> LoadAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(projectPath);
        var project = await JsonSerializer.DeserializeAsync(
            stream, SlideshowProjectJsonContext.Default.SlideshowProject, cancellationToken);
        if (project is null)
            throw new InvalidDataException("The slideshow project is empty or invalid.");
        if (project.SchemaVersion is < 1 or > SlideshowProject.CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported slideshow project version: {project.SchemaVersion}.");
        project.SchemaVersion = SlideshowProject.CurrentSchemaVersion;
        return project;
    }

    public async Task SaveAsync(string projectPath, SlideshowProject project, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(projectPath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The project path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = fullPath + ".tmp";
        var backupPath = fullPath + ".bak";

        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                             4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream, project, SlideshowProjectJsonContext.Default.SlideshowProject, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (File.Exists(fullPath))
                File.Copy(fullPath, backupPath, true);
            File.Move(temporaryPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}

public static class SlideshowProjectPaths
{
    public static string ToStoredPath(string? path, string projectPath)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
        return Path.GetRelativePath(projectDirectory, Path.GetFullPath(path));
    }

    public static string? ToAbsolutePath(string? path, string projectPath)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
        return Path.GetFullPath(Path.Combine(projectDirectory, path));
    }
}
