using Evection.Core;
using Evection.Detector;
using Evection.Installer;
using Xunit;

namespace Evection.Tests;

public class LaunchTests
{
    [Fact]
    public void FindsSteamAppIdAndName()
    {
        var root = Directory.CreateTempSubdirectory("evection-steam-").FullName;
        try
        {
            var common = Path.Combine(root, "steamapps", "common");
            using var game = new FakeGame(ScriptingBackend.Mono);
            var dir = Path.Combine(common, "My Game");
            Directory.CreateDirectory(common);
            Directory.Move(game.Directory, dir);
            File.WriteAllText(Path.Combine(root, "steamapps", "appmanifest_4242.acf"),
                "\"AppState\"\n{\n\t\"appid\"\t\t\"4242\"\n\t\"name\"\t\t\"My Game: Deluxe\"\n\t\"installdir\"\t\t\"My Game\"\n}\n");

            var info = GameDetector.Detect(dir);
            Assert.Equal(4242, info.SteamAppId);
            Assert.Equal("My Game: Deluxe", info.Name);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void NonSteamGameHasNoAppId()
    {
        using var game = new FakeGame(ScriptingBackend.Mono);
        Assert.Null(game.Detect().SteamAppId);
    }

    [Fact]
    public void ModsOnlyFromEvectionFlagControlsLoading()
    {
        using var game = new FakeGame(ScriptingBackend.Mono);
        var info = game.Detect();
        var core = Path.Combine(info.EvectionDirectory, "core");
        bool Enabled(params string[] args) => new LoaderPaths(core, args).ModsEnabledForThisLaunch();

        // Default: mods always load.
        Assert.True(Enabled("game.exe"));

        ModLoaderInstaller.SetModsOnlyFromEvection(info, true);
        Assert.True(ModLoaderInstaller.GetModsOnlyFromEvection(info));
        Assert.False(Enabled("game.exe"));
        Assert.True(Enabled(["game.exe", .. GameLauncher.GameArguments(null)]));

        ModLoaderInstaller.SetModsOnlyFromEvection(info, false);
        Assert.True(Enabled("game.exe"));
    }
}
