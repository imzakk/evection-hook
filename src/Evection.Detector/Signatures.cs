namespace Evection.Detector;

/// <summary>
/// Files and folders that reveal anti-cheat or other mod loaders. Evection Hook refuses to install
/// into games with anti-cheat — see docs/anti-cheat-policy.md.
/// </summary>
internal static class Signatures
{
    public static IReadOnlyList<Finding> FindAntiCheat(string gameDirectory)
    {
        var findings = new List<Finding>();
        void Check(string name, string relativePath)
        {
            var full = Path.Combine(gameDirectory, relativePath);
            if (Directory.Exists(full) || File.Exists(full))
                findings.Add(new Finding(name, relativePath));
        }

        Check("Easy Anti-Cheat", "EasyAntiCheat");
        Check("Easy Anti-Cheat", "EasyAntiCheat_EOS");
        Check("Easy Anti-Cheat", "start_protected_game.exe");
        Check("BattlEye", "BattlEye");
        Check("nProtect GameGuard", "GameGuard");
        Check("XIGNCODE3", "XIGNCODE");
        Check("XIGNCODE3", "x3.xem");
        Check("miHoYo Protect", "mhypbase.dll");
        Check("Anti-Cheat Expert", "AntiCheatExpert");

        foreach (var file in SafeEnumerateFiles(gameDirectory, "*_BE.exe"))
            findings.Add(new Finding("BattlEye", Path.GetFileName(file)));

        return findings.DistinctBy(f => f.Evidence).ToList();
    }

    public static IReadOnlyList<Finding> FindOtherModLoaders(string gameDirectory, bool evectionInstalled)
    {
        var findings = new List<Finding>();
        if (Directory.Exists(Path.Combine(gameDirectory, "BepInEx")))
            findings.Add(new Finding("BepInEx", "BepInEx/"));
        if (Directory.Exists(Path.Combine(gameDirectory, "MelonLoader")))
            findings.Add(new Finding("MelonLoader", "MelonLoader/"));

        // Proxy DLLs that aren't ours mean some other loader (or an unknown tool) already hooks the game.
        if (!evectionInstalled)
        {
            foreach (var proxy in new[] { "winhttp.dll", "version.dll", "winmm.dll", "doorstop_config.ini" })
            {
                if (File.Exists(Path.Combine(gameDirectory, proxy)) && findings.Count == 0)
                    findings.Add(new Finding("Unknown loader", proxy));
            }
        }
        return findings;
    }

    private static IEnumerable<string> SafeEnumerateFiles(string dir, string pattern)
    {
        try { return Directory.EnumerateFiles(dir, pattern); }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }
}
