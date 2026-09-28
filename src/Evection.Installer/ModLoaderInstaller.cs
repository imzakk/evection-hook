using System.Reflection;
using Evection.Detector;

namespace Evection.Installer;

public sealed class InstallException(string message) : Exception(message);

public sealed record InstallResult(GameInfo Game, InstallManifest Manifest, string ModsDirectory, IReadOnlyList<string> Notes);

public static class ModLoaderInstaller
{
    public const string ProxyDll = "winhttp.dll";
    public const string DoorstopConfig = "doorstop_config.ini";

    public static string Version =>
        typeof(ModLoaderInstaller).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    /// <summary>Reasons the game can't have Evection Hook installed, or an empty list if it can.</summary>
    public static IReadOnlyList<string> CheckCompatibility(GameInfo game, Payload? payload = null)
    {
        var problems = new List<string>();
        if (game.HasAntiCheat)
            problems.Add($"uses anti-cheat ({string.Join(", ", game.AntiCheats.Select(a => a.Name).Distinct())}). not supported, it could get you banned.");
        if (game.Platform != GamePlatform.Windows)
            problems.Add("this is the linux version. install the windows version through proton instead.");
        if (game.Backend == ScriptingBackend.Unknown)
            problems.Add("couldn't tell if this game is mono or il2cpp.");
        if (game.Architecture is not (CpuArchitecture.X64 or CpuArchitecture.X86))
            problems.Add($"unsupported cpu architecture: {game.Architecture}.");
        foreach (var loader in game.OtherModLoaders)
            problems.Add($"{loader.Name} is already installed ({loader.Evidence}). remove it first.");
        if (payload != null && game.Backend != ScriptingBackend.Unknown && !payload.Supports(game.Backend))
            problems.Add($"{game.Backend.ToString().ToLowerInvariant()} games aren't supported yet.");
        return problems;
    }

    public static InstallResult Install(GameInfo game, Payload payload, bool modsOnlyFromEvection = false)
    {
        var problems = CheckCompatibility(game, payload);
        if (problems.Count > 0)
            throw new InstallException(string.Join(Environment.NewLine, problems));

        // Reinstall = remove old loader files first, but keep the user's mods, configs and logs.
        if (game.EvectionInstalled)
            Uninstall(game, keepUserData: true);

        var evection = game.EvectionDirectory;
        var added = new List<string>();

        void Copy(string source, string relativeDest)
        {
            var dest = Path.Combine(game.GameDirectory, relativeDest);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(source, dest, overwrite: true);
            added.Add(relativeDest.Replace('\\', '/'));
        }

        void CopyTree(string sourceDir, string relativeDest)
        {
            foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
                Copy(file, Path.Combine(relativeDest, Path.GetRelativePath(sourceDir, file)));
        }

        Copy(Path.Combine(payload.DoorstopDirectory(game.Architecture), ProxyDll), ProxyDll);

        string targetAssembly;
        if (game.Backend == ScriptingBackend.Mono)
        {
            CopyTree(payload.CoreMonoDirectory, Path.Combine("EvectionHook", "core"));
            targetAssembly = @"EvectionHook\core\Evection.Core.Mono.dll";
        }
        else
        {
            CopyTree(payload.CoreIl2CppDirectory, Path.Combine("EvectionHook", "core"));
            CopyTree(payload.DotnetDirectory, Path.Combine("EvectionHook", "dotnet"));
            targetAssembly = @"EvectionHook\core\Evection.Core.Il2Cpp.dll";
        }

        File.WriteAllText(Path.Combine(game.GameDirectory, DoorstopConfig), DoorstopConfigText(targetAssembly, game.Backend));
        added.Add(DoorstopConfig);

        Directory.CreateDirectory(Path.Combine(evection, "Logs"));
        ModProfiles.EnsureMigrated(game);
        SetModsOnlyFromEvection(game, modsOnlyFromEvection);
        var mods = ModProfiles.GetActive(game).ModsDirectory;

        var manifest = new InstallManifest
        {
            Version = Version,
            Backend = game.Backend.ToString(),
            Architecture = game.Architecture.ToString(),
            InstalledAtUtc = DateTime.UtcNow,
            Files = added,
        };
        manifest.Save(evection);

        var notes = new List<string>();
        if (!OperatingSystem.IsWindows())
        {
            notes.Add("on linux/proton, set the game's steam launch options to:");
            notes.Add("    WINEDLLOVERRIDES=\"winhttp=n,b\" %command%");
        }
        return new InstallResult(game, manifest, mods, notes);
    }

    /// <summary>
    /// Removes Evection Hook. With <paramref name="keepUserData"/> the Mods, Config and Logs folders stay,
    /// so mods survive reinstalls and updates.
    /// </summary>
    public static void Uninstall(GameInfo game, bool keepUserData)
    {
        var evection = game.EvectionDirectory;
        var manifest = InstallManifest.Load(evection)
            ?? throw new InstallException($"Evection Hook isn't installed in {game.GameDirectory}.");

        var root = Path.TrimEndingDirectorySeparator(game.GameDirectory) + Path.DirectorySeparatorChar;
        foreach (var relative in manifest.Files)
        {
            var path = Path.GetFullPath(Path.Combine(game.GameDirectory, relative));
            if (!path.StartsWith(root, StringComparison.Ordinal))
                continue; // never touch anything outside the game folder, even if the manifest was edited
            if (File.Exists(path))
                File.Delete(path);
        }

        File.Delete(Path.Combine(evection, InstallManifest.FileName));
        foreach (var generated in new[] { "core", "dotnet", "interop", "cache" })
        {
            var dir = Path.Combine(evection, generated);
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }

        if (!keepUserData && Directory.Exists(evection))
            Directory.Delete(evection, recursive: true);
    }

    /// <summary>
    /// When true, the loader only turns mods on if the game was started by <see cref="GameLauncher"/>;
    /// launching from Steam directly gives the unmodded game.
    /// </summary>
    public static void SetModsOnlyFromEvection(GameInfo game, bool value) =>
        LoaderConfigFile.Load(game).Set(LoaderConfigFile.ModsOnlyFromEvectionKey, value ? "true" : "false").Save();

    public static bool GetModsOnlyFromEvection(GameInfo game) =>
        LoaderConfigFile.Load(game).Get(LoaderConfigFile.ModsOnlyFromEvectionKey) == "true";

    private static string DoorstopConfigText(string targetAssembly, ScriptingBackend backend) =>
        $"""
        # Generated by Evection Hook {Version}. Uninstall with Evection Hook rather than editing by hand.
        [General]
        enabled=true
        target_assembly={targetAssembly}
        redirect_output_log=false
        boot_config_override=
        ignore_disable_switch=false

        [UnityMono]
        dll_search_path_override=
        debug_enabled=false
        debug_address=127.0.0.1:10000
        debug_suspend=false

        [Il2Cpp]
        coreclr_path={(backend == ScriptingBackend.Il2Cpp ? @"EvectionHook\dotnet\coreclr.dll" : "")}
        corlib_dir={(backend == ScriptingBackend.Il2Cpp ? @"EvectionHook\dotnet" : "")}

        """;
}
