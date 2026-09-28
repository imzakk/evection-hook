using CommunityToolkit.Mvvm.ComponentModel;

namespace Evection.GUI.ViewModels;

public abstract class PageViewModel(ShellViewModel shell) : ObservableObject
{
    public ShellViewModel Shell { get; } = shell;
    public Services.AppServices App => Shell.Services;

    /// <summary>Which sidebar item is highlighted while this page is open.</summary>
    public abstract string NavKey { get; }

    /// <summary>Called every time the page is shown (including via back/forward).</summary>
    public virtual Task OnShownAsync() => Task.CompletedTask;

    /// <summary>Called when navigating away.</summary>
    public virtual void OnHidden() { }
}
