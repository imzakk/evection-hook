using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Evection.GUI.Services;

namespace Evection.GUI.ViewModels;

/// <summary>Switches between the three top-level screens: cover → sign-in → app.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly AppServices services;
    private ShellViewModel? shell;

    [ObservableProperty]
    private object screen;

    public MainWindowViewModel(AppServices services, bool showCover = true)
    {
        this.services = services;
        screen = showCover && services.Settings.Current.ShowIntro ? new CoverViewModel(this) : NextAfterCover();
    }

    public AppServices Services => services;

    internal void CoverFinished() => Screen = NextAfterCover();

    private object NextAfterCover() =>
        !services.Settings.Current.SignInPromptSeen && services.Sync.IsAvailable && !services.Sync.IsSignedIn
            ? new SignInViewModel(this)
            : Shell();

    internal void SignInFinished()
    {
        services.Settings.Update(s => s.SignInPromptSeen = true);
        Screen = Shell();
    }

    public ShellViewModel Shell() => shell ??= new ShellViewModel(services);
}

public sealed partial class CoverViewModel(MainWindowViewModel main) : ObservableObject
{
    public string Version => "v" + AppServices.Version;
    public bool ReduceMotion => main.Services.Settings.Current.ReduceMotion;

    [RelayCommand]
    private void GetStarted() => main.CoverFinished();
}

public sealed partial class SignInViewModel(MainWindowViewModel main) : ObservableObject
{
    private CancellationTokenSource? cancel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonText))]
    private bool isBusy;

    [ObservableProperty]
    private string? error;

    public string ButtonText => IsBusy ? "waiting for your browser…" : "sign in with discord";
    public string Version => "v" + AppServices.Version;

    [RelayCommand]
    private async Task SignIn()
    {
        if (IsBusy)
        {
            cancel?.Cancel();
            return;
        }
        Error = null;
        IsBusy = true;
        cancel = new CancellationTokenSource();
        try
        {
            var account = await main.Services.Sync.SignInAsync(cancel.Token);
            main.Services.Toasts.Success($"signed in as {account.DisplayName}");
            main.SignInFinished();
        }
        catch (SyncException e)
        {
            Error = e.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Continue() => main.SignInFinished();
}
