# Dependencies

What needs to be installed, by whom. **Players should never need to install anything** — if a release requires something
extra from players, that's a bug.

## Players (using a release)

| Need | Why | How |
|---|---|---|
| Nothing | Releases are self-contained: the .NET runtime, UI framework, fonts, Doorstop and Cpp2IL are all bundled | Download, unzip, run `EvectionHook` |
| *(Linux/Proton only)* a launch option | Wine must be told to load our `winhttp.dll` instead of its built-in one | Steam → game → Properties → Launch Options: `WINEDLLOVERRIDES="winhttp=n,b" %command%` |

Network: the app downloads game cover images from Steam's public CDN when Steam hasn't cached them locally.

Supported systems: Windows 10/11 x64, Linux x64 (glibc). Games: Windows builds of Unity games (native or through Proton).

## Mod authors

| Need | Version | Why |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0 or newer | Compile your mod |
| An editor | any | VS Code + C# Dev Kit, Rider, or Visual Studio |
| `Evection.API.dll`, `0Harmony.dll` | from `<Game>/EvectionHook/core/` | Reference these from your mod project (don't ship them) |
| The game's assemblies | Mono: `<Game>_Data/Managed/` | Reference `Assembly-CSharp.dll`, `UnityEngine.CoreModule.dll`, etc. |

## Contributors (building Evection Hook)

| Need | Version | Why | Install |
|---|---|---|---|
| .NET SDK | 8.0+ | Build everything | Windows: `winget install Microsoft.DotNet.SDK.8` · Linux: `sudo apt install dotnet-sdk-8.0` or [dotnet-install.sh](https://learn.microsoft.com/dotnet/core/tools/dotnet-install-script) |
| git | any | Source control | your package manager |
| bash, curl, unzip | any | `tools/*.sh` scripts (use Git Bash or WSL on Windows) | your package manager |
| Internet access | — | NuGet packages and `tools/fetch-deps.sh` | — |
| Node.js *(only for the sync server)* | 22+ | Testing/deploying `services/sync-worker` | https://nodejs.org or `nvm install 22` |
| A Cloudflare + Discord developer account *(only to run your own sync server)* | — | Optional sign-in / settings sync | see [services/sync-worker](services/sync-worker/README.md) |

NuGet restores these automatically (sources in [nuget.config](nuget.config)):

| Package | Used by | Feed |
|---|---|---|
| HarmonyX | Evection.API — method patching for mods | nuget.org |
| Avalonia (+ Desktop, Fluent theme) | Evection.GUI — cross-platform desktop UI | nuget.org |
| CommunityToolkit.Mvvm | Evection.GUI — view-model plumbing | nuget.org |
| Avalonia.Headless, Avalonia.Skia | tools/Evection.GUI.Snapshots — offscreen screenshots | nuget.org |
| ICSharpCode.Decompiler | Evection.Inspector — decompiling game code | nuget.org |
| UnityEngine.Modules | Evection.Core.Mono, examples — compile-time Unity API only (never shipped) | nuget.bepinex.dev |
| xunit, Microsoft.NET.Test.Sdk | tests | nuget.org |

`tools/fetch-deps.sh` downloads these binaries into `deps/` (git-ignored). Versions are pinned in [tools/deps.env](tools/deps.env):

| Binary | Why |
|---|---|
| [UnityDoorstop](https://github.com/NeighTools/UnityDoorstop) (`winhttp.dll`, x86 + x64) | Gets our code running inside the game at startup |
| [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL) (Linux + Windows builds) | Rebuilds IL2CPP games' code so it can be browsed |

Committed assets (no download needed): Geist and Geist Mono fonts (`src/Evection.GUI/Assets/Fonts`, SIL OFL 1.1),
the moon photograph and app icon (`src/Evection.GUI/Assets`).

Licenses for all of the above: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
