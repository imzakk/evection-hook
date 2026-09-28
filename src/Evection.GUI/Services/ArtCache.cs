using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using Evection.Detector;

namespace Evection.GUI.Services;

/// <summary>
/// Loads game images: Steam's local library cache first, then Steam's public CDN (saved to the app's cache folder).
/// </summary>
public static class ArtCache
{
    private static readonly ConcurrentDictionary<(int, SteamArtKind), Task<Bitmap?>> Loaded = new();
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static string Directory => Path.Combine(
        OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } xdg ? xdg
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache"),
        "evection-hook", "art");

    public static Task<Bitmap?> GetAsync(int appId, SteamArtKind kind) =>
        Loaded.GetOrAdd((appId, kind), key => Task.Run(() => LoadAsync(key.Item1, key.Item2)));

    private static async Task<Bitmap?> LoadAsync(int appId, SteamArtKind kind)
    {
        try
        {
            var path = SteamArt.FindLocal(appId, kind) ?? await DownloadAsync(appId, kind);
            if (path == null)
                return null;
            await using var stream = File.OpenRead(path);
            // Decode at roughly the size it's shown, to keep memory low.
            return kind switch
            {
                SteamArtKind.Cover => Bitmap.DecodeToWidth(stream, 360),
                SteamArtKind.Header => Bitmap.DecodeToWidth(stream, 460),
                _ => new Bitmap(stream),
            };
        }
        catch (Exception e) when (e is IOException or HttpRequestException or TaskCanceledException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task<string?> DownloadAsync(int appId, SteamArtKind kind)
    {
        var url = SteamArt.CdnUrl(appId, kind);
        if (url == null)
            return null;
        var file = Path.Combine(Directory, $"{appId}_{kind.ToString().ToLowerInvariant()}.jpg");
        if (File.Exists(file))
            return file;
        using var response = await Http.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;
        System.IO.Directory.CreateDirectory(Directory);
        await File.WriteAllBytesAsync(file, await response.Content.ReadAsByteArrayAsync());
        return file;
    }
}
