# Writing a mod

> Mono games are supported today. IL2CPP mod loading is in progress.

## 1. Find what to change

```sh
evection search ULTRAKILL hp               # where is health stored?
evection inspect ULTRAKILL NewMovement     # all fields/methods on the player
evection source ULTRAKILL NewMovement      # read the actual code
```

## 2. Create a project

```sh
dotnet new classlib -n MyMod -f net472
```

Edit `MyMod.csproj` — replace `GAME` with the game's install folder:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <GameDir>C:\Program Files (x86)\Steam\steamapps\common\GAME</GameDir>
    <Managed>$(GameDir)\GAME_Data\Managed</Managed>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="$(GameDir)\EvectionHook\core\Evection.API.dll" Private="false" />
    <Reference Include="$(GameDir)\EvectionHook\core\0Harmony.dll" Private="false" />
    <Reference Include="$(Managed)\Assembly-CSharp.dll" Private="false" />
    <Reference Include="$(Managed)\UnityEngine.CoreModule.dll" Private="false" />
    <Reference Include="$(Managed)\UnityEngine.IMGUIModule.dll" Private="false" />
  </ItemGroup>
  <!-- Copy the built mod straight into the game -->
  <Target Name="CopyToGame" AfterTargets="Build">
    <Copy SourceFiles="$(TargetPath)" DestinationFolder="$(GameDir)\EvectionHook\Mods" />
  </Target>
</Project>
```

## 3. Write it

```csharp
using Evection;
using HarmonyLib;

[ModInfo("God Mode", "1.0.0", "You", Games = new[] { "ULTRAKILL" })]
public class GodMode : EvectionMod
{
    public static bool Enabled;

    public override void OnLoad()
    {
        Enabled = Config.Get("enabled", true, "Turn god mode on/off");
        Harmony.PatchAll(typeof(GodMode).Assembly);
        Log.Info("God mode ready");
    }
}

[HarmonyPatch(typeof(NewMovement), nameof(NewMovement.GetHurt))]
static class NoDamagePatch
{
    static bool Prefix() => !GodMode.Enabled;   // return false = skip the original method
}
```

## Lifecycle

| Method | When |
|---|---|
| `OnLoad()` | Once, after the first scene loads |
| `OnSceneLoaded(name, index)` | Every scene load |
| `OnUpdate()` / `OnLateUpdate()` | Every frame |
| `OnGUI()` | IMGUI drawing |
| `OnQuit()` | Game closing |

Available on every mod: `Log` (→ `EvectionHook/Logs/latest.log`), `Config` (→ `EvectionHook/Config/<Mod>.cfg`), `Harmony`, `Info`, `FilePath`.

## Tips

- Libraries your mod needs go in `Mods/libs/`.
- If your mod throws every frame it gets disabled after 20 errors — check the log.
- Harmony docs: https://harmony.pardeike.net/articles/intro.html
