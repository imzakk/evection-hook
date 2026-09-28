using Evection.Core;
using Evection.Detector;
using Evection.Installer;
using Xunit;

namespace Evection.Tests;

public sealed class ProfileTests : IDisposable
{
    private readonly FakeGame game = new(ScriptingBackend.Mono);
    private GameInfo Info => game.Detect();

    public void Dispose() => game.Dispose();

    private void AddMod(ModProfile profile, string name)
    {
        Directory.CreateDirectory(profile.ModsDirectory);
        File.WriteAllText(Path.Combine(profile.ModsDirectory, name + ".dll"), "");
    }

    [Fact]
    public void StartsWithDefaultProfile()
    {
        var profiles = ModProfiles.List(Info);
        Assert.Equal(ModProfiles.DefaultId, Assert.Single(profiles).Id);
        Assert.Equal(ModProfiles.DefaultId, ModProfiles.GetActive(Info).Id);
    }

    [Fact]
    public void MovesOldModsIntoDefault()
    {
        var mods = Path.Combine(Info.EvectionDirectory, "Mods");
        Directory.CreateDirectory(mods);
        File.WriteAllText(Path.Combine(mods, "Old.dll"), "");
        Directory.CreateDirectory(Path.Combine(Info.EvectionDirectory, "Config"));
        File.WriteAllText(Path.Combine(Info.EvectionDirectory, "Config", "Old.cfg"), "a = 1");

        var profile = ModProfiles.GetActive(Info);
        Assert.Equal("Old", Assert.Single(new ModsFolder(profile).List()).Name);
        Assert.True(File.Exists(Path.Combine(profile.ConfigDirectory, "Old.cfg")));
        Assert.False(Directory.Exists(mods));
    }

    [Fact]
    public void CreatesCopiesRenamesAndDeletes()
    {
        var def = ModProfiles.GetActive(Info);
        AddMod(def, "A");

        var empty = ModProfiles.Create(Info, "Chaos Run");
        Assert.Equal("chaos-run", empty.Id);
        Assert.Empty(new ModsFolder(empty).List());

        var copy = ModProfiles.Create(Info, "With Friends", copyFromId: def.Id);
        Assert.Equal("A", Assert.Single(new ModsFolder(copy).List()).Name);

        Assert.Throws<InstallException>(() => ModProfiles.Create(Info, "with friends"));

        ModProfiles.Rename(Info, copy.Id, "Co-op");
        Assert.Equal("Co-op", ModProfiles.Get(Info, copy.Id).Name);
        Assert.Equal(copy.Id, ModProfiles.Find(Info, "co-op").Id);

        ModProfiles.SetActive(Info, copy.Id);
        ModProfiles.Delete(Info, copy.Id);
        Assert.NotEqual(copy.Id, ModProfiles.GetActive(Info).Id);
        Assert.Equal(2, ModProfiles.List(Info).Count);
    }

    [Fact]
    public void CannotDeleteLastProfile() =>
        Assert.Throws<InstallException>(() => ModProfiles.Delete(Info, ModProfiles.DefaultId));

    [Fact]
    public void LoaderUsesActiveOrLaunchedProfile()
    {
        var def = ModProfiles.GetActive(Info);
        var other = ModProfiles.Create(Info, "Other");
        var core = Path.Combine(Info.EvectionDirectory, "core");

        Assert.Equal(def.ModsDirectory, new LoaderPaths(core).ModsDirectory);

        ModProfiles.SetActive(Info, other.Id);
        Assert.Equal(other.ModsDirectory, new LoaderPaths(core).ModsDirectory);
        Assert.Equal(other.ConfigDirectory, new LoaderPaths(core).ConfigDirectory);

        // A launch from the app names its profile explicitly.
        var launched = new LoaderPaths(core, ["game.exe", .. GameLauncher.GameArguments(def.Id)]);
        Assert.Equal(def.Id, launched.Profile);
        Assert.Equal(def.ModsDirectory, launched.ModsDirectory);
    }

    [Fact]
    public void LoaderConfigKeepsBothSettings()
    {
        var other = ModProfiles.Create(Info, "Other");
        ModProfiles.SetActive(Info, other.Id);
        ModLoaderInstaller.SetModsOnlyFromEvection(Info, true);
        Assert.Equal(other.Id, ModProfiles.GetActive(Info).Id);
        Assert.True(ModLoaderInstaller.GetModsOnlyFromEvection(Info));
    }
}
