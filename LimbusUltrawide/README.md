# Limbus Ultrawide

Ultrawide layouts for Limbus Company's menus, combat UI, story scenes and effects. Includes ultrawide resolution choices in Graphics settings and enables the layout when an ultrawide resolution is active.

## Install

When upgrading from an older name, use `Install.ps1` or remove the old plugin DLL before extracting this release. Configuration IDs are unchanged.

Requires a compatible BepInEx 6 IL2CPP loader for your current game build. Releases contain this plugin only, not the game, loader, metadata or compatibility patches.

Extract the release ZIP into the game folder, or run `./Install.ps1 -GameDir "path/to/Limbus Company"` with the game closed. The plugin goes in `BepInEx/plugins/LimbusUltrawide/`. Settings are saved in `BepInEx/config/local.limbus.verticalui.cfg`.

## Walpurgisnacht event menus

`[Menu] WalpurgisFraming = true` preserves the native event-menu artwork, door layout and decorative masks. Mirrored copies of the existing background fill additional side space without enlarging or stretching the central art. Entry costs, locks and archived-event buttons retain their normal game behavior.

The shared menu family includes Walpurgisnacht 4, 5, 6 and 8; live visual checks are still required across those screens.

## Build

Install the .NET 6 and .NET 10 SDKs. With the shared `LimbusBuild` folder beside this one, run:

```powershell
../LimbusBuild/scripts/Build-Release.ps1 -Repository .
```

The script downloads checksum-verified BepInEx build 755 into ignored `deps/`, builds the plugin, runs layout checks and creates a ZIP and checksum in `artifacts/`. A manual build can use `dotnet build src -c Release -p:CoreDir="path/to/BepInEx/core"`.

Tests check layout calculations; they cannot verify every live UI or effect. Game updates may require compatibility changes. The latest Railway selector and Sanguine Desire adjustments still need visual confirmation.

## Releases

Push a project/version tag, such as `LimbusUltrawide/v0.9.57`. The shared GitHub Actions workflow builds, tests and publishes the ZIP and checksum. Branch pushes and pull requests build without publishing. Research, captures and local game files stay in ignored folders.
