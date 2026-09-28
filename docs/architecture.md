# Architecture

```
                    ┌────────────── Your PC ──────────────┐
  evection (CLI)  ──┤ Detector  → what game is this?      │
  GUI (planned)   ──┤ Installer → copy payload into game  │
                    │ Inspector → browse game code        │
                    └─────────────────────────────────────┘
                                   │ install
                                   ▼
<Game>/
  winhttp.dll            UnityDoorstop proxy: Windows loads it into the game at startup
  doorstop_config.ini    tells Doorstop which assembly to run
  EvectionHook/
    install.json         every file we added (uninstall removes exactly these)
    core/                Evection.Core.Mono.dll, Evection.Core.dll, Evection.API.dll, HarmonyX…
    Mods/                player's mods (Mods/libs for shared libraries)
    Config/              one .cfg per mod
    Logs/                latest.log, previous.log
```

## Projects

| Project | Runs where | Target | Purpose |
|---|---|---|---|
| `Evection.Detector` | your PC | net8.0 | Finds Unity games (incl. Steam libraries); detects backend, arch, Unity version, anti-cheat, other loaders |
| `Evection.Installer` | your PC | net8.0 | Install / reinstall / uninstall; `ModsFolder` enables/disables mods |
| `Evection.Inspector` | your PC | net8.0 | Browse, search, decompile game code (ILSpy engine; Cpp2IL for IL2CPP) |
| `Evection.Cli` | your PC | net8.0 | The `evection` command |
| `Evection.GUI` | your PC | net8.0 | The desktop app (Avalonia) — UI only, calls the libraries above |
| `Evection.API` | in game | net472 + net6.0 | What mods reference: `EvectionMod`, `ModInfo`, `ModLogger`, `ModConfig` |
| `Evection.Core` | in game | net472 + net6.0 | Engine-agnostic: logging, mod discovery, error isolation |
| `Evection.Core.Mono` | in game | net472 | Doorstop entry point + Unity bridge for Mono games |
| `Evection.Core.Il2Cpp` | in game | net6.0 | Planned — see its README |

## Startup sequence (Mono)

1. The game starts; Windows loads our `winhttp.dll` (Doorstop) from the game folder instead of the system one.
2. Doorstop reads `doorstop_config.ini` and calls `Doorstop.Entrypoint.Start()` in `Evection.Core.Mono.dll` — **before** any game code runs.
3. `Entrypoint` installs an assembly resolver (so `core/` and `Mods/libs/` are searchable), then `Bootstrap` opens the log and subscribes to `SceneManager.sceneLoaded`.
4. On the first scene load the engine is fully up: we create a hidden `DontDestroyOnLoad` GameObject (`EvectionHost`) and `ModManager` loads `Mods/*.dll`.
5. `EvectionHost` forwards `Update`/`LateUpdate`/`OnGUI`/`OnApplicationQuit` to every mod. A mod that throws 20 times in per-frame callbacks is switched off so the game stays playable.

## Inspector

- **Mono:** `<Game>_Data/Managed/*.dll` are real .NET assemblies — decompiled directly to full C#.
- **IL2CPP:** C# was compiled to native code. Cpp2IL reads `GameAssembly.dll` + `global-metadata.dat` and rebuilds "dummy"
  assemblies with every class, field and method signature (bodies are empty). Cached under `~/.cache/evection-hook/cpp2il/` (Linux) or
  `%LOCALAPPDATA%\evection-hook\cpp2il\` (Windows), keyed by the game build.
- "Game assemblies" (the default view) are everything except known Unity/System/library assemblies — see `GameCode.IsGameAssembly`.

## Desktop app (`src/Evection.GUI`)

MVVM with Avalonia + CommunityToolkit.Mvvm. The visual design comes from the **Evection UI** Figma file:

| Folder | What |
|---|---|
| `Theme/Tokens.axaml` | Colour (light/dark), radius and shadow tokens copied from the Figma variables |
| `Theme/Typography.axaml` | Heading/Label/Copy/Mono text styles |
| `Theme/Controls.axaml` | Button, Switch, Checkbox, Badge, Input, tabs, settings groups, toast, modal |
| `Controls/` | Icon (Figma icon set as geometry), Badge, StarField, GameImage (Steam cover/header/icon) |
| `Services/` | Settings (JSON in the user config folder), game library, toasts, settings sync |
| `ViewModels/`, `Views/` | Cover → Sign-in → Shell (sidebar, top bar) → Home / Games / Game / Code browser / Settings |

Screens: `dotnet run --project tools/Evection.GUI.Snapshots -- snapshots` renders every screen headlessly.

## Settings sync (optional)

`services/sync-worker` is a Cloudflare Worker. The app opens the browser for Discord sign-in, receives a signed token on a
loopback port, and then GETs/PUTs one JSON blob of the settings marked `[Synced]` in `AppSettings` (never paths or files).
If no server URL is compiled in (`-p:SyncServerUrl=...`), sign-in is shown as unavailable and everything else works.

## Launching and "mods only from evection hook"

`GameLauncher` starts Steam games with `steam -applaunch <appid> --evection-mods` (so Proton and launch options still
apply) and other games directly with `--evection-mods`. `EvectionHook/loader.cfg` holds `mods_only_from_evection`;
when it's true the runtime (`LoaderPaths.ModsEnabledForThisLaunch`) skips mods unless `--evection-mods` is on the
command line, so launching from Steam gives the unmodded game.

Game art comes from Steam's local cache (`appcache/librarycache/<appid>`), falling back to Steam's public CDN
(cached in `~/.cache/evection-hook/art`). Non-Steam games show a plain letter tile.
