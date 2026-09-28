using System.Diagnostics;
using Evection.Detector;

namespace Evection.Installer;

/// <summary>
/// Starts a game with mods on. The game gets <see cref="ModsArgument"/> on its command line, which tells the loader
/// "this launch came from evection hook" — used when mods are set to load only for launches from evection hook.
/// </summary>
public static class GameLauncher
{
    public const string ModsArgument = "--evection-mods";
    public const string ProfileArgument = "--evection-profile";

    /// <summary>Arguments passed to the game: mods on, and which profile to load.</summary>
    public static string[] GameArguments(string? profileId) =>
        profileId == null ? [ModsArgument] : [ModsArgument, ProfileArgument, profileId];

    /// <summary>Starts the game with a profile (the active one if null). The profile also becomes the active one.</summary>
    public static void Launch(GameInfo game, string? profileId = null)
    {
        if (game.EvectionInstalled)
        {
            profileId ??= ModProfiles.GetActive(game).Id;
            ModProfiles.SetActive(game, profileId);
            ModProfiles.MarkPlayed(game, profileId);
        }
        var gameArgs = GameArguments(game.EvectionInstalled ? profileId : null);

        if (game.SteamAppId is { } appId)
        {
            // Through Steam, so Steam features, Proton and the game's launch options all still apply.
            var steam = SteamLibrary.FindSteamExecutable()
                ?? throw new InstallException("couldn't find steam. start steam and try again.");
            Start(steam, null, ["-applaunch", appId.ToString(), .. gameArgs]);
            return;
        }

        if (game.Platform == GamePlatform.Windows && !OperatingSystem.IsWindows())
            throw new InstallException("this isn't a steam game, so start it the way you normally do (e.g. lutris or bottles).");

        Start(game.ExecutablePath, game.GameDirectory, gameArgs);
    }

    private static void Start(string file, string? workingDirectory, params string[] args)
    {
        var psi = new ProcessStartInfo(file) { UseShellExecute = false };
        if (workingDirectory != null)
            psi.WorkingDirectory = workingDirectory;
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        try
        {
            Process.Start(psi);
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            throw new InstallException($"couldn't start the game: {e.Message}");
        }
    }
}
