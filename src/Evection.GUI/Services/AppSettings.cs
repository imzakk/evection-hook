using System.Text.Json;
using System.Text.Json.Serialization;

namespace Evection.GUI.Services;

public enum AppTheme
{
    Dark,
    Light,
    System,
}

public sealed class Account
{
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Token { get; set; } = "";
}

/// <summary>
/// Everything the app remembers. Properties marked [Synced] are uploaded when signed in, so they follow the
/// player to a new computer; the rest are specific to this machine.
/// </summary>
public sealed class AppSettings
{
    // --- Synced across computers ---
    [Synced] public bool ShowIntro { get; set; } = true;
    [Synced] public AppTheme Theme { get; set; } = AppTheme.Dark;
    [Synced] public bool ReduceMotion { get; set; }
    [Synced] public bool ScanSteamOnStart { get; set; } = true;
    [Synced] public bool ConfirmUninstall { get; set; } = true;
    /// <summary>Mods only load when the game is started with "play" in evection hook, not from Steam.</summary>
    [Synced] public bool ModsOnlyFromEvection { get; set; }
    [Synced] public bool KeepModsOnUninstall { get; set; } = true;
    [Synced] public bool IncludeLibrariesInCodeBrowser { get; set; }

    /// <summary>Mod names per "game / profile", so a profile can be rebuilt on a new PC.</summary>
    [Synced] public Dictionary<string, List<string>> ModLists { get; set; } = new();

    // --- This computer only ---
    public List<string> ExtraGameFolders { get; set; } = [];
    public bool SignInPromptSeen { get; set; }
    public string? PayloadOverride { get; set; }
    public string? SyncServerOverride { get; set; }
    public Account? Account { get; set; }
    public DateTime? LastSyncedUtc { get; set; }

    internal static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>JSON of only the [Synced] properties.</summary>
    public string ToSyncedJson()
    {
        var dict = typeof(AppSettings).GetProperties()
            .Where(p => p.IsDefined(typeof(SyncedAttribute), false))
            .ToDictionary(p => p.Name, p => p.GetValue(this));
        return JsonSerializer.Serialize(dict, Json);
    }

    /// <summary>Applies [Synced] properties from JSON produced by <see cref="ToSyncedJson"/>, ignoring anything else.</summary>
    public void ApplySyncedJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        foreach (var prop in typeof(AppSettings).GetProperties().Where(p => p.IsDefined(typeof(SyncedAttribute), false)))
        {
            if (doc.RootElement.TryGetProperty(prop.Name, out var value))
                prop.SetValue(this, value.Deserialize(prop.PropertyType, Json));
        }
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class SyncedAttribute : Attribute;
