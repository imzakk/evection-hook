using Avalonia.Platform.Storage;

namespace Evection.GUI.Services;

/// <summary>File/folder pickers and clipboard, provided by the main window.</summary>
public interface IDialogs
{
    Task<string?> PickFolderAsync(string title);
    Task<IReadOnlyList<string>> PickFilesAsync(string title, string patternName, params string[] patterns);
    Task CopyAsync(string text);
}

/// <summary>Shared app-wide services, created once in <see cref="App"/>.</summary>
public sealed class AppServices
{
    public AppServices(SettingsStore? settings = null)
    {
        Settings = settings ?? new SettingsStore();
        Library = new GameLibrary(Settings);
        Sync = new SyncService(Settings);
        Settings.ChangedByUser += ScheduleSync;
    }

    private IDisposable? syncTimer;

    /// <summary>Uploads settings a few seconds after the last change, when signed in.</summary>
    private void ScheduleSync()
    {
        if (!Sync.IsSignedIn || Avalonia.Application.Current == null)
            return;
        syncTimer?.Dispose();
        syncTimer = Avalonia.Threading.DispatcherTimer.RunOnce(async () =>
        {
            try { await Sync.PushAsync(); }
            catch (SyncException e) { Toasts.Show("couldn't sync: " + e.Message, ToastKind.Warning); }
        }, TimeSpan.FromSeconds(3));
    }

    public SettingsStore Settings { get; }
    public GameLibrary Library { get; }
    public SyncService Sync { get; }
    public Toasts Toasts { get; } = new();
    public IDialogs? Dialogs { get; set; }

    public static string Version =>
        typeof(AppServices).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";

    public static IStorageProvider? Storage { get; set; }
}
