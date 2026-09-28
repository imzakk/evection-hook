using System.Text;
using System.Text.RegularExpressions;

namespace Evection.Detector;

/// <summary>Reads the Unity engine version from the headers of the game's serialized data files.</summary>
internal static partial class UnityVersionReader
{
    // e.g. 2019.4.40f1, 5.6.7f1, 6000.0.23f1
    [GeneratedRegex(@"(?<![\d.])(\d{1,4}\.\d{1,2}\.\d{1,3}[abfpx]\d{1,3})")]
    private static partial Regex VersionPattern();

    private static readonly string[] Candidates = ["globalgamemanagers", "data.unity3d", "mainData", "level0"];

    public static string? Read(string dataDirectory)
    {
        foreach (var name in Candidates)
        {
            var path = Path.Combine(dataDirectory, name);
            if (!File.Exists(path))
                continue;

            var version = ScanHeader(path);
            if (version != null)
                return version;
        }
        return null;
    }

    private static string? ScanHeader(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[512];
            var read = stream.Read(buffer, 0, buffer.Length);
            var text = Encoding.Latin1.GetString(buffer, 0, read);
            var match = VersionPattern().Match(text);
            return match.Success ? match.Value : null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
