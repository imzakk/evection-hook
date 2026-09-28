using System.Text.RegularExpressions;

namespace Evection.Detector;

/// <summary>Finds Unity games installed through Steam.</summary>
public static partial class SteamLibrary
{
    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial Regex LibraryPathPattern();

    [GeneratedRegex("\"installdir\"\\s+\"([^\"]+)\"")]
    private static partial Regex InstallDirPattern();

    [GeneratedRegex("\"appid\"\\s+\"(\\d+)\"")]
    private static partial Regex AppIdPattern();

    [GeneratedRegex("\"name\"\\s+\"([^\"]+)\"")]
    private static partial Regex NamePattern();

    /// <summary>
    /// The Steam app id of a game folder: from steamapps/appmanifest_*.acf (matching "installdir"),
    /// or steam_appid.txt next to the game.
    /// </summary>
    public static int? FindAppId(string gameDirectory) => FindApp(gameDirectory)?.AppId;

    /// <summary>App id and store name ("Bloons TD 6" rather than the exe's "BloonsTD6").</summary>
    public static (int AppId, string? Name)? FindApp(string gameDirectory)
    {
        try
        {
            var appIdFile = Path.Combine(gameDirectory, "steam_appid.txt");
            int? idFromFile = File.Exists(appIdFile) && int.TryParse(File.ReadAllText(appIdFile).Trim(), out var fromFile) ? fromFile : null;

            var common = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(gameDirectory));
            var steamapps = common == null ? null : Path.GetDirectoryName(common);
            if (steamapps == null || !string.Equals(Path.GetFileName(common), "common", StringComparison.OrdinalIgnoreCase))
                return idFromFile is { } id0 ? (id0, null) : null;

            var folder = Path.GetFileName(Path.TrimEndingDirectorySeparator(gameDirectory));
            foreach (var manifest in Directory.EnumerateFiles(steamapps, "appmanifest_*.acf"))
            {
                var text = File.ReadAllText(manifest);
                var dir = InstallDirPattern().Match(text);
                if (dir.Success && string.Equals(dir.Groups[1].Value, folder, StringComparison.OrdinalIgnoreCase))
                {
                    var id = AppIdPattern().Match(text);
                    if (id.Success && int.TryParse(id.Groups[1].Value, out var appId))
                    {
                        var name = NamePattern().Match(text);
                        return (appId, name.Success ? name.Groups[1].Value : null);
                    }
                }
            }
            if (idFromFile is { } id1)
                return (id1, null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
        return null;
    }

    /// <summary>Path to Steam's own executable, if it can be found.</summary>
    public static string? FindSteamExecutable()
    {
        if (OperatingSystem.IsWindows())
        {
            return FindSteamRoots().Select(r => Path.Combine(r, "steam.exe")).FirstOrDefault(File.Exists);
        }
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(dir, "steam");
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    public static IEnumerable<string> FindSteamRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = OperatingSystem.IsWindows()
            ? new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"),
            }
            : new[]
            {
                Path.Combine(home, ".steam", "steam"),
                Path.Combine(home, ".local", "share", "Steam"),
                Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"),
            };

        return candidates
            .Where(Directory.Exists)
            .Select(ResolveLinks)
            .Distinct(StringComparer.Ordinal);
    }

    /// <summary>All steamapps/common folders across every Steam library.</summary>
    public static IEnumerable<string> FindLibraryFolders()
    {
        var libraries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in FindSteamRoots())
        {
            libraries.Add(root);
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
                continue;
            foreach (Match match in LibraryPathPattern().Matches(File.ReadAllText(vdf)))
            {
                var path = match.Groups[1].Value.Replace(@"\\", @"\");
                if (Directory.Exists(path))
                    libraries.Add(ResolveLinks(path));
            }
        }

        return libraries
            .Select(lib => Path.Combine(lib, "steamapps", "common"))
            .Where(Directory.Exists);
    }

    public static IReadOnlyList<GameInfo> FindUnityGames()
    {
        var games = new Dictionary<string, GameInfo>(StringComparer.Ordinal);
        foreach (var common in FindLibraryFolders())
        {
            foreach (var dir in Directory.EnumerateDirectories(common))
            {
                var game = GameDetector.TryDetect(dir);
                if (game != null)
                    games.TryAdd(ResolveLinks(game.GameDirectory), game);
            }
        }
        return games.Values.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string ResolveLinks(string path)
    {
        try
        {
            var info = new DirectoryInfo(path);
            return info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? info.FullName;
        }
        catch (IOException)
        {
            return path;
        }
    }
}
