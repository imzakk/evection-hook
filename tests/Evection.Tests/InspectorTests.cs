using Evection.Inspector;
using Xunit;

namespace Evection.Tests;

/// <summary>Uses the Evection.Detector assembly itself as a stand-in for a game's Assembly-CSharp.</summary>
public class InspectorTests
{
    private static CodeBrowser Browser()
    {
        var assembly = typeof(Evection.Detector.GameDetector).Assembly.Location;
        var code = new GameCode
        {
            GameAssemblies = [assembly],
            AllAssemblies = [assembly],
            AssemblyDirectory = Path.GetDirectoryName(assembly)!,
            SignaturesOnly = false,
        };
        return new CodeBrowser(code);
    }

    [Fact]
    public void ListsTypes() =>
        Assert.Contains(Browser().FindTypes("GameInfo"), t => t.Type.FullName == "Evection.Detector.GameInfo");

    [Fact]
    public void SearchFindsMembers()
    {
        var hits = Browser().Search("AntiCheat").ToList();
        Assert.Contains(hits, h => h.Kind == MemberKind.Property && h.MemberName == "HasAntiCheat");
        Assert.Contains(hits, h => h.Kind == MemberKind.Method && h.MemberName == "FindAntiCheat");
    }

    [Fact]
    public void DescribeShowsFieldsAndMethods()
    {
        var text = Browser().Describe("GameDetector");
        Assert.Contains("static class Evection.Detector.GameDetector", text);
        Assert.Contains("public static GameInfo Detect(string path)", text);
    }

    [Fact]
    public void SourceDecompilesType()
    {
        var source = Browser().Source("PeReader");
        Assert.Contains("class PeReader", source);
        Assert.Contains("ReadArchitecture", source);
    }

    [Fact]
    public void AmbiguousNamesAreReported() =>
        Assert.Throws<InspectorException>(() => Browser().GetType("Game"));

    [Theory]
    [InlineData("Assembly-CSharp.dll", true)]
    [InlineData("Sons.dll", true)]
    [InlineData("UnityEngine.CoreModule.dll", false)]
    [InlineData("System.Core.dll", false)]
    [InlineData("Newtonsoft.Json.dll", false)]
    public void ClassifiesGameAssemblies(string file, bool expected) =>
        Assert.Equal(expected, GameCode.IsGameAssembly(file));
}
