# Evection.GUI

The desktop app. UI only — all real work happens in `Evection.Detector`, `Evection.Installer` and `Evection.Inspector`.

Design source: the **Evection UI** Figma file (tokens, text styles, components, and the "Music App" frames for app layout).
When the design changes in Figma, update `Theme/Tokens.axaml` (colours), `Theme/Typography.axaml` (text styles) and
`Controls/Icons.cs` (icon paths) to match.

```
Theme/        Tokens.axaml · Typography.axaml · Controls.axaml
Controls/     Icon, Icons, Badge, StarField, GameImage
Services/     AppSettings, SettingsStore, GameLibrary, SyncService, ArtCache, Toasts, Shell
ViewModels/   MainWindow → Cover / SignIn / Shell → Home, Games, Game, CodeBrowser, Settings
Views/        matching .axaml files (Pages/ for the main pages)
Assets/       Geist fonts (OFL), moon.jpg, icon
```

Run it: `dotnet run --project src/Evection.GUI`. Screenshot every screen: `dotnet run --project tools/Evection.GUI.Snapshots -- snapshots`.

Rules of thumb:
- All UI text is lowercase and short. Game and mod names keep their real spelling.
- Only the cover and sign-in screens are allowed to be flashy; everything else uses the plain settings-group / card patterns.
- New settings that should follow the player to another PC get `[Synced]` in `AppSettings`; machine-specific ones don't.
