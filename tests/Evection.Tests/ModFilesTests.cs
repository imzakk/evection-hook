using Evection.GUI.Services;
using Evection.Installer;
using Xunit;

namespace Evection.Tests;

public class ModFilesTests
{
    [Fact]
    public void ReadsModInfoWithoutLoadingTheMod()
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "HelloMod.dll");
        Assert.True(File.Exists(dll), "HelloMod.dll should be copied next to the tests");
        var meta = ModMetadataReader.TryRead(dll);
        Assert.NotNull(meta);
        Assert.Equal("Hello Mod", meta.Name);
        Assert.Equal("1.0.0", meta.Version);
        Assert.Equal("Evection Hook", meta.Author);
        Assert.Equal("Proves Evection Hook is working.", meta.Description);
    }

    [Fact]
    public void NonModFilesHaveNoMetadata()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "not a dll");
            Assert.Null(ModMetadataReader.TryRead(file));
            Assert.Null(ModMetadataReader.TryRead(typeof(ModFilesTests).Assembly.Location));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ConfigFileRoundTripsInModConfigFormat()
    {
        var dir = Directory.CreateTempSubdirectory("evection-cfg-").FullName;
        try
        {
            var path = Path.Combine(dir, "Hello Mod.cfg");
            File.WriteAllText(path, "# Draw the label\nshowLabel = True\n\nspeed = 1.5\n\nname = Bob\n");
            var cfg = ModConfigFile.Load(path);
            Assert.Equal("Hello Mod", cfg.ModName);
            Assert.Equal(3, cfg.Entries.Count);
            Assert.True(cfg.Entries[0].IsBoolean);
            Assert.Equal("Draw the label", cfg.Entries[0].Description);
            Assert.True(cfg.Entries[1].IsNumber);
            Assert.False(cfg.Entries[2].IsBoolean);

            cfg.Entries[0].Value = "False";
            cfg.Save();
            var again = ModConfigFile.Load(path);
            Assert.Equal("False", again.Entries[0].Value);
            Assert.Equal("Draw the label", again.Entries[0].Description);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void OnlySyncedSettingsLeaveTheComputer()
    {
        var settings = new AppSettings
        {
            Theme = AppTheme.Light,
            ExtraGameFolders = ["/secret/path"],
            Account = new Account { Token = "abc" },
            PayloadOverride = "/dev/payload",
        };
        settings.ModLists["ULTRAKILL"] = ["Hello Mod"];
        var json = settings.ToSyncedJson();
        Assert.Contains("Light", json);
        Assert.Contains("Hello Mod", json);
        Assert.DoesNotContain("/secret/path", json);
        Assert.DoesNotContain("abc", json);
        Assert.DoesNotContain("/dev/payload", json);

        var other = new AppSettings { ExtraGameFolders = ["/mine"] };
        other.ApplySyncedJson(json);
        Assert.Equal(AppTheme.Light, other.Theme);
        Assert.Equal(["Hello Mod"], other.ModLists["ULTRAKILL"]);
        Assert.Equal(["/mine"], other.ExtraGameFolders);
    }

    [Fact]
    public void SettingsStoreSurvivesCorruptFile()
    {
        var dir = Directory.CreateTempSubdirectory("evection-settings-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "settings.json"), "{ not json");
            var store = new SettingsStore(dir);
            Assert.True(store.Current.ShowIntro);
            store.Update(s => s.ShowIntro = false);
            Assert.False(new SettingsStore(dir).Current.ShowIntro);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
