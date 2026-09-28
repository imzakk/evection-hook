# Contributing

Thanks for helping! Evection Hook aims to be the easiest mod loader to use, so clear errors and simple UX matter as much as features.

## Getting started

1. Install the tools in [DEPENDENCIES.md](DEPENDENCIES.md#contributors-building-evection-hook).
2. `tools/fetch-deps.sh && dotnet build && dotnet test`
3. Read [docs/architecture.md](docs/architecture.md).

## Ground rules

- **No anti-cheat bypasses**, no code that hides the loader from anti-cheat, and no cheats that affect other players. PRs that add these will be closed. See [docs/anti-cheat-policy.md](docs/anti-cheat-policy.md).
- Never write outside the game folder (except the Cpp2IL cache) and always record installed files in `install.json`.
- Error messages are for players: say what went wrong **and** what to do next.
- Code in `Evection.Core*` runs inside games — it must never throw into the game. Catch, log, carry on.
- Add a test for detector/installer/inspector changes (`tests/Evection.Tests`). Fake game folders are built with `FakeGame`.
- Code style is enforced by `.editorconfig`; run `dotnet format` before committing.

## Reporting game compatibility

Use the **Game support** issue template and attach `EvectionHook/Logs/latest.log` plus the output of `evection detect "<game>"`.
