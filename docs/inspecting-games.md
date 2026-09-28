# Inspecting game code

`<game>` is a Steam game name (partial is fine), a game folder, or the game's `.exe`.

| Command | What you get |
|---|---|
| `evection types <game> [filter]` | Every class in the game's own code, optionally filtered by name |
| `evection search <game> <term>` | Classes, fields, properties and methods whose name contains `term` |
| `evection inspect <game> <Type>` | Outline of a class: base types, fields (with types), properties, methods |
| `evection source <game> <Type>` | Decompiled C# for one class |
| `evection dump <game> [--out dir]` | Whole-assembly decompilation to `.cs` files + `.csproj` |

Options: `--libs` includes Unity and third-party assemblies in types/search/inspect/source. `dump --all` decompiles every assembly;
`dump --assembly Name` just one.

## Mono vs IL2CPP

- **Mono** games ship real .NET assemblies — you get the full source of every method.
- **IL2CPP** games are compiled to native code. Cpp2IL recovers every class, field, property and method **signature**, but method
  bodies are empty. That's usually enough to know what to patch. The first run takes 10–60 s; after that it's cached until the game updates.

If Cpp2IL fails, the game may be obfuscated or on a very new Unity version — check `cpp2il.log` in the cache folder mentioned in the error.
