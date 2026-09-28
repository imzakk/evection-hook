using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Evection.Detector;
using Evection.GUI.Services;
using Evection.Installer;

namespace Evection.GUI.ViewModels;

/// <summary>One game: Overview · Mods · Mod settings · Logs · Advanced.</summary>
public sealed partial class GamePageViewModel : PageViewModel
{
    private readonly DispatcherTimer logTimer;
    private DateTime logWriteTime;

    public GamePageViewModel(ShellViewModel shell, GameInfo game) : base(shell)
    {
        this.game = game;
        logTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        logTimer.Tick += (_, _) => LoadLog();
        Load();
    }

    public override string NavKey => "game:" + Game.GameDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Item))]
    private GameInfo game;

    public GameItemViewModel Item => new(Game);

    [ObservableProperty] private int selectedTab;
    [ObservableProperty] private bool isBusy;

    // ---- Overview ----
    public ObservableCollection<string> Problems { get; } = [];
    [ObservableProperty] private bool canInstall;
    [ObservableProperty] private string statusTitle = "";
    [ObservableProperty] private string statusText = "";

    public bool IsInstalled => Game.EvectionInstalled;
    public bool NeedsProtonOption => !OperatingSystem.IsWindows() && Game.Platform == GamePlatform.Windows;
    public string ProtonLaunchOption => "WINEDLLOVERRIDES=\"winhttp=n,b\" %command%";
    public bool HasStatusText => StatusText.Length > 0;
    public string SteamAppIdText => Game.SteamAppId?.ToString() ?? "not a steam game";
    public bool IsManuallyAdded => App.Settings.Current.ExtraGameFolders.Contains(Game.GameDirectory);

    // ---- Profiles ----
    public ObservableCollection<ModProfile> Profiles { get; } = [];
    [ObservableProperty] private ModProfile? selectedProfile;
    private bool loadingProfiles;

    public string PlayText => SelectedProfile is { } p && Profiles.Count > 1 ? $"play {p.Name}" : "play";

    // ---- Mods ----
    public ObservableCollection<ModItemViewModel> Mods { get; } = [];
    [ObservableProperty] private bool hasMods;

    // ---- Mod settings ----
    public ObservableCollection<ModConfigViewModel> Configs { get; } = [];
    [ObservableProperty] private bool hasConfigs;

    // ---- Logs ----
    public ObservableCollection<LogLineViewModel> LogLines { get; } = [];
    [ObservableProperty] private bool hasLog;
    [ObservableProperty] private bool followLog = true;

    // ---- Advanced ----
    [ObservableProperty] private string installedFiles = "";
    [ObservableProperty] private string doorstopConfig = "";

    public override Task OnShownAsync()
    {
        Game = App.Library.Refresh(Game);
        Load();
        UpdateLogTimer();
        return Task.CompletedTask;
    }

    public override void OnHidden() => logTimer.Stop();

    partial void OnSelectedTabChanged(int value) => UpdateLogTimer();

    private void UpdateLogTimer()
    {
        if (SelectedTab == 3 && Shell.CurrentPage == this)
        {
            LoadLog();
            logTimer.Start();
        }
        else
        {
            logTimer.Stop();
        }
    }

    private void Load()
    {
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(IsManuallyAdded));

        Problems.Clear();
        Payload? payload = null;
        try { payload = Payload.Locate(App.Settings.Current.PayloadOverride); }
        catch (InstallException) { }
        foreach (var p in ModLoaderInstaller.CheckCompatibility(Game, payload))
            Problems.Add(p);
        if (payload == null)
            Problems.Add("the loader files (payload folder) are missing. re-download evection hook.");
        CanInstall = Problems.Count == 0;

        (StatusTitle, StatusText) = (IsInstalled, CanInstall) switch
        {
            (true, _) => ("installed", "add mods, then hit play."),
            (false, true) => ("not installed", "adds a few files to the game folder. you can remove it any time."),
            _ => ("can't install", ""),
        };
        OnPropertyChanged(nameof(HasStatusText));

        LoadProfiles();
        LoadAdvanced();
    }

    // ================= Profiles =================

    private void LoadProfiles(string? select = null)
    {
        loadingProfiles = true;
        Profiles.Clear();
        if (IsInstalled)
        {
            try
            {
                foreach (var p in ModProfiles.List(Game))
                    Profiles.Add(p);
                select ??= SelectedProfile?.Id ?? ModProfiles.GetActive(Game).Id;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InstallException)
            {
                App.Toasts.Error(e.Message);
            }
        }
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == select) ?? Profiles.FirstOrDefault();
        loadingProfiles = false;
        LoadMods();
        LoadConfigs();
        OnPropertyChanged(nameof(PlayText));
    }

    public void SelectProfile(string id) => LoadProfiles(id);

    partial void OnSelectedProfileChanged(ModProfile? value)
    {
        OnPropertyChanged(nameof(PlayText));
        if (loadingProfiles || value == null)
            return;
        // The selected profile is the one used when the game is launched from Steam, too.
        try { ModProfiles.SetActive(Game, value.Id); }
        catch (Exception e) when (e is IOException or InstallException) { App.Toasts.Error(e.Message); }
        LoadMods();
        LoadConfigs();
        Shell.RaiseInstalledChanged();
    }

    [RelayCommand]
    private async Task NewProfile()
    {
        var copyOptions = Profiles.Select(p => p.Name).ToList();
        var result = await Shell.ProfileDialogAsync(new ProfileDialogViewModel("new profile", "create", "", copyOptions, validate: name =>
            Profiles.Any(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)) ? "that name is taken." : null));
        if (result == null)
            return;
        try
        {
            var copyFrom = result.CopyFromIndex > 0 ? Profiles[result.CopyFromIndex - 1].Id : null;
            var created = ModProfiles.Create(Game, result.Name, copyFrom);
            LoadProfiles(created.Id);
            ModProfiles.SetActive(Game, created.Id);
            App.Toasts.Success($"created {created.Name}");
            Shell.RaiseInstalledChanged();
        }
        catch (Exception e) when (e is InstallException or IOException)
        {
            App.Toasts.Error(e.Message);
        }
    }

    [RelayCommand]
    private async Task RenameProfile()
    {
        if (SelectedProfile is not { } profile)
            return;
        var result = await Shell.ProfileDialogAsync(new ProfileDialogViewModel("rename profile", "rename", profile.Name, null, validate: name =>
            Profiles.Any(p => p.Id != profile.Id && string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)) ? "that name is taken." : null));
        if (result == null)
            return;
        try
        {
            ModProfiles.Rename(Game, profile.Id, result.Name);
            LoadProfiles(profile.Id);
            Shell.RaiseInstalledChanged();
        }
        catch (Exception e) when (e is InstallException or IOException)
        {
            App.Toasts.Error(e.Message);
        }
    }

    [RelayCommand]
    private void DuplicateProfile()
    {
        if (SelectedProfile is not { } profile)
            return;
        try
        {
            var name = profile.Name + " copy";
            for (var i = 2; Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)); i++)
                name = $"{profile.Name} copy {i}";
            var copy = ModProfiles.Create(Game, name, profile.Id);
            LoadProfiles(copy.Id);
            ModProfiles.SetActive(Game, copy.Id);
            App.Toasts.Success($"created {copy.Name}");
            Shell.RaiseInstalledChanged();
        }
        catch (Exception e) when (e is InstallException or IOException)
        {
            App.Toasts.Error(e.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteProfile()
    {
        if (SelectedProfile is not { } profile)
            return;
        if (Profiles.Count == 1)
        {
            App.Toasts.Error("you need at least one profile.");
            return;
        }
        if (!await Shell.ConfirmAsync($"delete {profile.Name}?", "its mods and mod settings are deleted.", "delete", danger: true))
            return;
        try
        {
            ModProfiles.Delete(Game, profile.Id);
            LoadProfiles(ModProfiles.GetActive(Game).Id);
            App.Toasts.Show($"deleted {profile.Name}");
            Shell.RaiseInstalledChanged();
        }
        catch (Exception e) when (e is InstallException or IOException)
        {
            App.Toasts.Error(e.Message);
        }
    }

    [RelayCommand]
    private void OpenProfileFolder()
    {
        if (SelectedProfile != null)
            Shell.Open(SelectedProfile.Directory);
    }

    // ================= Overview actions =================

    [RelayCommand]
    private async Task Install()
    {
        await Run(() =>
        {
            var payload = Payload.Locate(App.Settings.Current.PayloadOverride);
            ModLoaderInstaller.Install(Game, payload, App.Settings.Current.ModsOnlyFromEvection);
        });
        if (IsInstalled)
        {
            App.Toasts.Success($"installed into {Game.Name}");
            SelectedTab = 1;
        }
    }

    [RelayCommand]
    private async Task Uninstall()
    {
        var keep = App.Settings.Current.KeepModsOnUninstall;
        if (App.Settings.Current.ConfirmUninstall &&
            !await Shell.ConfirmAsync($"remove evection hook from {Game.Name}?",
                keep ? "your mods are kept." : "your mods and their settings will be deleted too.",
                "remove", danger: true))
            return;
        await Run(() => ModLoaderInstaller.Uninstall(Game, keepUserData: keep));
        App.Toasts.Show($"removed from {Game.Name}");
    }

    [RelayCommand]
    private async Task CopyLaunchOption()
    {
        if (App.Dialogs != null)
            await App.Dialogs.CopyAsync(ProtonLaunchOption);
        App.Toasts.Success("copied");
    }

    [RelayCommand] private void OpenGameFolder() => Shell.Open(Game.GameDirectory);

    [RelayCommand] private void Play() => Shell.Launch(Game, SelectedProfile?.Id);

    private async Task Run(Action action)
    {
        IsBusy = true;
        try
        {
            await Task.Run(action);
        }
        catch (Exception e) when (e is InstallException or IOException or UnauthorizedAccessException)
        {
            App.Toasts.Error(e is UnauthorizedAccessException ? "permission denied. is the game running?" : e.Message);
        }
        finally
        {
            Game = App.Library.Refresh(Game);
            Load();
            Shell.RaiseInstalledChanged();
            IsBusy = false;
        }
    }

    // ================= Mods =================

    private void LoadMods()
    {
        Mods.Clear();
        if (IsInstalled && SelectedProfile != null)
        {
            foreach (var file in new ModsFolder(SelectedProfile).List())
                Mods.Add(new ModItemViewModel(this, file));
            App.Settings.Current.ModLists[$"{Game.Name} / {SelectedProfile.Name}"] = Mods.Select(m => m.DisplayName).ToList();
        }
        HasMods = Mods.Count > 0;
    }

    [RelayCommand]
    private async Task AddMods()
    {
        if (App.Dialogs == null)
            return;
        var files = await App.Dialogs.PickFilesAsync("choose mod files", "mods", "*.dll");
        AddModFiles(files);
    }

    public void AddModFiles(IEnumerable<string> files)
    {
        if (SelectedProfile == null)
            return;
        var folder = new ModsFolder(SelectedProfile);
        var added = 0;
        foreach (var file in files)
        {
            try
            {
                folder.Add(file);
                added++;
            }
            catch (Exception e) when (e is InstallException or IOException)
            {
                App.Toasts.Error($"{Path.GetFileName(file)}: {e.Message}");
            }
        }
        if (added > 0)
        {
            LoadMods();
            App.Settings.Save();
            Shell.RaiseInstalledChanged();
            App.Toasts.Success($"{(added == 1 ? "mod" : $"{added} mods")} added to {SelectedProfile.Name}");
        }
    }

    [RelayCommand]
    private void OpenModsFolder()
    {
        if (SelectedProfile != null)
            Shell.Open(SelectedProfile.ModsDirectory);
    }

    internal void SetModEnabled(ModItemViewModel mod, bool enabled)
    {
        try
        {
            var profile = SelectedProfile!;
            new ModsFolder(profile).SetEnabled(mod.FileName, enabled);
            App.Toasts.Show($"{mod.DisplayName} {(enabled ? "on" : "off")}", ToastKind.Info, "undo", () =>
            {
                new ModsFolder(profile).SetEnabled(mod.FileName, !enabled);
                LoadMods();
            });
        }
        catch (Exception e) when (e is InstallException or IOException)
        {
            App.Toasts.Error(e.Message);
        }
        LoadMods();
    }

    internal async Task RemoveMod(ModItemViewModel mod)
    {
        if (!await Shell.ConfirmAsync($"remove {mod.DisplayName}?", "deletes the mod file. its settings are kept.", "remove", danger: true))
            return;
        try
        {
            File.Delete(mod.Path);
        }
        catch (IOException e)
        {
            App.Toasts.Error(e.Message);
        }
        LoadMods();
        App.Settings.Save();
        Shell.RaiseInstalledChanged();
    }

    // ================= Mod settings =================

    private void LoadConfigs()
    {
        Configs.Clear();
        if (SelectedProfile != null)
        {
            foreach (var file in ModConfigFile.LoadAll(SelectedProfile.ConfigDirectory))
                Configs.Add(new ModConfigViewModel(this, file));
        }
        HasConfigs = Configs.Count > 0;
    }

    [RelayCommand]
    private void OpenConfigFolder()
    {
        if (SelectedProfile != null)
            Shell.Open(SelectedProfile.ConfigDirectory);
    }

    // ================= Logs =================

    private void LoadLog()
    {
        var path = Game.LogFile;
        if (!File.Exists(path))
        {
            HasLog = false;
            LogLines.Clear();
            return;
        }
        var time = File.GetLastWriteTimeUtc(path);
        if (time == logWriteTime && LogLines.Count > 0)
            return;
        logWriteTime = time;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var lines = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).TakeLast(2000).ToList();
            LogLines.Clear();
            foreach (var line in lines)
                LogLines.Add(LogLineViewModel.Parse(line.TrimEnd('\r')));
            HasLog = true;
        }
        catch (IOException)
        {
            // Being written — try again next tick.
        }
    }

    [RelayCommand] private void OpenLogsFolder() => Shell.Open(Path.GetDirectoryName(Game.LogFile)!);

    [RelayCommand]
    private async Task CopyLog()
    {
        if (App.Dialogs == null || !File.Exists(Game.LogFile))
            return;
        await App.Dialogs.CopyAsync(string.Join(Environment.NewLine, LogLines.Select(l => l.Raw)));
        App.Toasts.Success("log copied");
    }

    // ================= Advanced =================

    private void LoadAdvanced()
    {
        var manifest = IsInstalled ? InstallManifest.Load(Game.EvectionDirectory) : null;
        InstalledFiles = manifest == null
            ? "not installed"
            : $"version {manifest.Version} · {manifest.Backend.ToLowerInvariant()} · {manifest.Architecture.ToLowerInvariant()} · installed {manifest.InstalledAtUtc.ToLocalTime():g}\n\n" + string.Join("\n", manifest.Files);
        var ini = Path.Combine(Game.GameDirectory, ModLoaderInstaller.DoorstopConfig);
        DoorstopConfig = File.Exists(ini) ? File.ReadAllText(ini).Trim() : "none";
    }

    [RelayCommand]
    private async Task Reinstall()
    {
        await Run(() => ModLoaderInstaller.Install(Game, Payload.Locate(App.Settings.Current.PayloadOverride), App.Settings.Current.ModsOnlyFromEvection));
        App.Toasts.Success("reinstalled");
    }

    [RelayCommand]
    private async Task RemoveEverything()
    {
        if (!await Shell.ConfirmAsync($"remove everything from {Game.Name}?",
                "deletes evection hook, mods, mod settings and logs. the game itself isn't touched.",
                "remove everything", danger: true))
            return;
        await Run(() => ModLoaderInstaller.Uninstall(Game, keepUserData: false));
        App.Toasts.Show("everything removed");
    }

    [RelayCommand] private void OpenEvectionFolder() => Shell.Open(Game.EvectionDirectory);

    [RelayCommand]
    private void RemoveFromLibrary()
    {
        App.Library.RemoveFolder(Game);
        Shell.GoHomeCommand.Execute(null);
    }

    [RelayCommand]
    private void InspectCode()
    {
        Shell.CodeBrowser.SelectGame(Game);
        Shell.Navigate(Shell.CodeBrowser);
    }
}

