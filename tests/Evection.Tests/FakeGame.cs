using Evection.Detector;

namespace Evection.Tests;

/// <summary>Builds a throwaway folder that looks like an installed Unity game.</summary>
internal sealed class FakeGame : IDisposable
{
    public string Directory { get; } = Path.Combine(Path.GetTempPath(), "evection-test-" + Guid.NewGuid().ToString("N"));
    public string Name { get; }

    public FakeGame(ScriptingBackend backend, CpuArchitecture arch = CpuArchitecture.X64, string name = "TestGame", string unityVersion = "2021.3.5f1")
    {
        Name = name;
        var data = Path.Combine(Directory, name + "_Data");
        System.IO.Directory.CreateDirectory(data);
        File.WriteAllBytes(Path.Combine(Directory, name + ".exe"), PeHeader(arch));
        File.WriteAllLines(Path.Combine(data, "app.info"), ["Test Company", "Test Game"]);

        var ggm = new byte[128];
        var version = System.Text.Encoding.ASCII.GetBytes(unityVersion);
        Array.Copy(version, 0, ggm, 20, version.Length);
        File.WriteAllBytes(Path.Combine(data, "globalgamemanagers"), ggm);

        if (backend == ScriptingBackend.Mono)
        {
            System.IO.Directory.CreateDirectory(Path.Combine(data, "Managed"));
            File.WriteAllText(Path.Combine(data, "Managed", "Assembly-CSharp.dll"), "");
        }
        else if (backend == ScriptingBackend.Il2Cpp)
        {
            File.WriteAllText(Path.Combine(Directory, "GameAssembly.dll"), "");
            System.IO.Directory.CreateDirectory(Path.Combine(data, "il2cpp_data", "Metadata"));
            File.WriteAllText(Path.Combine(data, "il2cpp_data", "Metadata", "global-metadata.dat"), "");
        }
    }

    public string Add(string relativePath, bool directory = false)
    {
        var path = Path.Combine(Directory, relativePath);
        if (directory)
            System.IO.Directory.CreateDirectory(path);
        else
            File.WriteAllText(path, "");
        return path;
    }

    public GameInfo Detect() => GameDetector.Detect(Directory);

    public IReadOnlyList<string> Snapshot() =>
        System.IO.Directory.EnumerateFileSystemEntries(Directory, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(Directory, p))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static byte[] PeHeader(CpuArchitecture arch)
    {
        var bytes = new byte[0x100];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        BitConverter.GetBytes(0x80).CopyTo(bytes, 0x3C);
        "PE\0\0"u8.ToArray().CopyTo(bytes, 0x80);
        ushort machine = arch == CpuArchitecture.X86 ? (ushort)0x14C : (ushort)0x8664;
        BitConverter.GetBytes(machine).CopyTo(bytes, 0x84);
        return bytes;
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Directory))
            System.IO.Directory.Delete(Directory, recursive: true);
    }
}
