# Evection.Core.Il2Cpp (planned)

Runtime for IL2CPP games. Not built yet — the installer reports IL2CPP as unsupported until `payload/core-il2cpp` exists.

## Plan

1. Doorstop (IL2CPP mode) boots a bundled CoreCLR (`EvectionHook/dotnet/`, set via `coreclr_path` / `corlib_dir`) and calls
   `Doorstop.Entrypoint.Start()` in `Evection.Core.Il2Cpp.dll` (net6.0).
2. First launch / after game updates: run Cpp2IL (dummy DLLs) → **Il2CppInterop.Generator** → `EvectionHook/interop/`
   (C# proxies for every game class). Cache by `GameAssembly.dll` hash; show progress in the log.
3. Initialise **Il2CppInterop.Runtime** (native detours via its hook provider), register an `EvectionHost` MonoBehaviour with
   `ClassInjector.RegisterTypeInIl2Cpp`, hook `SceneManager.sceneLoaded`.
4. Reuse `Evection.Core` (`ModManager`, `Log`) unchanged. Mods target net6.0 and reference `EvectionHook/interop/*.dll`.

Test games: Sons Of The Forest (2022.2), BloonsTD6 (6000.0), Iron Nest (6000.3).

Reference implementations: BepInEx 6 `BepInEx.Unity.IL2CPP`, MelonLoader `Il2CppAssemblyGenerator`.
