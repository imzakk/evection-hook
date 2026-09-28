using System.Diagnostics;
using Evection.Detector;

namespace Evection.Inspector;

/// <summary>Runs Cpp2IL to turn an IL2CPP game's native code + metadata back into browsable .NET assemblies.</summary>
internal static class Cpp2Il
{
    public static string EnsureDummyAssemblies(GameInfo game, Action<string>? progress)
    {
        var gameAssembly = new FileInfo(game.GameAssemblyPath!);
        // Cache per game build: the key changes whenever the game updates.
        var key = $"{gameAssembly.Length:x}-{gameAssembly.LastWriteTimeUtc.Ticks:x}";
        var output = Path.Combine(CacheRoot(), Sanitize(game.Name), key);
        var marker = Path.Combine(output, ".complete");
        if (File.Exists(marker))
            return output;

        if (Directory.Exists(output))
            Directory.Delete(output, recursive: true);
        Directory.CreateDirectory(output);

        var exe = FindExecutable();
        progress?.Invoke($"Rebuilding {game.Name}'s code with Cpp2IL (first time only, can take a minute)...");

        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("--game-path");
        psi.ArgumentList.Add(game.GameDirectory);
        psi.ArgumentList.Add("--exe-name");
        psi.ArgumentList.Add(game.ExecutableStem);
        psi.ArgumentList.Add("--output-as");
        psi.ArgumentList.Add("dummydll");
        psi.ArgumentList.Add("--output-to");
        psi.ArgumentList.Add(output);
        psi.ArgumentList.Add("--use-processor");
        psi.ArgumentList.Add("attributeinjector");

        using var process = Process.Start(psi) ?? throw new InspectorException($"Couldn't start {exe}.");
        var log = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (log) log.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (log) log.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        File.WriteAllText(Path.Combine(output, "cpp2il.log"), log.ToString());
        if (process.ExitCode != 0 || !Directory.EnumerateFiles(output, "*.dll").Any())
        {
            throw new InspectorException(
                $"Cpp2IL failed (exit code {process.ExitCode}). The game may be obfuscated or use an unsupported Unity version. " +
                $"Log: {Path.Combine(output, "cpp2il.log")}");
        }

        File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
        return output;
    }

    public static string CacheRoot()
    {
        var env = Environment.GetEnvironmentVariable("EVECTION_CACHE");
        if (!string.IsNullOrEmpty(env))
            return env;
        var baseDir = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } xdg
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
        return Path.Combine(baseDir, "evection-hook", "cpp2il");
    }

    private static string FindExecutable()
    {
        var fileName = OperatingSystem.IsWindows() ? "Cpp2IL-windows.exe" : "Cpp2IL-linux";
        var candidates = new List<string?>
        {
            Environment.GetEnvironmentVariable("EVECTION_CPP2IL"),
            Path.Combine(AppContext.BaseDirectory, "tools", fileName),
        };
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            candidates.Add(Path.Combine(dir.FullName, "deps", "cpp2il", fileName));

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new InspectorException(
                "Cpp2IL wasn't found, so IL2CPP games can't be inspected. Run tools/fetch-deps.sh, or set EVECTION_CPP2IL.");
    }

    private static string Sanitize(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '_' : c));
}
