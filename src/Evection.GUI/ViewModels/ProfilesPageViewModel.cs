using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Evection.Detector;
using Evection.GUI.Services;
using Evection.Installer;

namespace Evection.GUI.ViewModels;

/// <summary>One profile card on the profiles page.</summary>
public sealed class ProfileCardViewModel(GameInfo game, ModProfile profile, bool isActive)
{
    public GameInfo Game { get; } = game;
    public ModProfile Profile { get; } = profile;
    public string Name => Profile.Name;
    public string GameName => Game.Name;
    public bool IsActive { get; } = isActive;
    public int ModCount { get; } = new ModsFolder(profile).List().Count(m => m.Enabled);

    public string Details =>
        $"{ModCount} mod{(ModCount == 1 ? "" : "s")} · {(Profile.LastPlayedUtc is { } t ? "played " + Ago(t) : "never played")}";

    private static string Ago(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        return span.TotalMinutes < 1 ? "just now"
            : span.TotalHours < 1 ? $"{(int)span.TotalMinutes} min ago"
            : span.TotalDays < 1 ? $"{(int)span.TotalHours} h ago"
            : span.TotalDays < 2 ? "yesterday"
            : span.TotalDays < 30 ? $"{(int)span.TotalDays} days ago"
            : utc.ToLocalTime().ToString("MMM d").ToLowerInvariant();
    }
}

/// <summary>All mod profiles across every installed game, like a launcher library.</summary>
public sealed partial class ProfilesPageViewModel(ShellViewModel shell) : PageViewModel(shell)
{
    private List<ProfileCardViewModel> all = [];

    public override string NavKey => "profiles";

    public ObservableCollection<ProfileCardViewModel> Items { get; } = [];
    public ObservableCollection<string> GameFilters { get; } = ["all games"];

    [ObservableProperty] private int gameFilter;
    /// <summary>0 = recently played, 1 = name.</summary>
    [ObservableProperty] private int sortIndex;
    [ObservableProperty] private string countText = "";
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool hasInstalledGames;

    public string[] SortOptions { get; } = ["recently played", "name"];

    partial void OnGameFilterChanged(int value) => Apply();
    partial void OnSortIndexChanged(int value) => Apply();

    private IEnumerable<GameInfo> InstalledGames => App.Library.Games.Where(g => g.EvectionInstalled);

    public override Task OnShownAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    public void Refresh()
    {
        var cards = new List<ProfileCardViewModel>();
        foreach (var game in InstalledGames)
        {
            try
            {
                var active = ModProfiles.GetActive(game).Id;
                cards.AddRange(ModProfiles.List(game).Select(p => new ProfileCardViewModel(game, p, p.Id == active)));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InstallException)
            {
                // Game folder unavailable (e.g. unmounted drive) — skip it.
            }
        }
        all = cards;

        var filter = GameFilter;
        GameFilters.Clear();
        GameFilters.Add("all games");
        foreach (var name in all.Select(c => c.GameName).Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            GameFilters.Add(name);
        GameFilter = filter < GameFilters.Count ? filter : 0;
        HasInstalledGames = InstalledGames.Any();
        Apply();
    }

    private void Apply()
    {
        IEnumerable<ProfileCardViewModel> cards = all;
        if (GameFilter > 0 && GameFilter < GameFilters.Count)
            cards = cards.Where(c => c.GameName == GameFilters[GameFilter]);
        cards = SortIndex == 1
            ? cards.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.GameName)
            : cards.OrderByDescending(c => c.Profile.LastPlayedUtc ?? DateTime.MinValue).ThenBy(c => c.GameName).ThenBy(c => c.Name);

        Items.Clear();
        foreach (var c in cards)
            Items.Add(c);
        CountText = $"{Items.Count} profile{(Items.Count == 1 ? "" : "s")}";
        IsEmpty = Items.Count == 0;
    }

    [RelayCommand]
    private async Task NewProfile()
    {
        var games = InstalledGames.Select(g => new GameItemViewModel(g)).ToList();
        if (games.Count == 0)
        {
            App.Toasts.Show("install evection hook into a game first");
            return;
        }
        IReadOnlyList<string> CopyOptions(GameItemViewModel? g) =>
            g == null ? [] : ModProfiles.List(g.Info).Select(p => p.Name).ToList();

        var preselect = GameFilter > 0 ? games.FirstOrDefault(g => g.Name == GameFilters[GameFilter]) : null;
        var dialog = new ProfileDialogViewModel("new profile", "create", "", CopyOptions(preselect ?? games[0]),
            validate: null, games: games)
        {
            CopyOptionsForGame = CopyOptions,
        };
        if (preselect != null)
            dialog.SelectedGame = preselect;

        var result = await Shell.ProfileDialogAsync(dialog);
        if (result?.SelectedGame is not { } game)
            return;
        try
        {
            var profiles = ModProfiles.List(game.Info);
            var copyFrom = result.CopyFromIndex > 0 ? profiles[result.CopyFromIndex - 1].Id : null;
            var created = ModProfiles.Create(game.Info, result.Name, copyFrom);
            App.Toasts.Success($"created {created.Name}");
            Shell.OpenProfile(game.Info, created.Id);
        }
        catch (Exception e) when (e is InstallException or IOException)
        {
            App.Toasts.Error(e.Message);
        }
    }

    [RelayCommand]
    private void Play(ProfileCardViewModel card) => Shell.Launch(card.Game, card.Profile.Id);

    [RelayCommand]
    private void Open(ProfileCardViewModel card) => Shell.OpenProfile(card.Game, card.Profile.Id);
}
