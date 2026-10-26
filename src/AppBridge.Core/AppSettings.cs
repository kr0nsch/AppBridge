using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppBridge.Core;

public enum VersionPolicy
{
    /// <summary>Install the newest version the App Store offers.</summary>
    Latest,

    /// <summary>Install exactly the version found on the source iPhone, when Apple still serves it.</summary>
    SameAsSource,
}

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

public sealed class AppSettings
{
    /// <summary>Folder for downloaded IPA files; null means the default for this copy.</summary>
    public string? DownloadDirectory { get; set; }

    public bool KeepDownloads { get; set; } = true;

    public VersionPolicy VersionPolicy { get; set; } = VersionPolicy.Latest;

    /// <summary>Let ipatool add free apps to the signed-in account when they are not in its purchases yet.</summary>
    public bool AcquireFreeLicenses { get; set; } = true;

    public ThemePreference Theme { get; set; } = ThemePreference.System;

    /// <summary>Empty for the Windows display language, otherwise a BCP-47 tag such as "ru-RU".</summary>
    public string Language { get; set; } = "";
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;

public sealed class SettingsStore
{
    private readonly string _path;
    private readonly Lock _gate = new();

    public SettingsStore(string path)
    {
        _path = path;
        Current = Load(path);
    }

    public AppSettings Current { get; }

    public void Save()
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Current, SettingsJsonContext.Default.AppSettings));
            File.Move(temp, _path, overwrite: true);
        }
    }

    private static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize(File.ReadAllText(path), SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            AppLog.Warn($"Settings file is unreadable and will be replaced: {ex.Message}");
        }

        return new AppSettings();
    }
}
