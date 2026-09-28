using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Evection.Detector;
using Evection.GUI.Services;
using Evection.Inspector;

namespace Evection.GUI.ViewModels;

public sealed record CodeResultViewModel(string Icon, string Title, string Subtitle, string TypeName);

/// <summary>Modder tools: browse, search and decompile a game's code, and export it as a project.</summary>
public sealed partial class CodeBrowserPageViewModel(ShellViewModel shell) : PageViewModel(shell)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private GameCode? code;
    private CodeBrowser? browser;
    private CancellationTokenSource? searchCancel;

    public override string NavKey => "code";

    public ObservableCollection<GameItemViewModel> Games { get; } = [];
    public ObservableCollection<CodeResultViewModel> Results { get; } = [];

    [ObservableProperty] private GameItemViewModel? selectedGame;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isLoaded;
    [ObservableProperty] private string loadingText = "";
    [ObservableProperty] private string query = "";
    /// <summary>0 = classes, 1 = fields/properties/methods.</summary>
    [ObservableProperty] private int searchMode;
    [ObservableProperty] private string resultsText = "";
    [ObservableProperty] private CodeResultViewModel? selectedResult;
    [ObservableProperty] private int detailTab;
    [ObservableProperty] private string outline = "";
    [ObservableProperty] private string source = "";
    [ObservableProperty] private bool isDecompiling;
    [ObservableProperty] private bool signaturesOnly;
    [ObservableProperty] private bool isExporting;

    public bool HasSelection => SelectedResult != null;

    public bool IncludeLibraries
    {
        get => App.Settings.Current.IncludeLibrariesInCodeBrowser;
        set
        {
            App.Settings.Update(s => s.IncludeLibrariesInCodeBrowser = value);
            OnPropertyChanged();
            if (code != null)
            {
                browser = new CodeBrowser(code, value);
                _ = SearchAsync();
            }
        }
    }

    public void RefreshGames()
    {
        var selected = SelectedGame?.Key;
        Games.Clear();
        foreach (var g in App.Library.Games.Where(g => g.Backend != ScriptingBackend.Unknown))
            Games.Add(new GameItemViewModel(g));
        if (selected != null)
            SelectedGame = Games.FirstOrDefault(g => g.Key == selected);
    }

    public void SelectGame(GameInfo game)
    {
        if (Games.Count == 0)
            RefreshGames();
        SelectedGame = Games.FirstOrDefault(g => g.Key == game.GameDirectory);
    }

    public override Task OnShownAsync()
    {
        if (Games.Count == 0)
            RefreshGames();
        return Task.CompletedTask;
    }

    partial void OnSelectedGameChanged(GameItemViewModel? value)
    {
        code = null;
        browser = null;
        IsLoaded = false;
        Results.Clear();
        SelectedResult = null;
        if (value != null)
            _ = LoadAsync(value.Info);
    }

    private async Task LoadAsync(GameInfo game)
    {
        IsLoading = true;
        LoadingText = game.Backend == ScriptingBackend.Il2Cpp
            ? "rebuilding il2cpp code, this takes a minute the first time…"
            : "loading…";
        try
        {
            var loaded = await Task.Run(() => GameCode.Load(game));
            if (SelectedGame?.Key != game.GameDirectory)
                return;
            code = loaded;
            browser = new CodeBrowser(loaded, IncludeLibraries);
            SignaturesOnly = loaded.SignaturesOnly;
            IsLoaded = true;
            await SearchAsync();
        }
        catch (InspectorException e)
        {
            App.Toasts.Error(e.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnQueryChanged(string value) => _ = SearchAsync(debounce: true);
    partial void OnSearchModeChanged(int value) => _ = SearchAsync();

    private async Task SearchAsync(bool debounce = false)
    {
        searchCancel?.Cancel();
        var cancel = searchCancel = new CancellationTokenSource();
        var b = browser;
        if (b == null)
            return;
        if (debounce)
        {
            try { await Task.Delay(250, cancel.Token); }
            catch (TaskCanceledException) { return; }
        }

        var query = Query.Trim();
        var mode = SearchMode;
        List<CodeResultViewModel> results;
        var total = 0;
        await gate.WaitAsync();
        try
        {
            (results, total) = await Task.Run(() =>
            {
                IEnumerable<CodeResultViewModel> items = mode == 0 || query.Length == 0
                    ? b.FindTypes(query.Length == 0 ? null : query).Select(t => new CodeResultViewModel(
                        "code", t.Type.Name, TypeLocation(t.Type, t.Assembly), t.Type.FullName))
                    : b.Search(query).Where(h => h.Kind != MemberKind.Type).Select(h => new CodeResultViewModel(
                        h.Kind switch { MemberKind.Method => "terminal", MemberKind.Property => "settings", _ => "edit" },
                        $"{ShortName(h.TypeName)}.{h.MemberName}", h.Signature, h.TypeName));
                var list = items.Take(501).ToList();
                return (list.Take(500).ToList(), list.Count);
            }, cancel.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            gate.Release();
        }
        if (cancel.IsCancellationRequested)
            return;

        Results.Clear();
        foreach (var r in results)
            Results.Add(r);
        ResultsText = total > 500 ? "first 500 results" : $"{results.Count} result{(results.Count == 1 ? "" : "s")}";
    }

    private static string TypeLocation(ICSharpCode.Decompiler.TypeSystem.ITypeDefinition type, string assembly) =>
        type.DeclaringTypeDefinition is { } parent ? $"in {parent.Name} · {assembly}"
        : string.IsNullOrEmpty(type.Namespace) ? assembly
        : $"{type.Namespace} · {assembly}";

    private static string ShortName(string fullName)
    {
        var dot = fullName.LastIndexOf('.');
        return dot >= 0 ? fullName[(dot + 1)..] : fullName;
    }

    partial void OnSelectedResultChanged(CodeResultViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        if (value != null)
            _ = ShowTypeAsync(value.TypeName);
    }

    private async Task ShowTypeAsync(string typeName)
    {
        var b = browser;
        if (b == null)
            return;
        IsDecompiling = true;
        await gate.WaitAsync();
        try
        {
            var (o, s) = await Task.Run(() =>
            {
                try { return (b.Describe(typeName), b.Source(typeName)); }
                catch (Exception e) when (e is InspectorException or InvalidOperationException) { return (e.Message, "// " + e.Message); }
            });
            if (SelectedResult?.TypeName == typeName)
            {
                Outline = o;
                Source = s;
            }
        }
        finally
        {
            gate.Release();
            IsDecompiling = false;
        }
    }

    [RelayCommand]
    private async Task CopySource()
    {
        if (App.Dialogs != null && Source.Length > 0)
        {
            await App.Dialogs.CopyAsync(DetailTab == 0 ? Outline : Source);
            App.Toasts.Success("copied");
        }
    }

    [RelayCommand]
    private async Task Export()
    {
        if (code == null || App.Dialogs == null || SelectedGame == null)
            return;
        var folder = await App.Dialogs.PickFolderAsync("export to…");
        if (folder == null)
            return;

        var target = Path.Combine(folder, string.Concat(SelectedGame.Name.Split(Path.GetInvalidFileNameChars())) + " (source)");
        var assemblies = IncludeLibraries ? code.AllAssemblies : code.GameAssemblies;
        IsExporting = true;
        App.Toasts.Show("exporting…", ToastKind.Info);
        try
        {
            await Task.Run(() => ProjectExporter.Export(code, assemblies, target));
            App.Toasts.Show("export done", ToastKind.Success, "open", () => Shell.Open(target), seconds: 10);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InspectorException)
        {
            App.Toasts.Error("export failed: " + e.Message);
        }
        finally
        {
            IsExporting = false;
        }
    }

    [RelayCommand]
    private void OpenWritingModsGuide() => Shell.Open(Links.WritingMods);
}
