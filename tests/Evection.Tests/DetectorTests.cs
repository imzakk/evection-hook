using Evection.Detector;
using Xunit;

namespace Evection.Tests;

public class DetectorTests
{
    [Fact]
    public void DetectsMonoGame()
    {
        using var game = new FakeGame(ScriptingBackend.Mono);
        var info = game.Detect();
        Assert.Equal(ScriptingBackend.Mono, info.Backend);
        Assert.Equal(CpuArchitecture.X64, info.Architecture);
        Assert.Equal("2021.3.5f1", info.UnityVersion);
        Assert.Equal("Test Game", info.Name);
        Assert.Equal("Test Company", info.Company);
        Assert.NotNull(info.ManagedDirectory);
        Assert.False(info.HasAntiCheat);
    }

    [Fact]
    public void DetectsIl2CppX86Game()
    {
        using var game = new FakeGame(ScriptingBackend.Il2Cpp, CpuArchitecture.X86, unityVersion: "6000.0.23f1");
        var info = game.Detect();
        Assert.Equal(ScriptingBackend.Il2Cpp, info.Backend);
        Assert.Equal(CpuArchitecture.X86, info.Architecture);
        Assert.Equal("6000.0.23f1", info.UnityVersion);
        Assert.NotNull(info.GameAssemblyPath);
    }

    [Fact]
    public void AcceptsExecutablePath()
    {
        using var game = new FakeGame(ScriptingBackend.Mono);
        var info = GameDetector.Detect(Path.Combine(game.Directory, "TestGame.exe"));
        Assert.Equal(game.Directory, info.GameDirectory);
    }

    [Theory]
    [InlineData("EasyAntiCheat", true, "Easy Anti-Cheat")]
    [InlineData("BattlEye", true, "BattlEye")]
    [InlineData("TestGame_BE.exe", false, "BattlEye")]
    public void DetectsAntiCheat(string evidence, bool isDirectory, string expected)
    {
        using var game = new FakeGame(ScriptingBackend.Mono);
        game.Add(evidence, isDirectory);
        var info = game.Detect();
        Assert.Contains(info.AntiCheats, a => a.Name == expected);
    }

    [Theory]
    [InlineData("BepInEx", true, "BepInEx")]
    [InlineData("MelonLoader", true, "MelonLoader")]
    [InlineData("winhttp.dll", false, "Unknown loader")]
    public void DetectsOtherModLoaders(string evidence, bool isDirectory, string expected)
    {
        using var game = new FakeGame(ScriptingBackend.Mono);
        game.Add(evidence, isDirectory);
        Assert.Contains(game.Detect().OtherModLoaders, l => l.Name == expected);
    }

    [Fact]
    public void RejectsNonUnityFolder()
    {
        var dir = Directory.CreateTempSubdirectory("evection-test-");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "game.exe"), "");
            Assert.Throws<GameDetectionException>(() => GameDetector.Detect(dir.FullName));
            Assert.Null(GameDetector.TryDetect(dir.FullName));
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public void IgnoresCrashHandler()
    {
        using var game = new FakeGame(ScriptingBackend.Mono);
        game.Add("UnityCrashHandler64.exe");
        Assert.Equal("TestGame.exe", Path.GetFileName(game.Detect().ExecutablePath));
    }
}
