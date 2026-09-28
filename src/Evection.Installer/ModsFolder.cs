using Evection.Detector;

namespace Evection.Installer;

public sealed record ModFile(string Name, string Path, bool Enabled);

/// <summary>Lists, enables and disables the mods in one profile. Disabled mods are renamed to *.dll.disabled.</summary>
public sealed class ModsFolder(ModProfile profile)
{
    private const string DisabledSuffix = ".disabled";

    /// <summary>The active profile's mods.</summary>
    public ModsFolder(GameInfo game) : this(ModProfiles.GetActive(game)) { }

    public string Directory { get; } = profile.ModsDirectory;

    public IReadOnlyList<ModFile> List()
    {
        if (!System.IO.Directory.Exists(Directory))
            return [];
        return System.IO.Directory.EnumerateFiles(Directory)
            .Select(ToModFile)
            .OfType<ModFile>()
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public ModFile SetEnabled(string name, bool enabled)
    {
        var mod = Find(name);
        if (mod.Enabled == enabled)
            return mod;
        var target = enabled
            ? mod.Path[..^DisabledSuffix.Length]
            : mod.Path + DisabledSuffix;
        File.Move(mod.Path, target);
        return mod with { Path = target, Enabled = enabled };
    }

    /// <summary>Copies a mod .dll into the Mods folder.</summary>
    public ModFile Add(string dllPath)
    {
        if (!dllPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            throw new InstallException("Mods must be .dll files.");
        System.IO.Directory.CreateDirectory(Directory);
        var dest = System.IO.Path.Combine(Directory, System.IO.Path.GetFileName(dllPath));
        File.Copy(dllPath, dest, overwrite: true);
        return ToModFile(dest)!;
    }

    private ModFile Find(string name)
    {
        var mods = List();
        return mods.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InstallException($"No mod named '{name}'. Installed: {string.Join(", ", mods.Select(m => m.Name))}");
    }

    private static ModFile? ToModFile(string path)
    {
        var file = System.IO.Path.GetFileName(path);
        if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return new ModFile(file[..^4], path, Enabled: true);
        if (file.EndsWith(".dll" + DisabledSuffix, StringComparison.OrdinalIgnoreCase))
            return new ModFile(file[..^(4 + DisabledSuffix.Length)], path, Enabled: false);
        return null;
    }
}
