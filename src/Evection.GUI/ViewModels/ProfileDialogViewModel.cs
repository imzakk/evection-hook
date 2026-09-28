using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Evection.GUI.ViewModels;

/// <summary>The new/rename profile modal. <see cref="CopyOptions"/> is null when renaming.</summary>
public sealed partial class ProfileDialogViewModel : ObservableObject
{
    private readonly Func<string, string?>? validate;

    public ProfileDialogViewModel(string title, string confirmText, string name, IReadOnlyList<string>? copyFrom,
        Func<string, string?>? validate = null, IReadOnlyList<GameItemViewModel>? games = null)
    {
        Title = title;
        ConfirmText = confirmText;
        this.name = name;
        this.validate = validate;
        Games = games;
        selectedGame = games?.FirstOrDefault();
        SetCopyOptions(copyFrom);
    }

    public string Title { get; }
    public string ConfirmText { get; }

    /// <summary>Only set on the profiles page, where the game has to be picked.</summary>
    public IReadOnlyList<GameItemViewModel>? Games { get; }
    public bool ShowGames => Games != null;

    [ObservableProperty] private string name;
    [ObservableProperty] private string? error;
    [ObservableProperty] private GameItemViewModel? selectedGame;
    [ObservableProperty] private IReadOnlyList<string>? copyOptions;
    [ObservableProperty] private int copyFromIndex;

    public bool ShowCopy => CopyOptions != null;

    /// <summary>Lets the profiles page refresh the copy list when the game changes.</summary>
    public Func<GameItemViewModel?, IReadOnlyList<string>>? CopyOptionsForGame { get; init; }

    internal Action<bool>? Close { get; set; }

    partial void OnSelectedGameChanged(GameItemViewModel? value)
    {
        if (CopyOptionsForGame != null)
            SetCopyOptions(CopyOptionsForGame(value));
    }

    partial void OnCopyOptionsChanged(IReadOnlyList<string>? value) => OnPropertyChanged(nameof(ShowCopy));

    private void SetCopyOptions(IReadOnlyList<string>? profiles)
    {
        CopyOptions = profiles == null ? null : ["start empty", .. profiles.Select(p => $"copy of {p}")];
        CopyFromIndex = 0;
    }

    partial void OnNameChanged(string value) => Error = null;

    [RelayCommand]
    private void Confirm()
    {
        var trimmed = Name.Trim();
        if (trimmed.Length == 0)
        {
            Error = "give it a name.";
            return;
        }
        if (ShowGames && SelectedGame == null)
        {
            Error = "pick a game.";
            return;
        }
        Error = validate?.Invoke(trimmed);
        if (Error == null)
            Close?.Invoke(true);
    }

    [RelayCommand] private void Cancel() => Close?.Invoke(false);
}
