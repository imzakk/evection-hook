# Examples

| Example | Backend | What it shows |
|---|---|---|
| [HelloMod](HelloMod) | Mono | The mod lifecycle, config file, logging, and drawing on screen |

Build: `dotnet build examples/HelloMod -c Release`, then copy `bin/Release/net472/HelloMod.dll`
into `<Game>/EvectionHook/Mods/` (or run `evection mods <game> add path/to/HelloMod.dll`).
