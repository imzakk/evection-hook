namespace Evection.Detector;

public enum ScriptingBackend
{
    Unknown,
    Mono,
    Il2Cpp,
}

public enum CpuArchitecture
{
    Unknown,
    X86,
    X64,
    Arm64,
}

public enum GamePlatform
{
    Windows,
    Linux,
}

/// <summary>A reason the game folder contains something we care about (anti-cheat, another mod loader...).</summary>
public sealed record Finding(string Name, string Evidence);

/// <summary>Everything the detector learned about a Unity game install.</summary>
public sealed class GameInfo
{
    public required string Name { get; init; }
    public string? Company { get; init; }
    public required string GameDirectory { get; init; }
    public required string ExecutablePath { get; init; }
    public required string DataDirectory { get; init; }
    public required GamePlatform Platform { get; init; }
    public required ScriptingBackend Backend { get; init; }
    public required CpuArchitecture Architecture { get; init; }
    public string? UnityVersion { get; init; }

    /// <summary>Mono: the folder holding Assembly-CSharp.dll and friends.</summary>
    public string? ManagedDirectory { get; init; }

    /// <summary>IL2CPP: the native GameAssembly.dll.</summary>
    public string? GameAssemblyPath { get; init; }

    /// <summary>IL2CPP: global-metadata.dat.</summary>
    public string? MetadataPath { get; init; }

    /// <summary>Steam app id, if the game was installed through Steam.</summary>
    public int? SteamAppId { get; init; }

    public IReadOnlyList<Finding> AntiCheats { get; init; } = Array.Empty<Finding>();
    public IReadOnlyList<Finding> OtherModLoaders { get; init; } = Array.Empty<Finding>();
    public bool EvectionInstalled { get; init; }

    public string ExecutableStem => Path.GetFileNameWithoutExtension(ExecutablePath);
    public string EvectionDirectory => Path.Combine(GameDirectory, "EvectionHook");
    public string LogFile => Path.Combine(EvectionDirectory, "Logs", "latest.log");
    public bool HasAntiCheat => AntiCheats.Count > 0;
}

public sealed class GameDetectionException(string message) : Exception(message);