public sealed partial class ModItemViewModel : ObservableObject
{
    private readonly GamePageViewModel page;
    private bool enabled;

    public ModItemViewModel(GamePageViewModel page, ModFile file)
    {
        this.page = page;
        FileName = file.Name;
        Path = file.Path;
        enabled = file.Enabled;
        Metadata = ModMetadataReader.TryRead(file.Path);
    }

    public string FileName { get; }
    public string Path { get; }
    public ModMetadata? Metadata { get; }
    public string DisplayName => Metadata?.Name ?? FileName;
    public string Details => Metadata == null
        ? $"{FileName}.dll"
        : $"v{Metadata.Version} · {Metadata.Author}";
    public string? Description => Metadata?.Description;
    public bool HasDescription => !string.IsNullOrEmpty(Description);

    public bool Enabled
    {
        get => enabled;
        set
        {
            if (SetProperty(ref enabled, value))
                page.SetModEnabled(this, value);
        }
    }

    [RelayCommand] private Task Remove() => page.RemoveMod(this);
    [RelayCommand] private void Reveal() => Shell.Open(System.IO.Path.GetDirectoryName(Path)!);
}

public sealed class ModConfigViewModel
{
    public ModConfigViewModel(GamePageViewModel page, ModConfigFile file)
    {
        File = file;
        Entries = file.Entries.Select(e => new ConfigEntryViewModel(page, file, e)).ToList();
    }

