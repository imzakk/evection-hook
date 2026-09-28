# Third-party notices

Evection Hook's own code is MIT licensed. It uses or redistributes the following components, each under its own license.

| Component | License | Distributed how | Source |
|---|---|---|---|
| UnityDoorstop | LGPL-2.1 | Unmodified `winhttp.dll` binaries copied into games | https://github.com/NeighTools/UnityDoorstop |
| Cpp2IL | MIT | Unmodified executable bundled with the CLI | https://github.com/SamboyCoding/Cpp2IL |
| HarmonyX | MIT | `0Harmony.dll` in the game's `EvectionHook/core` | https://github.com/BepInEx/HarmonyX |
| MonoMod | MIT | Dependency of HarmonyX | https://github.com/MonoMod/MonoMod |
| Mono.Cecil | MIT | Dependency of HarmonyX | https://github.com/jbevain/cecil |
| ICSharpCode.Decompiler (ILSpy) | MIT | Compiled into the CLI | https://github.com/icsharpcode/ILSpy |
| .NET runtime | MIT | Bundled in self-contained builds | https://github.com/dotnet/runtime |
| Avalonia UI | MIT | Compiled into the desktop app | https://github.com/AvaloniaUI/Avalonia |
| CommunityToolkit.Mvvm | MIT | Compiled into the desktop app | https://github.com/CommunityToolkit/dotnet |
| SkiaSharp / HarfBuzzSharp | MIT | Native libraries bundled with the desktop app | https://github.com/mono/SkiaSharp |
| Geist, Geist Mono (Vercel) | SIL Open Font License 1.1 | Font files embedded in the desktop app; license text in `src/Evection.GUI/Assets/Fonts/OFL.txt` | https://github.com/vercel/geist-font |
| Moon photograph (cover screen) | Public domain — NASA, Artemis II image gallery | `src/Evection.GUI/Assets/moon.jpg` (rotated and cropped) | https://www.nasa.gov/gallery/ |

Moon image courtesy of NASA. NASA material is not copyrighted, but its use here does not imply endorsement by NASA
([NASA media usage guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/)).

**LGPL note (UnityDoorstop):** we ship Doorstop as an unmodified, separately replaceable DLL. Its source is available at the link
above; users may replace `winhttp.dll` with their own build.

Unity, UnityEngine and related names are trademarks of Unity Technologies. Evection Hook is not affiliated with Unity Technologies
or with any game developer. `UnityEngine.Modules` is used only as compile-time reference assemblies and is never redistributed.
