using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Evection.Core.Mono
{
    /// <summary>
    /// Sets up logging, then waits for Unity to load its first scene (the engine isn't ready before that)
    /// to create the host GameObject and load mods.
    /// </summary>
    internal static class Bootstrap
    {
        private static LoaderPaths paths = null!;
        private static bool started;

        public static void Run(string coreDirectory)
        {
            paths = new LoaderPaths(coreDirectory, Environment.GetCommandLineArgs());
            Log.Initialize(paths.LogsDirectory);
            Log.Info($"Evection Hook {typeof(Bootstrap).Assembly.GetName().Version} (Mono runtime)");
            Log.Info($"Game: {Process.GetCurrentProcess().MainModule?.FileName}");

            if (!paths.ModsEnabledForThisLaunch())
            {
                Log.Info("Started outside evection hook and mods are set to load only from there, so mods are off this time.");
                return;
            }
            Log.Info($"Profile: {paths.Profile}");
            AssemblyResolver.AddModDirectories(paths.ModsDirectory, paths.ModLibrariesDirectory);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!started)
            {
                started = true;
                try
                {
                    Start();
                }
                catch (Exception e)
                {
                    Log.Error("Startup failed: " + e);
                }
            }
            ModManager.SceneLoaded(scene.name, scene.buildIndex);
        }

        private static void Start()
        {
            Log.Info($"Unity {Application.unityVersion}, product '{Application.productName}'");

            var host = new GameObject("EvectionHook");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<EvectionHost>();

            var exeStem = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule?.FileName ?? "");
            ModManager.LoadAll(paths, exeStem);
        }
    }

    /// <summary>Invisible MonoBehaviour that forwards Unity's per-frame callbacks to mods.</summary>
    internal sealed class EvectionHost : MonoBehaviour
    {
        private void Update() => ModManager.Update();
        private void LateUpdate() => ModManager.LateUpdate();
        private void OnGUI() => ModManager.GUI();
        private void OnApplicationQuit() => ModManager.Quit();
    }
}
