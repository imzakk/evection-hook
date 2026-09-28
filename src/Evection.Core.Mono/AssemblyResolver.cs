using System;
using System.IO;
using System.Reflection;

namespace Evection.Core.Mono
{
    /// <summary>Lets the game find our dependencies (HarmonyX, MonoMod...) and mod libraries, which aren't in the game's Managed folder.</summary>
    internal static class AssemblyResolver
    {
        private static readonly System.Collections.Generic.List<string> searchDirectories = new System.Collections.Generic.List<string>();

        public static void Install(string coreDirectory)
        {
            searchDirectories.Add(coreDirectory);
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }

        /// <summary>Called once the mod profile is known, so mods can reference their libraries.</summary>
        public static void AddModDirectories(string modsDirectory, string librariesDirectory)
        {
            searchDirectories.Add(librariesDirectory);
            searchDirectories.Add(modsDirectory);
        }

        private static Assembly? Resolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name + ".dll";
            foreach (var dir in searchDirectories)
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path))
                    return Assembly.LoadFrom(path);
            }
            return null;
        }
    }
}
