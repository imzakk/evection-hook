using Evection.Detector;

namespace Evection.Inspector;

/// <summary>
/// Locates the .NET assemblies that describe a game's code.
/// Mono games ship them directly; IL2CPP games need Cpp2IL to rebuild "dummy" assemblies
/// (all classes, fields and method signatures, but no method bodies).
/// </summary>
public sealed class GameCode
{
    /// <summary>Assemblies written by the game developer (Assembly-CSharp etc.), in load-priority order.</summary>
    public required IReadOnlyList<string> GameAssemblies { get; init; }

    /// <summary>Every assembly available, including Unity and third-party libraries.</summary>
    public required IReadOnlyList<string> AllAssemblies { get; init; }

    /// <summary>Folder that references are resolved from.</summary>
    public required string AssemblyDirectory { get; init; }

    /// <summary>True for IL2CPP games — method bodies aren't available, only signatures.</summary>
    public required bool SignaturesOnly { get; init; }

    public static GameCode Load(GameInfo game, Action<string>? progress = null)
    {
        var directory = game.Backend switch
        {
            ScriptingBackend.Mono => game.ManagedDirectory!,
            ScriptingBackend.Il2Cpp => Cpp2Il.EnsureDummyAssemblies(game, progress),
            _ => throw new InspectorException("Couldn't tell whether this game uses Mono or IL2CPP."),
        };

        var all = Directory.EnumerateFiles(directory, "*.dll").OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        var gameAssemblies = all.Where(IsGameAssembly)
            .OrderBy(p => Path.GetFileNameWithoutExtension(p) switch
            {
                "Assembly-CSharp" => 0,
                "Assembly-CSharp-firstpass" => 1,
                _ => 2,
            })
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new GameCode
        {
            GameAssemblies = gameAssemblies,
            AllAssemblies = all,
            AssemblyDirectory = directory,
            SignaturesOnly = game.Backend == ScriptingBackend.Il2Cpp,
        };
    }

    /// <summary>Where rebuilt IL2CPP code is cached (safe to delete; it's regenerated on demand).</summary>
    public static string CacheDirectory => Cpp2Il.CacheRoot();

    public string Resolve(string assemblyName)
    {
        var name = assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? assemblyName : assemblyName + ".dll";
        return AllAssemblies.FirstOrDefault(p => string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InspectorException($"No assembly named '{assemblyName}' in {AssemblyDirectory}.");
    }

    private static readonly string[] EnginePrefixes =
    [
        "Unity.", "UnityEngine", "UnityEditor", "System", "Mono.", "mscorlib", "netstandard", "Microsoft.",
        "Newtonsoft.", "Il2Cpp", "__Generated", "DOTween", "DemiLib", "Rewired", "Photon", "Steamworks",
        "Facepunch", "Sirenix", "FMOD", "Cinemachine", "TextMeshPro", "Assembly-UnityScript",
    ];

    /// <summary>Heuristic: assemblies that are the game's own code rather than engine or library code.</summary>
    public static bool IsGameAssembly(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (name.StartsWith("Assembly-CSharp", StringComparison.OrdinalIgnoreCase))
            return true;
        return !EnginePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class InspectorException(string message) : Exception(message);
