using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Evection.Detector;
using Evection.GUI.Services;

namespace Evection.GUI.ViewModels;

/// <summary>The main app: sidebar, top bar, page navigation (with back/forward), toasts, dialogs and the advanced gate.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly Stack<PageViewModel> back = new();
    private readonly Stack<PageViewModel> forward = new();
    private readonly Dictionary<string, GamePageViewModel> gamePages = new();

    public ShellViewModel(AppServices services)
    {
        Services = services;
        Home = new HomePageViewModel(this);
        Games = new GamesPageViewModel(this);
        Profiles = new ProfilesPageViewModel(this);
        CodeBrowser = new CodeBrowserPageViewModel(this);
        Settings = new SettingsPageViewModel(this);

        currentPage = Home;
        services.Library.Changed += OnLibraryChanged;
        services.Settings.Saved += () => OnPropertyChanged(nameof(AccountName));
        _ = ScanAsync();
    }

    public AppServices Services { get; }
    public ObservableCollection<Toast> Toasts => Services.Toasts.Items;
    public string Version => "v" + AppServices.Version;

    public HomePageViewModel Home { get; }
    public GamesPageViewModel Games { get; }
    public ProfilesPageViewModel Profiles { get; }
    public CodeBrowserPageViewModel CodeBrowser { get; }
    public SettingsPageViewModel Settings { get; }

    /// <summary>Games with Evection Hook installed — listed in the sidebar like playlists.</summary>
    public ObservableCollection<GameItemViewModel> InstalledGames { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NavKey))]
    [NotifyCanExecuteChangedFor(nameof(GoBackCommand), nameof(GoForwardCommand))]
    private PageViewModel currentPage;

    [ObservableProperty]
    private bool isScanning;

    [ObservableProperty]
    private string searchText = "";

    /// <summary>The open modal: a <see cref="DialogViewModel"/> or <see cref="ProfileDialogViewModel"/>.</summary>
    [ObservableProperty]
    private object? dialog;

    [ObservableProperty]
    private bool isAccountMenuOpen;

    public string NavKey => CurrentPage.NavKey;

    // ---- Account ----
    public bool IsSignedIn => Services.Sync.IsSignedIn;
    public string AccountName => Services.Settings.Current.Account?.DisplayName ?? "not signed in";
    public string AccountInitial => IsSignedIn ? AccountName[..1].ToUpperInvariant() : "";
    public string AccountSubtitle => IsSignedIn ? "sync on" : "sync off";

    public void RaiseAccountChanged()
    {
        OnPropertyChanged(nameof(IsSignedIn));
        OnPropertyChanged(nameof(AccountName));
        OnPropertyChanged(nameof(AccountInitial));
        OnPropertyChanged(nameof(AccountSubtitle));
    }

    // ---- Navigation ----
    public void Navigate(PageViewModel page)
    {
        if (page == CurrentPage)
            return;
        back.Push(CurrentPage);
        forward.Clear();
        Show(page);
    }

    private void Show(PageViewModel page)
    {
        CurrentPage.OnHidden();
        CurrentPage = page;
        IsAccountMenuOpen = false;
        _ = page.OnShownAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack()
    {
        forward.Push(CurrentPage);
        Show(back.Pop());
    }

    private bool CanGoBack() => back.Count > 0;

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void GoForward()
    {
        back.Push(CurrentPage);
        Show(forward.Pop());
    }

    private bool CanGoForward() => forward.Count > 0;

    [RelayCommand] private void GoHome() => Navigate(Home);
    [RelayCommand] private void GoGames() => Navigate(Games);
    [RelayCommand] private void GoProfiles() => Navigate(Profiles);
    [RelayCommand] private void GoCodeBrowser() => Navigate(CodeBrowser);

    [RelayCommand]
    private void GoSettings(string? tab)
    {
        if (int.TryParse(tab, out var index))
            Settings.SelectedTab = index;
        Navigate(Settings);
    }

    [RelayCommand]
    public void OpenGame(GameItemViewModel? game)
    {
        if (game == null)
            return;
        if (!gamePages.TryGetValue(game.Key, out var page))
            gamePages[game.Key] = page = new GamePageViewModel(this, game.Info);
        Navigate(page);
    }

    public void OpenGame(GameInfo info) => OpenGame(new GameItemViewModel(info));

    /// <summary>Opens a game's page on the mods tab with a profile selected.</summary>
    public void OpenProfile(GameInfo game, string profileId)
    {
        OpenGame(game);
        if (CurrentPage is GamePageViewModel page)
        {
            page.SelectProfile(profileId);
            page.SelectedTab = 1;
        }
    }

    [RelayCommand]
    public void Play(GameItemViewModel? game)
    {
        if (game != null)
            Launch(game.Info, null);
    }

    /// <summary>Starts a game with a profile (null = its active profile).</summary>
    public void Launch(GameInfo game, string? profileId)
    {
        try
        {
            Installer.GameLauncher.Launch(game, profileId);
            var profile = game.EvectionInstalled ? Installer.ModProfiles.GetActive(game).Name : null;
            Services.Toasts.Show(profile == null ? $"starting {game.Name}…" : $"starting {game.Name} ({profile})…");
            RaiseInstalledChanged();
        }
        catch (Exception e) when (e is Installer.InstallException or IOException)
        {
            Services.Toasts.Error(e.Message);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        Games.Filter = value;
        if (!string.IsNullOrWhiteSpace(value) && CurrentPage != Games)
            Navigate(Games);
    }

    // ---- Account menu ----
    [RelayCommand]
    private void ToggleAccountMenu() => IsAccountMenuOpen = !IsAccountMenuOpen;

    [RelayCommand]
    private void OpenAccount()
    {
        IsAccountMenuOpen = false;
        GoSettings("2");
    }

    [RelayCommand]
    private async Task SignOut()
    {
        IsAccountMenuOpen = false;
        if (await ConfirmAsync("sign out?", "your settings stay on this computer but stop syncing.", "sign out"))
        {
            Services.Sync.SignOut();
            RaiseAccountChanged();
            Services.Toasts.Show("signed out");
        }
    }

    // ---- Library ----
    public async Task ScanAsync()
    {
        IsScanning = true;
        try
        {
            await Services.Library.ScanAsync();
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void OnLibraryChanged()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            InstalledGames.Clear();
            foreach (var g in Services.Library.Games.Where(g => g.EvectionInstalled))
                InstalledGames.Add(new GameItemViewModel(g));
            Home.Refresh();
            Games.Refresh();
            Profiles.Refresh();
            CodeBrowser.RefreshGames();
        });
    }

    /// <summary>Refresh the sidebar list after installing/uninstalling or adding mods.</summary>
    public void RaiseInstalledChanged() => OnLibraryChanged();

    public void Open(string path) => Evection.GUI.Services.Shell.Open(path);

    // ---- Dialogs ----
    /// <summary>Shows the new/rename profile dialog. Returns null if cancelled.</summary>
    public Task<ProfileDialogViewModel?> ProfileDialogAsync(ProfileDialogViewModel dialogModel)
    {
        var tcs = new TaskCompletionSource<ProfileDialogViewModel?>();
        dialogModel.Close = result =>
        {
            Dialog = null;
            tcs.TrySetResult(result ? dialogModel : null);
        };
        Dialog = dialogModel;
        return tcs.Task;
    }

    public void CancelDialog()
    {
        switch (Dialog)
        {
            case DialogViewModel d: d.CancelCommand.Execute(null); break;
            case ProfileDialogViewModel p: p.CancelCommand.Execute(null); break;
        }
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText, bool danger = false)
    {
        var tcs = new TaskCompletionSource<bool>();
        Dialog = new DialogViewModel(title, message, confirmText, danger, result =>
        {
            Dialog = null;
            tcs.TrySetResult(result);
        });
        return tcs.Task;
    }
}

public sealed partial class DialogViewModel(string title, string message, string confirmText, bool danger, Action<bool> close) : ObservableObject
{
    public string Title { get; } = title;
    public string Message { get; } = message;
    public string ConfirmText { get; } = confirmText;
    public bool Danger { get; } = danger;

    [RelayCommand] private void Confirm() => close(true);
    [RelayCommand] private void Cancel() => close(false);
}
