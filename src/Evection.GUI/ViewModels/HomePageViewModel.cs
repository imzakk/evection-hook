using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Evection.GUI.ViewModels;

public sealed partial class HomePageViewModel(ShellViewModel shell) : PageViewModel(shell)
{
    public override string NavKey => "home";

    public string Greeting => DateTime.Now.Hour switch
    {
        < 5 => "good evening",
        < 12 => "good morning",
        < 18 => "good afternoon",
        _ => "good evening",
    };

    public ObservableCollection<GameItemViewModel> InstalledGames { get; } = [];
    public ObservableCollection<GameItemViewModel> SuggestedGames { get; } = [];

    [ObservableProperty] private int gamesFound;
    [ObservableProperty] private int installedCount;
    [ObservableProperty] private int modCount;
    [ObservableProperty] private bool hasInstalled;
    [ObservableProperty] private bool hasGames;

    public bool IsScanning => Shell.IsScanning;

    public void Refresh()
    {
        var all = App.Library.Games.Select(g => new GameItemViewModel(g)).ToList();
        InstalledGames.Clear();
        SuggestedGames.Clear();
        foreach (var g in all.Where(g => g.IsInstalled))
            InstalledGames.Add(g);
        foreach (var g in all.Where(g => !g.IsInstalled && !g.HasAntiCheat && !g.IsLinuxNative).Take(6))
            SuggestedGames.Add(g);

        GamesFound = all.Count;
        InstalledCount = InstalledGames.Count;
        ModCount = InstalledGames.Sum(g => g.ModCount);
        HasInstalled = InstalledCount > 0;
        HasGames = GamesFound > 0;
    }

    [RelayCommand]
    private void BrowseGames() => Shell.Navigate(Shell.Games);
}