    public ModConfigFile File { get; }
    public string Title => File.ModName.ToLowerInvariant();
    public IReadOnlyList<ConfigEntryViewModel> Entries { get; }
}

public sealed partial class ConfigEntryViewModel(GamePageViewModel page, ModConfigFile file, ModConfigEntry entry) : ObservableObject
{
    public string Key => entry.Key;
    public string? Description => entry.Description;
    public bool HasDescription => !string.IsNullOrEmpty(entry.Description);
    public bool IsBoolean { get; } = entry.IsBoolean;
    public bool IsText => !IsBoolean;

    public bool BoolValue
    {
        get => bool.TryParse(entry.Value, out var b) && b;
        set
        {
            entry.Value = value ? "True" : "False";
            Save();
            OnPropertyChanged();
        }
    }

    public string TextValue
    {
        get => entry.Value;
        set
        {
            if (entry.Value == value)
                return;
            entry.Value = value;
            Save();
            OnPropertyChanged();
        }
    }

    private void Save()
    {
        try
        {
            file.Save();
        }
        catch (IOException e)
        {
            page.App.Toasts.Error(e.Message);
        }
    }
}

/// <summary>A parsed line of EvectionHook/Logs/latest.log: "[12:00:00.000] [INFO ] [Source] message".</summary>
public sealed record LogLineViewModel(string Raw, string Time, string Level, string Source, string Message)
{
    public bool IsError => Level == "ERROR";
    public bool IsWarning => Level == "WARN";
    public bool IsDebug => Level == "DEBUG";

    public static LogLineViewModel Parse(string line)
    {
        var match = System.Text.RegularExpressions.Regex.Match(line, @"^\[(?<t>[^\]]+)\] \[(?<l>[^\]]+)\] \[(?<s>[^\]]+)\] (?<m>.*)$");
        return match.Success
            ? new LogLineViewModel(line, match.Groups["t"].Value, match.Groups["l"].Value.Trim(), match.Groups["s"].Value, match.Groups["m"].Value)
            : new LogLineViewModel(line, "", "", "", line);
    }
}
