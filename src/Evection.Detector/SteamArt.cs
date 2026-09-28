namespace Evection.Detector;

public enum SteamArtKind
{
    /// <summary>Portrait library cover (600×900).</summary>
    Cover,
    /// <summary>Wide store header (460×215).</summary>
    Header,
    /// <summary>Large banner behind the game's library page.</summary>
    Hero,
    /// <summary>Small square app icon (32×32).</summary>
    Icon,
}

/// <summary>Finds the images Steam already downloaded for a game (appcache/librarycache).</summary>
public static class SteamArt
{
    private static readonly Dictionary<SteamArtKind, string[]> FileNames = new()
    {
        [SteamArtKind.Cover] = ["library_600x900.jpg", "library_capsule.jpg"],
        [SteamArtKind.Header] = ["header.jpg", "library_header.jpg"],
        [SteamArtKind.Hero] = ["library_hero.jpg"],
    };

    /// <summary>Local image path, or null if Steam hasn't cached it.</summary>
    public static string? FindLocal(int appId, SteamArtKind kind)
    {
        foreach (var root in SteamLibrary.FindSteamRoots())
        {
            var cache = Path.Combine(root, "appcache", "librarycache");
            var appFolder = Path.Combine(cache, appId.ToString());
            try
            {
                if (kind == SteamArtKind.Icon)
                {
                    // The icon is the only file named by its hash in the app folder itself.
                    var icon = Directory.Exists(appFolder)
                        ? Directory.EnumerateFiles(appFolder, "*.jpg").FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Length == 40)
                        : null;
                    if (icon != null)
                        return icon;
                    var oldIcon = Path.Combine(cache, $"{appId}_icon.jpg");
                    if (File.Exists(oldIcon))
                        return oldIcon;
                    continue;
                }

                foreach (var name in FileNames[kind])
                {
                    // Newer Steam: <appid>/<name> or <appid>/<hash>/<name>. Older Steam: <appid>_<name>.
                    var direct = Path.Combine(appFolder, name);
                    if (File.Exists(direct))
                        return direct;
                    if (Directory.Exists(appFolder))
                    {
                        var nested = Directory.EnumerateDirectories(appFolder).Select(d => Path.Combine(d, name)).FirstOrDefault(File.Exists);
                        if (nested != null)
                            return nested;
                    }
                    var flat = Path.Combine(cache, $"{appId}_{name}");
                    if (File.Exists(flat))
                        return flat;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
        return null;
    }

    /// <summary>Public Steam CDN address for an image Steam hasn't cached locally.</summary>
    public static string? CdnUrl(int appId, SteamArtKind kind) => kind switch
    {
        SteamArtKind.Cover => $"https://shared.steamstatic.com/store_item_assets/steam/apps/{appId}/library_600x900.jpg",
        SteamArtKind.Header => $"https://shared.steamstatic.com/store_item_assets/steam/apps/{appId}/header.jpg",
        SteamArtKind.Hero => $"https://shared.steamstatic.com/store_item_assets/steam/apps/{appId}/library_hero.jpg",
        _ => null,
    };
}
