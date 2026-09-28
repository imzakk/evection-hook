using Evection.Detector;
using Evection.Installer;
using Xunit;

namespace Evection.Tests;

public sealed class InstallerTests : IDisposable
{
    private readonly string payloadDir = Directory.CreateTempSubdirectory("evection-payload-").FullName;
    private readonly Payload payload;

    public InstallerTests()
    {
        foreach (var arch in new[] { "x64", "x86" })
        {
            Directory.CreateDirectory(Path.Combine(payloadDir, "doorstop", arch));
            File.WriteAllText(Path.Combine(payloadDir, "doorstop", arch, "winhttp.dll"), arch);
        }
        Directory.CreateDirectory(Path.Combine(payloadDir, "core-mono"));
        File.WriteAllText(Path.Combine(payloadDir, "core-mono", "Evection.Core.Mono.dll"), "core");
        File.WriteAllText(Path.Combine(payloadDir, "core-mono", "0Harmony.dll"), "harmony");
        payload = new Payload(payloadDir);
    }

    public void Dispose() => Directory.Delete(payloadDir, recursive: true);

    [Fact]
    public void InstallsMonoGame()
    {
        using var game = new FakeGame(ScriptingBackend.Mono, CpuArchitecture.X86);
        var result = ModLoaderInstaller.Install(game.Detect(), payload);

        Assert.Equal("x86", File.ReadAllText(Path.Combine(game.Directory, "winhttp.dll")));
        Assert.True(File.Exists(Path.Combine(game.Directory, "EvectionHook", "core", "Evection.Core.Mono.dll")));
        Assert.True(Directory.Exists(result.ModsDirectory));
        var config = File.ReadAllText(Path.Combine(game.Directory, "doorstop_config.ini"));
        Assert.Contains(@"target_assembly=EvectionHook\core\Evection.Core.Mono.dll", config);
        Assert.True(game.Detect().EvectionInstalled);
    }

    [Fact]
    public void UninstallRestoresOriginalFolder()
    {
        using var game = new FakeGame(ScriptingBackend.Mono);
        var before = game.Snapshot();
        ModLoaderInstaller.Install(game.Detect(), payload);
        ModLoaderInstaller.Uninstall(game.Detect(), keepUserData: false);
        Assert.Equal(before, game.Snapshot());
    }

    [Fact]
    public void UninstallKeepsModsByDefault()
    {
        using var game = new FakeGame(ScriptingBackend.Mono);
        var result = ModLoaderInstaller.Install(game.Detect(), payload);
        File.WriteAllText(Path.Combine(result.ModsDirectory, "MyMod.dll"), "");
        ModLoaderInstaller.Uninstall(game.Detect(), keepUserData: true);

        Assert.True(File.Exists(Path.Combine(result.ModsDirectory, "MyMod.dll")));
        Assert.False(File.Exists(Path.Combine(game.Directory, "winhttp.dll")));
        Assert.False(game.Detect().EvectionInstalled);
    }

    [Fact]
    public void ReinstallKeepsMods()
    {
        using var game = new FakeGame(ScriptingBackend.Mono);
        var result = ModLoaderInstaller.Install(game.Detect(), payload);
        File.WriteAllText(Path.Combine(result.ModsDirectory, "MyMod.dll"), "");
        ModLoaderInstaller.Install(game.Detect(), payload);
        Assert.True(File.Exists(Path.Combine(result.ModsDirectory, "MyMod.dll")));
    }

    [Fact]
    public void RefusesAntiCheatGames()
    {
        using var game = new FakeGame(ScriptingBackend.Mono);
        game.Add("EasyAntiCheat", directory: true);
        var before = game.Snapshot();
        var e = Assert.Throws<InstallException>(() => ModLoaderInstaller.Install(game.Detect(), payload));
        Assert.Contains("anti-cheat", e.Message);
        Assert.Equal(before, game.Snapshot());
    }

    [Fact]
    public void RefusesWhenAnotherLoaderIsInstalled()
    {
        using var game = new FakeGame(ScriptingBackend.Mono);
        game.Add("BepInEx", directory: true);
        Assert.Throws<InstallException>(() => ModLoaderInstaller.Install(game.Detect(), payload));
    }

    [Fact]
    public void RefusesIl2CppWhenPayloadLacksRuntime()
    {
        using var game = new FakeGame(ScriptingBackend.Il2Cpp);
        var e = Assert.Throws<InstallException>(() => ModLoaderInstaller.Install(game.Detect(), payload));
        Assert.Contains("il2cpp", e.Message);
    }

    [Fact]
    public void ModsCanBeDisabledAndEnabled()
    {
        using var game = new FakeGame(ScriptingBackend.Mono);
        ModLoaderInstaller.Install(game.Detect(), payload);
        var mods = new ModsFolder(game.Detect());
        File.WriteAllText(Path.Combine(mods.Directory, "Cool.dll"), "");

        mods.SetEnabled("cool", false);
        Assert.True(File.Exists(Path.Combine(mods.Directory, "Cool.dll.disabled")));
        Assert.False(Assert.Single(mods.List()).Enabled);

        mods.SetEnabled("Cool", true);
        Assert.True(Assert.Single(mods.List()).Enabled);
    }
}
