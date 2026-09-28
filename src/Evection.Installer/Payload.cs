using Evection.Detector;

namespace Evection.Installer;

/// <summary>
/// The folder of files that get copied into a game. Produced by tools/build-payload.sh:
/// <code>
/// payload/
///   doorstop/x64/winhttp.dll   doorstop/x86/winhttp.dll
///   core-mono/                 Evection.Core.Mono.dll + dependencies
///   core-il2cpp/               (IL2CPP runtime — not built yet)
///   dotnet/                    (CoreCLR for IL2CPP games — not built yet)
/// </code>
/// </summary>
public sealed class Payload(string root)
{
    public string Root { get; } = Path.GetFullPath(root);

    public string DoorstopDirectory(CpuArchitecture arch) => Path.Combine(Root, "doorstop", arch == CpuArchitecture.X86 ? "x86" : "x64");
    public string CoreMonoDirectory => Path.Combine(Root, "core-mono");
    public string CoreIl2CppDirectory => Path.Combine(Root, "core-il2cpp");
    public string DotnetDirectory => Path.Combine(Root, "dotnet");

    public bool Supports(ScriptingBackend backend) => backend switch
    {
        ScriptingBackend.Mono => Directory.Exists(CoreMonoDirectory),
        ScriptingBackend.Il2Cpp => Directory.Exists(CoreIl2CppDirectory) && Directory.Exists(DotnetDirectory),
        _ => false,
    };

    /// <summary>
    /// Finds the payload: an explicit path, $EVECTION_PAYLOAD, a "payload" folder next to the app,
    /// or artifacts/payload in a parent folder (when running from a source checkout).
    /// </summary>
    public static Payload Locate(string? explicitPath = null)
    {
        foreach (var candidate in Candidates(explicitPath))
        {
            if (candidate != null && Directory.Exists(Path.Combine(candidate, "doorstop")))
                return new Payload(candidate);
        }
        throw new InstallException(
            "Couldn't find the Evection Hook payload. If you're running from source, run tools/build-payload.sh first.");
    }

    private static IEnumerable<string?> Candidates(string? explicitPath)
    {
        yield return explicitPath;
        yield return Environment.GetEnvironmentVariable("EVECTION_PAYLOAD");
        yield return Path.Combine(AppContext.BaseDirectory, "payload");
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            yield return Path.Combine(dir.FullName, "artifacts", "payload");
    }
}
