# Limbus Mods

Mods and tools for Limbus Company, with a shared build and release process.

| Folder | Purpose |
| --- | --- |
| [LimbusAutodialogue](LimbusAutodialogue) | Remembers story dialogue Auto paly setting |
| [LimbusUltrawide](LimbusUltrawide) | Improved Ultrawide UI, story and combat layouts |
| [LimbusUnnerfed](LimbusUnnerfed) | Removes nerfs from story bosses and adds optional level and threadspin caps |
| [LimbusCollectionExporter](LimbusCollectionExporter) | Exports identitiy, ego and facade ownership into CSV/JSON |
| [LimbusBuild](LimbusBuild) | Shared build, test, packaging and release scripts. |

All projects live in this one repository.

## Build

Install .NET 6 and .NET 10 SDKs, then run `./Build-All.ps1`. Each project uses `LimbusBuild/scripts/Build-Release.ps1`; dependencies are downloaded with checksum verification. Release ZIPs and checksums are saved in each project's `artifacts/` directory.

## Install

Download the appropriate ZIP from [Releases](https://github.com/MarkRathbone/LimbusMods/releases). Plugins require a compatible BepInEx 6 IL2CPP loader. Extract the ZIP into the game folder or use its `Install.ps1`. See each project's README for configuration and upgrade details.

## Releases

The root `.github/workflows/ci.yml` calls the shared reusable workflow. Main-branch pushes and pull requests build all projects without publishing. Pushing a project/version tag builds, tests and publishes that project's GitHub Release:

```powershell
git tag LimbusAutodialogue/v1.0.0
git push origin LimbusAutodialogue/v1.0.0
```

Other tags follow the same format: `LimbusUltrawide/v0.9.57`, `LimbusUnnerfed/v0.4.20`, `LimbusCollectionExporter/v1.0.0`, and `LimbusBuild/v1.0.0`. Each version must match its project's declaration. Releases contain only the selected package and checksum.

Unnerfed's local test stages are hidden by default.

## Enable Canto VIII test stages

Limbus Unnerfed includes optional test stages for the restored bosses. They are hidden by default.

1. Install Limbus Unnerfed and launch the game once to generate its configuration, then close the game.
2. Open `BepInEx/config/local.limbus.unnerfed.cfg` inside the game folder.
3. Under `[Local Testing]`, change the existing setting to:

   ```ini
   [Local Testing]
   EnableReplayNodes = true
   ```

4. Save the file and restart the game with mods enabled.
5. Open the Canto VIII map and scroll to the right of its final nodes, near **8-33**. The boss test stages appear as **T1?T14**.

These stages use the Unnerfed profiles, are free to enter, and give no rewards or story progress. To hide them again, set `EnableReplayNodes = false` and restart.
