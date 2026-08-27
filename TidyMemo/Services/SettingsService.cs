using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TidyMemo.Models;

namespace TidyMemo.Services;

[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsJsonContext : JsonSerializerContext { }

public class SettingsService
{
    private static readonly string SettingsPath;
    private static readonly string LegacySettingsPath;

    static SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "TidyMemo");
        Directory.CreateDirectory(folder);
        SettingsPath = Path.Combine(folder, "settings.json");
        LegacySettingsPath = Path.Combine(appData, "Exif" + "Renamer", "settings.json");
    }

    public AppSettings Load()
    {
        var sourcePath = File.Exists(SettingsPath)
            ? SettingsPath
            : LegacySettingsPath;
        if (!File.Exists(sourcePath))
            return new AppSettings();
        try
        {
            var json = File.ReadAllText(sourcePath);
            var settings = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings)
                           ?? new AppSettings();
            if (sourcePath == LegacySettingsPath)
                Save(settings);
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings);
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* silent */ }
    }
}
