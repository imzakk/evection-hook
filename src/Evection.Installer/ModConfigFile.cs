namespace Evection.Installer;

/// <summary>One "key = value" setting, with the comment line written above it (the mod's description of the setting).</summary>
public sealed class ModConfigEntry(string key, string value, string? description)
{
    public string Key { get; } = key;
    public string Value { get; set; } = value;
    public string? Description { get; } = description;

    public bool IsBoolean => bool.TryParse(Value, out _);
    public bool IsNumber => double.TryParse(Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
}

/// <summary>
/// Reads and writes a mod's EvectionHook/Config/&lt;Mod&gt;.cfg in the same format Evection.API's ModConfig uses,
/// so players can change mod settings from the app while the game is closed.
/// </summary>
public sealed class ModConfigFile
{
    private ModConfigFile(string path, List<ModConfigEntry> entries)
    {
        FilePath = path;
        Entries = entries;
    }

    public string FilePath { get; }
    public string ModName => Path.GetFileNameWithoutExtension(FilePath);
    public IReadOnlyList<ModConfigEntry> Entries { get; }

    public static ModConfigFile Load(string path)
    {
        var entries = new List<ModConfigEntry>();
        string? pendingComment = null;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                pendingComment = null;
                continue;
            }
            if (line.StartsWith('#'))
            {
                pendingComment = line[1..].Trim();
                continue;
            }
            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;
            entries.Add(new ModConfigEntry(line[..eq].Trim(), line[(eq + 1)..].Trim(), pendingComment));
            pendingComment = null;
        }
        return new ModConfigFile(path, entries);
    }

    public static IReadOnlyList<ModConfigFile> LoadAll(string configDirectory) =>
        Directory.Exists(configDirectory)
            ? Directory.EnumerateFiles(configDirectory, "*.cfg").OrderBy(p => p, StringComparer.OrdinalIgnoreCase).Select(Load).ToList()
            : [];

    public void Save()
    {
        var lines = new List<string>();
        foreach (var entry in Entries)
        {
            if (entry.Description != null)
                lines.Add("# " + entry.Description);
            lines.Add($"{entry.Key} = {entry.Value}");
            lines.Add("");
        }
        File.WriteAllLines(FilePath, lines);
    }
}
