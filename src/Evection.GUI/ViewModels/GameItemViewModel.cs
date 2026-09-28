using Evection.Detector;
using Evection.Installer;

namespace Evection.GUI.ViewModels;

/// <summary>A game as shown in lists, tiles and the sidebar.</summary>
public sealed class GameItemViewModel(GameInfo info)
{
    public GameInfo Info { get; } = info;
    public string Name => Info.Name;
    public string Key => Info.GameDirectory;
    public string BackendText => Info.Backend switch
    {
        ScriptingBackend.Il2Cpp => "il2cpp",
        ScriptingBackend.Mono => "mono",
        _ => "unknown",
    };
    public string Subtitle => $"{BackendText} · unity {Info.UnityVersion ?? "?"}";
    public bool IsInstalled => Info.EvectionInstalled;
    public bool HasAntiCheat => Info.HasAntiCheat;
    public bool IsLinuxNative => Info.Platform == GamePlatform.Linux;
    public bool IsMono => Info.Backend == ScriptingBackend.Mono;
    public bool IsIl2Cpp => Info.Backend == ScriptingBackend.Il2Cpp;

    /// <summary>The active profile, for installed games.</summary>
    public ModProfile? ActiveProfile
    {
        get
        {
            if (!IsInstalled)
                return null;
            try { return ModProfiles.GetActive(Info); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InstallException) { return null; }
        }
    }

    public int ModCount => ActiveProfile is { } p ? new ModsFolder(p).List().Count(m => m.Enabled) : 0;

    public string SidebarSubtitle => ActiveProfile is { } p ? $"{p.Name} · {ModCount} mod{(ModCount == 1 ? "" : "s")}" : BackendText;

    public string StatusText => HasAntiCheat ? "anti-cheat" : IsLinuxNative ? "linux build" : IsInstalled ? "installed" : "not installed";
}
