using System.Text.Json;

namespace Evection.GUI.Services;

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON in the user's config folder.</summary>
public sealed class SettingsStore
{
    public SettingsStore(string? directory = null)
    {
        Directory = directory ?? DefaultDirectory();
        Current = Load();
    }

    public string Directory { get; }
    public string FilePath => Path.Combine(Directory, "settings.json");
    public AppSettings Current { get; private set; }

    /// <summary>Raised after every save, including ones caused by syncing (re-apply theme, refresh UI).</summary>
    public event Action? Saved;

    /// <summary>Raised when the player changed something (not when a sync did) — triggers an upload.</summary>
    public event Action? ChangedByUser;

    public static string DefaultDirectory() => OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EvectionHook")
        : Path.Combine(
            Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
            "evection-hook");

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), AppSettings.Json) ?? new AppSettings();
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            // Corrupt settings shouldn't stop the app from starting; keep a copy for debugging and start fresh.
            try { File.Copy(FilePath, FilePath + ".broken", overwrite: true); } catch (IOException) { }
        }
        return new AppSettings();
    }

    public void Save() => Save(byUser: true);

    /// <summary>Save without triggering another sync (used by the sync service itself).</summary>
    internal void SaveFromSync() => Save(byUser: false);

    private void Save(bool byUser)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Current, AppSettings.Json));
        File.Move(tmp, FilePath, overwrite: true);
        Saved?.Invoke();
        if (byUser)
            ChangedByUser?.Invoke();
    }

    /// <summary>Change settings and save in one step.</summary>
    public void Update(Action<AppSettings> change)
    {
        change(Current);
        Save();
    }
}
