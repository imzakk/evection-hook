namespace Evection.Detector;

public static class GameDetector
{
    private static readonly string[] IgnoredExecutables = ["UnityCrashHandler64", "UnityCrashHandler32"];

    /// <summary>
    /// Inspects a game folder (or the game's .exe) and describes it.
    /// Throws <see cref="GameDetectionException"/> if it isn't a Unity game.
    /// </summary>
    public static GameInfo Detect(string path)
    {
        path = Path.GetFullPath(path);
        string gameDirectory;
        string? preferredStem = null;

        if (File.Exists(path))
        {
            gameDirectory = Path.GetDirectoryName(path)!;
            preferredStem = Path.GetFileNameWithoutExtension(path);
        }
        else if (Directory.Exists(path))
        {
            gameDirectory = path;
        }
        else
        {
            throw new GameDetectionException($"'{path}' does not exist.");
        }

        var (executable, dataDirectory, platform) = FindExecutable(gameDirectory, preferredStem)
            ?? throw new GameDetectionException(
                $"'{gameDirectory}' doesn't look like a Unity game (no <Game>_Data folder next to the game's executable).");

        var gameAssembly = Path.Combine(gameDirectory, platform == GamePlatform.Windows ? "GameAssembly.dll" : "GameAssembly.so");
        var metadata = Path.Combine(dataDirectory, "il2cpp_data", "Metadata", "global-metadata.dat");
        var managed = Path.Combine(dataDirectory, "Managed");

        ScriptingBackend backend;
        if (File.Exists(gameAssembly) && File.Exists(metadata))
            backend = ScriptingBackend.Il2Cpp;
        else if (File.Exists(Path.Combine(managed, "Assembly-CSharp.dll")) || File.Exists(Path.Combine(managed, "UnityEngine.dll")))
            backend = ScriptingBackend.Mono;
        else
            backend = ScriptingBackend.Unknown;

        var (company, product) = ReadAppInfo(dataDirectory);
        var steam = SteamLibrary.FindApp(gameDirectory);
        var installed = File.Exists(Path.Combine(gameDirectory, "EvectionHook", "install.json"));

        return new GameInfo
        {
            Name = steam?.Name ?? product ?? Path.GetFileNameWithoutExtension(executable),
            Company = company,
            GameDirectory = gameDirectory,
            ExecutablePath = executable,
            DataDirectory = dataDirectory,
            Platform = platform,
            Backend = backend,
            Architecture = platform == GamePlatform.Windows ? PeReader.ReadArchitecture(executable) : CpuArchitecture.Unknown,
            UnityVersion = UnityVersionReader.Read(dataDirectory),
            ManagedDirectory = backend == ScriptingBackend.Mono ? managed : null,
            GameAssemblyPath = backend == ScriptingBackend.Il2Cpp ? gameAssembly : null,
            MetadataPath = backend == ScriptingBackend.Il2Cpp ? metadata : null,
            AntiCheats = Signatures.FindAntiCheat(gameDirectory),
            OtherModLoaders = Signatures.FindOtherModLoaders(gameDirectory, installed),
            EvectionInstalled = installed,
            SteamAppId = steam?.AppId,
        };
    }

    /// <summary>Like <see cref="Detect"/> but returns null instead of throwing.</summary>
    public static GameInfo? TryDetect(string path)
    {
        try { return Detect(path); }
        catch (GameDetectionException) { return null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static (string Executable, string DataDirectory, GamePlatform Platform)? FindExecutable(string gameDirectory, string? preferredStem)
    {
        var candidates = new List<(string, string, GamePlatform)>();
        foreach (var dataDir in Directory.EnumerateDirectories(gameDirectory, "*_Data"))
        {
            var stem = Path.GetFileName(dataDir)[..^"_Data".Length];
            var exe = Path.Combine(gameDirectory, stem + ".exe");
            if (File.Exists(exe))
            {
                candidates.Add((exe, dataDir, GamePlatform.Windows));
                continue;
            }
            foreach (var ext in new[] { ".x86_64", ".x86" })
            {
                var linuxExe = Path.Combine(gameDirectory, stem + ext);
                if (File.Exists(linuxExe))
                    candidates.Add((linuxExe, dataDir, GamePlatform.Linux));
            }
        }

        candidates.RemoveAll(c => IgnoredExecutables.Contains(Path.GetFileNameWithoutExtension(c.Item1)));
        if (candidates.Count == 0)
            return null;

        if (preferredStem != null)
        {
            var preferred = candidates.FirstOrDefault(c => Path.GetFileNameWithoutExtension(c.Item1) == preferredStem);
            if (preferred != default)
                return preferred;
        }

        // Prefer Windows builds (Proton installs) and folders that actually contain game code.
        return candidates
            .OrderBy(c => c.Item3 == GamePlatform.Windows ? 0 : 1)
            .ThenBy(c => Directory.Exists(Path.Combine(c.Item2, "Managed")) || Directory.Exists(Path.Combine(c.Item2, "il2cpp_data")) ? 0 : 1)
            .First();
    }

    private static (string? Company, string? Product) ReadAppInfo(string dataDirectory)
    {
        var appInfo = Path.Combine(dataDirectory, "app.info");
        if (!File.Exists(appInfo))
            return (null, null);
        try
        {
            var lines = File.ReadAllLines(appInfo);
            string? Line(int i) => lines.Length > i && !string.IsNullOrWhiteSpace(lines[i]) ? lines[i].Trim() : null;
            return (Line(0), Line(1));
        }
        catch (IOException)
        {
            return (null, null);
        }
    }
}
