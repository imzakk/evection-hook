using System.Text.Json;
using System.Text.RegularExpressions;
using Evection.Detector;

namespace Evection.Installer;

/// <summary>A named set of mods (and their settings) for one game.</summary>
public sealed class ModProfile
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required string Directory { get; init; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? LastPlayedUtc { get; set; }

    public string ModsDirectory => Path.Combine(Directory, "Mods");
    public string ConfigDirectory => Path.Combine(Directory, "Config");
}

/// <summary>
/// Profiles live in &lt;Game&gt;/EvectionHook/Profiles/&lt;id&gt;/ (Mods/, Config/, profile.json).
/// The active one is written to loader.cfg, so launching from Steam uses it too.
/// </summary>
public static partial class ModProfiles
{
    public const string DefaultId = "default";

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlug();

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string Root(GameInfo game) => Path.Combine(game.EvectionDirectory, "Profiles");

    /// <summary>All profiles, creating "default" (and moving pre-profile mods into it) the first time.</summary>
    public static IReadOnlyList<ModProfile> List(GameInfo game)
    {
        EnsureMigrated(game);
        return System.IO.Directory.EnumerateDirectories(Root(game))
            .Select(Load)
            .OfType<ModProfile>()
            .OrderBy(p => p.Id == DefaultId ? 0 : 1)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static ModProfile Get(GameInfo game, string id) =>
        List(game).FirstOrDefault(p => p.Id == id)
        ?? throw new InstallException($"no profile called '{id}'.");

    /// <summary>Finds a profile by id or (case-insensitive) name.</summary>
    public static ModProfile Find(GameInfo game, string idOrName)
    {
        var all = List(game);
        return all.FirstOrDefault(p => p.Id == idOrName)
            ?? all.FirstOrDefault(p => string.Equals(p.Name, idOrName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InstallException($"no profile called '{idOrName}'. profiles: {string.Join(", ", all.Select(p => p.Name))}");
    }

    public static ModProfile GetActive(GameInfo game)
    {
        var id = LoaderConfigFile.Load(game).Get(LoaderConfigFile.ProfileKey) ?? DefaultId;
        var all = List(game);
        return all.FirstOrDefault(p => p.Id == id) ?? all.First();
    }

    public static void SetActive(GameInfo game, string id)
    {
        Get(game, id);
        LoaderConfigFile.Load(game).Set(LoaderConfigFile.ProfileKey, id).Save();
    }

    public static ModProfile Create(GameInfo game, string name, string? copyFromId = null)
    {
        name = name.Trim();
        if (name.Length == 0)
            throw new InstallException("give the profile a name.");
        if (List(game).Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InstallException($"there's already a profile called '{name}'.");

        var id = UniqueId(game, name);
        var dir = Path.Combine(Root(game), id);
        if (copyFromId != null)
            CopyDirectory(Get(game, copyFromId).Directory, dir);
        var profile = new ModProfile { Id = id, Name = name, Directory = dir, CreatedUtc = DateTime.UtcNow };
        Save(profile);
        return profile;
    }

    public static void Rename(GameInfo game, string id, string newName)
    {
        newName = newName.Trim();
        if (newName.Length == 0)
            throw new InstallException("give the profile a name.");
        if (List(game).Any(p => p.Id != id && string.Equals(p.Name, newName, StringComparison.OrdinalIgnoreCase)))
            throw new InstallException($"there's already a profile called '{newName}'.");
        var profile = Get(game, id);
        profile.Name = newName;
        Save(profile);
    }

    public static void Delete(GameInfo game, string id)
    {
        var all = List(game);
        if (all.Count == 1)
            throw new InstallException("you need at least one profile.");
        var profile = Get(game, id);
        var wasActive = GetActive(game).Id == id;
        System.IO.Directory.Delete(profile.Directory, recursive: true);
        if (wasActive)
            SetActive(game, all.First(p => p.Id != id).Id);
    }

    public static void MarkPlayed(GameInfo game, string id)
    {
        var profile = Get(game, id);
        profile.LastPlayedUtc = DateTime.UtcNow;
        Save(profile);
    }

    /// <summary>Moves mods from before profiles existed (EvectionHook/Mods, /Config) into the "default" profile.</summary>
    public static void EnsureMigrated(GameInfo game)
    {
        var root = Root(game);
        if (System.IO.Directory.Exists(root) && System.IO.Directory.EnumerateDirectories(root).Any())
            return;

        var dir = Path.Combine(root, DefaultId);
        var profile = new ModProfile { Id = DefaultId, Name = DefaultId, Directory = dir, CreatedUtc = DateTime.UtcNow };
        System.IO.Directory.CreateDirectory(profile.ModsDirectory);
        System.IO.Directory.CreateDirectory(profile.ConfigDirectory);
        foreach (var (old, target) in new[] { ("Mods", profile.ModsDirectory), ("Config", profile.ConfigDirectory) })
        {
            var legacy = Path.Combine(game.EvectionDirectory, old);
            if (!System.IO.Directory.Exists(legacy))
                continue;
            CopyDirectory(legacy, target);
            System.IO.Directory.Delete(legacy, recursive: true);
        }
        Save(profile);
    }

    private static ModProfile? Load(string dir)
    {
        var json = Path.Combine(dir, "profile.json");
        var id = Path.GetFileName(dir);
        try
        {
            var data = File.Exists(json) ? JsonSerializer.Deserialize<ProfileData>(File.ReadAllText(json), Json) : null;
            return new ModProfile
            {
                Id = id,
                Name = data?.Name ?? id,
                Directory = dir,
                CreatedUtc = data?.CreatedUtc ?? System.IO.Directory.GetCreationTimeUtc(dir),
                LastPlayedUtc = data?.LastPlayedUtc,
            };
        }
        catch (JsonException)
        {
            return new ModProfile { Id = id, Name = id, Directory = dir };
        }
    }

    private static void Save(ModProfile profile)
    {
        System.IO.Directory.CreateDirectory(profile.ModsDirectory);
        System.IO.Directory.CreateDirectory(profile.ConfigDirectory);
        var data = new ProfileData(profile.Name, profile.CreatedUtc, profile.LastPlayedUtc);
        File.WriteAllText(Path.Combine(profile.Directory, "profile.json"), JsonSerializer.Serialize(data, Json));
    }

    private static string UniqueId(GameInfo game, string name)
    {
        var slug = NonSlug().Replace(name.ToLowerInvariant(), "-").Trim('-');
        if (slug.Length == 0)
            slug = "profile";
        var id = slug;
        for (var i = 2; System.IO.Directory.Exists(Path.Combine(Root(game), id)); i++)
            id = $"{slug}-{i}";
        return id;
    }

    private static void CopyDirectory(string source, string target)
    {
        System.IO.Directory.CreateDirectory(target);
        foreach (var file in System.IO.Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var dest = Path.Combine(target, Path.GetRelativePath(source, file));
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }

    private sealed record ProfileData(string Name, DateTime CreatedUtc, DateTime? LastPlayedUtc);
}
