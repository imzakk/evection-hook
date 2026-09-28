using Evection;
using UnityEngine;

namespace HelloMod
{
    [ModInfo("Hello Mod", "1.0.0", "Evection Hook", Description = "Proves Evection Hook is working.")]
    public class HelloMod : EvectionMod
    {
        private bool showLabel;
        private string lastScene = "";

        public override void OnLoad()
        {
            showLabel = Config.Get("showLabel", true, "Draw 'Evection Hook is running' in the corner of the screen");
            Log.Info("Hello from Evection Hook!");
        }

        public override void OnSceneLoaded(string sceneName, int buildIndex)
        {
            lastScene = sceneName;
            Log.Info($"Scene loaded: {sceneName} (#{buildIndex})");
        }

        public override void OnGUI()
        {
            if (showLabel)
                GUI.Label(new Rect(10, 10, 600, 25), $"Evection Hook is running — scene: {lastScene}");
        }
    }
}
