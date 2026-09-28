using System.Text.Json;
using System.Text.Json.Serialization;

namespace Evection.Installer;

/// <summary>Written to EvectionHook/install.json so uninstall removes exactly what install added.</summary>
public sealed class InstallManifest
{
    public const string FileName = "install.json";

    public string Version { get; set; } = "";
    public string Backend { get; set; } = "";
    public string Architecture { get; set; } = "";
    public DateTime InstalledAtUtc { get; set; }

    /// <summary>Files added to the game folder, relative to the game directory, using '/' separators.</summary>
    public List<string> Files { get; set; } = [];

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static InstallManifest? Load(string evectionDirectory)
    {
        var path = Path.Combine(evectionDirectory, FileName);
        return File.Exists(path) ? JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(path), Options) : null;
    }

    public void Save(string evectionDirectory) =>
        File.WriteAllText(Path.Combine(evectionDirectory, FileName), JsonSerializer.Serialize(this, Options));
}
