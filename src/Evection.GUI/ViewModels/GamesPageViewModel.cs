using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Evection.Detector;
using Evection.GUI.Services;

namespace Evection.GUI.ViewModels;

public sealed partial class GamesPageViewModel(ShellViewModel shell) : PageViewModel(shell)
{
    public override string NavKey => "games";

    public ObservableCollection<GameItemViewModel> Items { get; } = [];

    /// <summary>0 = All, 1 = Installed, 2 = Mono, 3 = IL2CPP, 4 = Not supported.</summary>
    [ObservableProperty] private int selectedTab;
    [ObservableProperty] private string filter = "";
    [ObservableProperty] private bool gridView = true;
    [ObservableProperty] private string countText = "";
    [ObservableProperty] private bool isEmpty;

    partial void OnSelectedTabChanged(int value) => Refresh();
    partial void OnFilterChanged(string value) => Refresh();

    public void Refresh()
    {
        var games = App.Library.Games.Select(g => new GameItemViewModel(g));
        games = SelectedTab switch
        {
            1 => games.Where(g => g.IsInstalled),
            2 => games.Where(g => g.IsMono && !g.HasAntiCheat),
            3 => games.Where(g => g.IsIl2Cpp && !g.HasAntiCheat),
            4 => games.Where(g => g.HasAntiCheat || g.IsLinuxNative || g.Info.Backend == ScriptingBackend.Unknown),
            _ => games,
        };
        if (!string.IsNullOrWhiteSpace(Filter))
            games = games.Where(g => g.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase));

        Items.Clear();
        foreach (var g in games)
            Items.Add(g);
        CountText = $"{Items.Count} game{(Items.Count == 1 ? "" : "s")}";
        IsEmpty = Items.Count == 0;
    }

    [RelayCommand] private void ShowGrid() => GridView = true;
    [RelayCommand] private void ShowList() => GridView = false;

    [RelayCommand]
    private async Task Rescan()
    {
        await Shell.ScanAsync();
        App.Toasts.Show($"found {App.Library.Games.Count} games");
    }

    [RelayCommand]
    private async Task AddFolder()
    {
        if (App.Dialogs == null)
            return;
        var folder = await App.Dialogs.PickFolderAsync("choose the game's folder");
        if (folder == null)
            return;
        try
        {
            var game = App.Library.AddFolder(folder);
            App.Toasts.Success($"added {game.Name}");
            Shell.OpenGame(game);
        }
        catch (GameDetectionException e)
        {
            App.Toasts.Error(e.Message);
        }
    }
}
