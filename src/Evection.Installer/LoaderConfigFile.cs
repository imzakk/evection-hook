using Evection.Detector;

namespace Evection.Installer;

/// <summary>
/// EvectionHook/loader.cfg — read by the in-game loader at startup.
/// <code>
/// profile = default
/// mods_only_from_evection = false
/// </code>
/// </summary>
public sealed class LoaderConfigFile
{
    public const string FileName = "loader.cfg";
    public const string ProfileKey = "profile";
    public const string ModsOnlyFromEvectionKey = "mods_only_from_evection";

    private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

    private LoaderConfigFile(string path) => FilePath = path;

    public string FilePath { get; }

    public static LoaderConfigFile Load(GameInfo game)
    {
        var file = new LoaderConfigFile(Path.Combine(game.EvectionDirectory, FileName));
        if (File.Exists(file.FilePath))
        {
            foreach (var line in File.ReadAllLines(file.FilePath))
            {
                var eq = line.IndexOf('=');
                if (eq > 0 && !line.TrimStart().StartsWith('#'))
                    file.values[line[..eq].Trim()] = line[(eq + 1)..].Trim();
            }
        }
        return file;
    }

    public string? Get(string key) => values.TryGetValue(key, out var v) ? v : null;

    public LoaderConfigFile Set(string key, string value)
    {
        values[key] = value;
        return this;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var lines = new List<string> { "# evection hook loader settings (edited by the app)" };
        lines.AddRange(values.Select(kv => $"{kv.Key} = {kv.Value}"));
        File.WriteAllLines(FilePath, lines);
    }
}
