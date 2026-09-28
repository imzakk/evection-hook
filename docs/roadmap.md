# Roadmap

- [x] Game detection: Mono / IL2CPP, x86 / x64, Unity version, anti-cheat, other loaders, Steam library scan
- [x] Installer with exact uninstall, reinstall that keeps mods, mod enable/disable
- [x] Code inspector: types, search, inspect, source, dump — Mono (full) and IL2CPP (signatures via Cpp2IL)
- [x] Mono runtime: Doorstop entry, mod lifecycle, config, logging, error isolation
- [x] Verified Mono runtime in a real game (ULTRAKILL, Unity 2022.3, via Proton)
- [ ] **IL2CPP runtime** — see [src/Evection.Core.Il2Cpp](../src/Evection.Core.Il2Cpp/README.md)
- [x] Desktop app (`src/Evection.GUI`) built from the Evection UI Figma design system
- [x] Optional Discord sign-in + settings sync (`services/sync-worker`) — **needs deploying**
- [x] Moon photo license confirmed (NASA, public domain)
- [ ] Set Proton launch options automatically
- [ ] Mod dependencies & load order rules
- [ ] In-game inspector (live GameObjects, components, field values)
- [ ] Windows-native build script (`tools/*.ps1`)
- [ ] Unreal Engine support
