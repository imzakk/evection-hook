using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Evection.GUI.ViewModels;

namespace Evection.GUI.Views.Pages;

public sealed partial class GamePage : UserControl
{
    private INotifyCollectionChanged? logLines;

    public GamePage()
    {
        InitializeComponent();
        ModsDropArea.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
        });
        ModsDropArea.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (DataContext is not GamePageViewModel vm || !vm.IsInstalled)
                return;
            var files = e.Data.GetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();
            if (files is { Count: > 0 })
                vm.AddModFiles(files);
        });
        DataContextChanged += (_, _) =>
        {
            if (logLines != null)
                logLines.CollectionChanged -= ScrollLogToEnd;
            logLines = (DataContext as GamePageViewModel)?.LogLines;
            if (logLines != null)
                logLines.CollectionChanged += ScrollLogToEnd;
        };
    }

    private void ScrollLogToEnd(object? sender, NotifyCollectionChangedEventArgs e) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => LogScroll?.ScrollToEnd(), Avalonia.Threading.DispatcherPriority.Background);
}
