using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;

namespace Evection.GUI.Services;

public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error,
}

public sealed class Toast
{
    public required string Message { get; init; }
    public ToastKind Kind { get; init; }
    public string? ActionText { get; init; }
    public IRelayCommand? ActionCommand { get; init; }
    public IRelayCommand? DismissCommand { get; set; }

    public string Icon => Kind switch
    {
        ToastKind.Success => "check",
        ToastKind.Warning => "alert",
        ToastKind.Error => "alert",
        _ => "info",
    };

    public bool HasAction => ActionText != null;
}

/// <summary>Bottom-of-screen notifications ("Mod disabled — Undo").</summary>
public sealed class Toasts
{
    public ObservableCollection<Toast> Items { get; } = [];

    public void Show(string message, ToastKind kind = ToastKind.Info, string? actionText = null, Action? action = null, double seconds = 4)
    {
        Toast? toast = null;
        toast = new Toast
        {
            Message = message,
            Kind = kind,
            ActionText = actionText,
            ActionCommand = action == null ? null : new RelayCommand(() => { action(); Remove(toast!); }),
        };
        toast.DismissCommand = new RelayCommand(() => Remove(toast));

        Dispatcher.UIThread.Post(() =>
        {
            Items.Add(toast);
            while (Items.Count > 3)
                Items.RemoveAt(0);
            DispatcherTimer.RunOnce(() => Remove(toast), TimeSpan.FromSeconds(kind == ToastKind.Error ? seconds * 2 : seconds));
        });
    }

    public void Success(string message) => Show(message, ToastKind.Success);
    public void Error(string message) => Show(message, ToastKind.Error);

    private void Remove(Toast toast) => Dispatcher.UIThread.Post(() => Items.Remove(toast));
}
