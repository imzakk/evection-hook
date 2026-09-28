using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Evection.GUI.Services;
using Evection.GUI.ViewModels;

namespace Evection.GUI.Views;

public sealed partial class MainWindow : Window, IDialogs
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.Services.Dialogs = this;
        };
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    public async Task<IReadOnlyList<string>> PickFilesAsync(string title, string patternName, params string[] patterns)
    {
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType(patternName) { Patterns = patterns }],
        });
        return result.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
    }

    public async Task CopyAsync(string text)
    {
        if (Clipboard != null)
            await Clipboard.SetTextAsync(text);
    }
}
