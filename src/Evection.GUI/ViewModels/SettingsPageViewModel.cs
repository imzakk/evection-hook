using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Evection.GUI.Services;
using Evection.Inspector;

namespace Evection.GUI.ViewModels;

/// <summary>Settings: General · Appearance · Account · Advanced · About. Every change saves immediately.</summary>
public sealed partial class SettingsPageViewModel : PageViewModel
{
    public SettingsPageViewModel(ShellViewModel shell) : base(shell)
    {
        App.Settings.Saved += () => Avalonia.Threading.Dispatcher.UIThread.Post(RaiseAll);
    }

    public override string NavKey => "settings";

    [ObservableProperty] private int selectedTab;
    [ObservableProperty] private bool isSyncing;

    private AppSettings S => App.Settings.Current;

    private void Set(Action<AppSettings> change, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        App.Settings.Update(change);
        OnPropertyChanged(name);
    }

    // ---- General ----
    public bool ShowIntro { get => S.ShowIntro; set => Set(s => s.ShowIntro = value); }
    public bool ScanSteamOnStart { get => S.ScanSteamOnStart; set => Set(s => s.ScanSteamOnStart = value); }
    public bool ConfirmUninstall { get => S.ConfirmUninstall; set => Set(s => s.ConfirmUninstall = value); }
    public bool KeepModsOnUninstall { get => S.KeepModsOnUninstall; set => Set(s => s.KeepModsOnUninstall = value); }

    /// <summary>Applies to every game evection hook is installed in.</summary>
    public bool ModsOnlyFromEvection
    {
        get => S.ModsOnlyFromEvection;
        set
        {
            Set(s => s.ModsOnlyFromEvection = value);
            foreach (var game in App.Library.Games.Where(g => g.EvectionInstalled))
            {
                try { Installer.ModLoaderInstaller.SetModsOnlyFromEvection(game, value); }
                catch (IOException e) { App.Toasts.Error($"{game.Name}: {e.Message}"); }
            }
        }
    }
    public ObservableCollection<string> ExtraFolders { get; } = [];

    [RelayCommand]
    private async Task AddFolder() => await Shell.Games.AddFolderCommand.ExecuteAsync(null);

    [RelayCommand]
    private void RemoveFolder(string folder)
    {
        var game = App.Library.Games.FirstOrDefault(g => g.GameDirectory == folder);
        if (game != null)
            App.Library.RemoveFolder(game);
        else
            App.Settings.Update(s => s.ExtraGameFolders.Remove(folder));
    }

    // ---- Appearance ----
    public string[] ThemeOptions { get; } = ["dark", "light", "system"];

    public int ThemeIndex
    {
        get => (int)S.Theme;
        set
        {
            Set(s => s.Theme = (AppTheme)value);
            Evection.GUI.App.ApplyTheme((AppTheme)value);
        }
    }

    public bool ReduceMotion { get => S.ReduceMotion; set => Set(s => s.ReduceMotion = value); }

    // ---- Account ----
    public bool SyncAvailable => App.Sync.IsAvailable;
    public bool IsSignedIn => App.Sync.IsSignedIn;
    public string AccountName => S.Account?.DisplayName ?? "";
    public string LastSyncedText => S.LastSyncedUtc is { } t ? $"last synced {t.ToLocalTime():g}" : "not synced yet";

    [RelayCommand]
    private async Task SignIn()
    {
        IsSyncing = true;
        try
        {
            var account = await App.Sync.SignInAsync(CancellationToken.None);
            App.Toasts.Success($"signed in as {account.DisplayName}");
        }
        catch (SyncException e)
        {
            App.Toasts.Error(e.Message);
        }
        finally
        {
            IsSyncing = false;
            RaiseAll();
            Shell.RaiseAccountChanged();
        }
    }

    [RelayCommand]
    private async Task SyncNow()
    {
        IsSyncing = true;
        try
        {
            await App.Sync.PullAsync();
            await App.Sync.PushAsync();
            App.Toasts.Success("synced");
        }
        catch (SyncException e)
        {
            App.Toasts.Error(e.Message);
        }
        finally
        {
            IsSyncing = false;
            RaiseAll();
        }
    }

    [RelayCommand]
    private async Task SignOut()
    {
        await Shell.SignOutCommand.ExecuteAsync(null);
        RaiseAll();
    }

    [RelayCommand]
    private async Task DeleteCloudData()
    {
        if (!await Shell.ConfirmAsync("delete cloud data?", "removes your synced settings from the server and signs you out.", "delete", danger: true))
            return;
        try
        {
            await App.Sync.DeleteCloudDataAsync();
            App.Toasts.Show("cloud data deleted");
        }
        catch (SyncException e)
        {
            App.Toasts.Error(e.Message);
        }
        RaiseAll();
        Shell.RaiseAccountChanged();
    }

    // ---- Advanced ----
    public string PayloadOverride
    {
        get => S.PayloadOverride ?? "";
        set => Set(s => s.PayloadOverride = string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    public string SyncServerOverride
    {
        get => S.SyncServerOverride ?? "";
        set
        {
            Set(s => s.SyncServerOverride = string.IsNullOrWhiteSpace(value) ? null : value.Trim());
            OnPropertyChanged(nameof(SyncAvailable));
        }
    }

    public string CacheFolder => GameCode.CacheDirectory;
    public string SettingsFolder => App.Settings.Directory;

    [RelayCommand] private void OpenCacheFolder() { Directory.CreateDirectory(CacheFolder); Shell.Open(CacheFolder); }
    [RelayCommand] private void OpenSettingsFolder() => Shell.Open(SettingsFolder);

    [RelayCommand]
    private async Task ClearCache()
    {
        if (!await Shell.ConfirmAsync("clear the code cache?", "it's rebuilt next time you open an il2cpp game in the code browser.", "clear"))
            return;
        try
        {
            if (Directory.Exists(CacheFolder))
                Directory.Delete(CacheFolder, recursive: true);
            App.Toasts.Success("cache cleared");
        }
        catch (IOException e)
        {
            App.Toasts.Error(e.Message);
        }
    }

    // ---- About ----
    public string Version => "evection hook " + AppServices.Version;
    public string RuntimeInfo => $".net {Environment.Version} · {System.Runtime.InteropServices.RuntimeInformation.OSDescription.ToLowerInvariant()}";

    [RelayCommand] private void OpenLink(string url) => Shell.Open(url);

    [RelayCommand] private void GoToGeneral() => SelectedTab = 0;

    private void RaiseAll()
    {
        ExtraFolders.Clear();
        foreach (var f in S.ExtraGameFolders)
            ExtraFolders.Add(f);
        OnPropertyChanged(string.Empty);
    }

    public override Task OnShownAsync()
    {
        RaiseAll();
        return Task.CompletedTask;
    }
}
