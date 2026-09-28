using System;
using System.Collections.Generic;
using System.IO;

namespace Evection.Core
{
    /// <summary>
    /// Folder layout inside &lt;Game&gt;/EvectionHook, including which mod profile this launch uses:
    /// "--evection-profile &lt;id&gt;" on the command line, else "profile" in loader.cfg, else "default".
    /// </summary>
    public sealed class LoaderPaths
    {
        private readonly string[] commandLine;
        private readonly Dictionary<string, string> config;

        public LoaderPaths(string coreDirectory, string[]? commandLine = null)
        {
            CoreDirectory = coreDirectory;
            Root = Path.GetDirectoryName(coreDirectory)!;
            GameDirectory = Path.GetDirectoryName(Root)!;
            this.commandLine = commandLine ?? Array.Empty<string>();
            config = ReadConfig(LoaderConfig);
            Profile = ArgumentValue("--evection-profile") ?? Get("profile") ?? "default";
        }

        public string GameDirectory { get; }
        public string Root { get; }
        public string CoreDirectory { get; }
        public string LoaderConfig => Path.Combine(Root, "loader.cfg");
        public string LogsDirectory => Path.Combine(Root, "Logs");

        /// <summary>The mod profile for this launch.</summary>
        public string Profile { get; }

        private string ProfileDirectory => Path.Combine(Path.Combine(Root, "Profiles"), Profile);

        /// <summary>Installs from before profiles existed keep their mods in EvectionHook/Mods.</summary>
        private bool UsesProfiles => Directory.Exists(Path.Combine(Root, "Profiles"));

        public string ModsDirectory => UsesProfiles ? Path.Combine(ProfileDirectory, "Mods") : Path.Combine(Root, "Mods");
        public string ModLibrariesDirectory => Path.Combine(ModsDirectory, "libs");
        public string ConfigDirectory => UsesProfiles ? Path.Combine(ProfileDirectory, "Config") : Path.Combine(Root, "Config");

        /// <summary>
        /// False when mods are set to load only for launches from evection hook and this launch wasn't one
        /// (evection hook starts games with --evection-mods).
        /// </summary>
        public bool ModsEnabledForThisLaunch() =>
            Get("mods_only_from_evection") != "true" || Array.IndexOf(commandLine, "--evection-mods") >= 0;

        private string? Get(string key) => config.TryGetValue(key, out var value) && value.Length > 0 ? value : null;

        private string? ArgumentValue(string name)
        {
            var i = Array.IndexOf(commandLine, name);
            return i >= 0 && i + 1 < commandLine.Length ? commandLine[i + 1] : null;
        }

        private static Dictionary<string, string> ReadConfig(string path)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
                return values;
            foreach (var line in File.ReadAllLines(path))
            {
                var eq = line.IndexOf('=');
                if (eq > 0 && !line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                    values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            return values;
        }
    }
}
