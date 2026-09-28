using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Evection.Core
{
    /// <summary>A mod that was found and instantiated.</summary>
    public sealed class LoadedMod
    {
        internal LoadedMod(EvectionMod instance, string file)
        {
            Instance = instance;
            File = file;
        }

        public EvectionMod Instance { get; }
        public string File { get; }
        public ModInfoAttribute Info => Instance.Info;

        /// <summary>Set after too many errors in per-frame callbacks; the mod stops receiving them.</summary>
        public bool Faulted { get; internal set; }
        internal int FrameErrors;
    }

    /// <summary>Finds mods in the Mods folder, loads them, and forwards game events to them — isolating their errors.</summary>
    public static class ModManager
    {
        /// <summary>After this many exceptions in OnUpdate/OnLateUpdate/OnGUI a mod is switched off, to keep the game playable.</summary>
        public const int MaxFrameErrors = 20;

        private static readonly List<LoadedMod> mods = new List<LoadedMod>();

        public static IReadOnlyList<LoadedMod> Mods => mods;

        public static void LoadAll(LoaderPaths paths, string gameExecutableStem)
        {
            Directory.CreateDirectory(paths.ModsDirectory);
            var files = Directory.GetFiles(paths.ModsDirectory, "*.dll").OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

            var found = new List<LoadedMod>();
            foreach (var file in files)
                found.AddRange(LoadFile(file, paths, gameExecutableStem));

            foreach (var mod in found.OrderBy(m => m.Info.Priority).ThenBy(m => m.Info.Name, StringComparer.OrdinalIgnoreCase))
            {
                mods.Add(mod);
                Log.Info($"Loading {mod.Info.Name} v{mod.Info.Version} by {mod.Info.Author}");
                if (!Invoke(mod, m => m.OnLoad(), nameof(EvectionMod.OnLoad)))
                    mod.Faulted = true;
            }
            Log.Info($"{mods.Count(m => !m.Faulted)} mod(s) loaded" + (mods.Any(m => m.Faulted) ? $", {mods.Count(m => m.Faulted)} failed" : "") + ".");
        }

        private static IEnumerable<LoadedMod> LoadFile(string file, LoaderPaths paths, string gameExecutableStem)
        {
            Assembly assembly;
            Type[] types;
            try
            {
                assembly = Assembly.LoadFrom(file);
                types = GetLoadableTypes(assembly, file);
            }
            catch (Exception e)
            {
                Log.Error($"Couldn't load {Path.GetFileName(file)}: {e.Message}");
                yield break;
            }

            var modTypes = types.Where(t => t != null && !t.IsAbstract && typeof(EvectionMod).IsAssignableFrom(t)).ToList();
            if (modTypes.Count == 0)
            {
                Log.Warning($"{Path.GetFileName(file)} has no EvectionMod class — if it's a library, move it to Mods/libs.");
                yield break;
            }

            foreach (var type in modTypes)
            {
                var info = type.GetCustomAttributes(typeof(ModInfoAttribute), false).OfType<ModInfoAttribute>().FirstOrDefault();
                if (info == null)
                {
                    Log.Error($"{type.FullName} in {Path.GetFileName(file)} is missing [ModInfo(...)] — skipped.");
                    continue;
                }
                if (info.Games is { Length: > 0 } games && !games.Any(g => string.Equals(g, gameExecutableStem, StringComparison.OrdinalIgnoreCase)))
                {
                    Log.Info($"Skipping {info.Name}: made for {string.Join(", ", games)}.");
                    continue;
                }

                EvectionMod instance;
                try
                {
                    instance = (EvectionMod)Activator.CreateInstance(type)!;
                }
                catch (Exception e)
                {
                    Log.Error($"Couldn't create {info.Name}: {(e.InnerException ?? e)}");
                    continue;
                }

                instance.Info = info;
                instance.Log = new ModLogger(info.Name);
                instance.Harmony = new Harmony("evection." + info.Name);
                instance.Config = new ModConfig(Path.Combine(paths.ConfigDirectory, SafeFileName(info.Name) + ".cfg"));
                instance.FilePath = file;
                yield return new LoadedMod(instance, file);
            }
        }

        private static Type[] GetLoadableTypes(Assembly assembly, string file)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                foreach (var loaderException in e.LoaderExceptions.Where(x => x != null).Take(5))
                    Log.Warning($"{Path.GetFileName(file)}: {loaderException!.Message}");
                return e.Types.Where(t => t != null).ToArray()!;
            }
        }

        public static void Update() => Frame(m => m.OnUpdate(), nameof(EvectionMod.OnUpdate));
        public static void LateUpdate() => Frame(m => m.OnLateUpdate(), nameof(EvectionMod.OnLateUpdate));
        public static void GUI() => Frame(m => m.OnGUI(), nameof(EvectionMod.OnGUI));

        public static void SceneLoaded(string name, int buildIndex)
        {
            foreach (var mod in mods)
            {
                if (!mod.Faulted)
                    Invoke(mod, m => m.OnSceneLoaded(name, buildIndex), nameof(EvectionMod.OnSceneLoaded));
            }
        }

        public static void Quit()
        {
            foreach (var mod in mods)
                Invoke(mod, m => m.OnQuit(), nameof(EvectionMod.OnQuit));
        }

        private static void Frame(Action<EvectionMod> callback, string name)
        {
            for (var i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                if (mod.Faulted || Invoke(mod, callback, name))
                    continue;
                if (++mod.FrameErrors >= MaxFrameErrors)
                {
                    mod.Faulted = true;
                    Log.Error($"{mod.Info.Name} threw {MaxFrameErrors} errors and has been disabled for this session.");
                }
            }
        }

        private static bool Invoke(LoadedMod mod, Action<EvectionMod> callback, string name)
        {
            try
            {
                callback(mod.Instance);
                return true;
            }
            catch (Exception e)
            {
                Log.Write(LogLevel.Error, mod.Info.Name, $"Error in {name}: {e}");
                return false;
            }
        }

        private static string SafeFileName(string name) =>
            new string(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
    }
}
