using Evection.Detector;

namespace Evection.GUI.Services;

/// <summary>The list of Unity games the app knows about: Steam libraries plus folders the player added.</summary>
public sealed class GameLibrary(SettingsStore settings)
{
    private List<GameInfo> games = [];

    public IReadOnlyList<GameInfo> Games => games;
    public bool HasScanned { get; private set; }

    public event Action? Changed;

    public async Task ScanAsync()
    {
        var extra = settings.Current.ExtraGameFolders.ToList();
        var scanSteam = settings.Current.ScanSteamOnStart;
        var found = await Task.Run(() =>
        {
            var list = new Dictionary<string, GameInfo>(StringComparer.Ordinal);
            if (scanSteam)
            {
                foreach (var g in SteamLibrary.FindUnityGames())
                    list[g.GameDirectory] = g;
            }
            foreach (var folder in extra)
            {
                if (GameDetector.TryDetect(folder) is { } g)
                    list[g.GameDirectory] = g;
            }
            return list.Values.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
        });
        games = found;
        HasScanned = true;
        Changed?.Invoke();
    }

    /// <summary>Re-reads one game from disk (after installing, uninstalling...).</summary>
    public GameInfo Refresh(GameInfo game)
    {
        var fresh = GameDetector.TryDetect(game.GameDirectory) ?? game;
        var index = games.FindIndex(g => g.GameDirectory == game.GameDirectory);
        if (index >= 0)
            games[index] = fresh;
        Changed?.Invoke();
        return fresh;
    }

    /// <summary>Adds a folder the player picked. Throws <see cref="GameDetectionException"/> if it isn't a Unity game.</summary>
    public GameInfo AddFolder(string path)
    {
        var game = GameDetector.Detect(path);
        settings.Update(s =>
        {
            if (!s.ExtraGameFolders.Contains(game.GameDirectory))
                s.ExtraGameFolders.Add(game.GameDirectory);
        });
        if (games.All(g => g.GameDirectory != game.GameDirectory))
        {
            games.Add(game);
            games.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }
        Changed?.Invoke();
        return game;
    }

    public void RemoveFolder(GameInfo game)
    {
        settings.Update(s => s.ExtraGameFolders.Remove(game.GameDirectory));
        games.RemoveAll(g => g.GameDirectory == game.GameDirectory);
        Changed?.Invoke();
    }
}
