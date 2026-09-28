using System;
using HarmonyLib;

namespace Evection
{
    /// <summary>
    /// Base class for every mod. Subclass it, add <see cref="ModInfoAttribute"/>, and drop the compiled .dll
    /// into the game's EvectionHook/Mods folder.
    /// </summary>
    /// <example>
    /// <code>
    /// [ModInfo("My Mod", "1.0.0", "Me")]
    /// public class MyMod : EvectionMod
    /// {
    ///     public override void OnLoad() => Log.Info("Hello!");
    /// }
    /// </code>
    /// </example>
    public abstract class EvectionMod
    {
        /// <summary>Name, version and author from the <see cref="ModInfoAttribute"/>.</summary>
        public ModInfoAttribute Info { get; internal set; } = null!;

        /// <summary>Writes to EvectionHook/Logs/latest.log, prefixed with this mod's name.</summary>
        public ModLogger Log { get; internal set; } = null!;

        /// <summary>A Harmony instance unique to this mod, for patching game methods.</summary>
        public Harmony Harmony { get; internal set; } = null!;

        /// <summary>This mod's settings file (EvectionHook/Config/&lt;ModName&gt;.cfg).</summary>
        public ModConfig Config { get; internal set; } = null!;

        /// <summary>Full path of this mod's .dll.</summary>
        public string FilePath { get; internal set; } = "";

        /// <summary>Called once, after the first scene has loaded and the game is ready.</summary>
        public virtual void OnLoad() { }

        /// <summary>Called every frame (Unity's Update).</summary>
        public virtual void OnUpdate() { }

        /// <summary>Called every frame after all Updates (Unity's LateUpdate).</summary>
        public virtual void OnLateUpdate() { }

        /// <summary>Called for drawing immediate-mode GUI (Unity's OnGUI). Mono games only for now.</summary>
        public virtual void OnGUI() { }

        /// <summary>Called whenever a scene finishes loading.</summary>
        public virtual void OnSceneLoaded(string sceneName, int buildIndex) { }

        /// <summary>Called when the game is closing.</summary>
        public virtual void OnQuit() { }
    }

    /// <summary>Describes a mod. Required on every <see cref="EvectionMod"/> subclass.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ModInfoAttribute : Attribute
    {
        public ModInfoAttribute(string name, string version, string author)
        {
            Name = name;
            Version = version;
            Author = author;
        }

        public string Name { get; }
        public string Version { get; }
        public string Author { get; }

        /// <summary>Optional one-line description shown in mod lists.</summary>
        public string? Description { get; set; }

        /// <summary>Lower loads first. Default 0.</summary>
        public int Priority { get; set; }

        /// <summary>
        /// Optional: only load when the game's executable name (without .exe) matches one of these.
        /// Leave empty to load in any game.
        /// </summary>
        public string[]? Games { get; set; }
    }
}
