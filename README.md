# Limbus Mods

Mods and tools for Limbus Company, with a shared build and release process.

| Folder | Purpose |
| --- | --- |
| [LimbusAutodialogue](LimbusAutodialogue) | Remembers story dialogue Auto settings; does not automate battles. |
| [LimbusUltrawide](LimbusUltrawide) | Ultrawide UI, story and combat layouts. |
| [LimbusUnnerfed](LimbusUnnerfed) | Reviewed story-boss restorations and optional combat caps. |
| [LimbusCollectionExporter](LimbusCollectionExporter) | Read-only CSV/JSON collection export. |
| [LimbusBuild](LimbusBuild) | Shared build, test, packaging and release scripts. |

All projects live in this one repository. Research, captures, local game files and legacy projects are kept in ignored `research/` folders and are not published.

## Build

Install .NET 6 and .NET 10 SDKs, then run `./Build-All.ps1`. Each project uses `LimbusBuild/scripts/Build-Release.ps1`; dependencies are downloaded with checksum verification. No game installation or private research fixture is needed for compilation and public tests. Release ZIPs and checksums are saved in each project's `artifacts/` directory.

## Install

Download the appropriate ZIP from [Releases](https://github.com/MarkRathbone/LimbusMods/releases). Plugins require a compatible BepInEx 6 IL2CPP loader. Extract the ZIP into the game folder or use its `Install.ps1`. Research, game files and compatibility loader patches are not included. See each project's README for configuration and upgrade details.

## Releases

The root `.github/workflows/ci.yml` calls the shared reusable workflow. Main-branch pushes and pull requests build all projects without publishing. Pushing a project/version tag builds, tests and publishes that project's GitHub Release:

```powershell
git tag LimbusAutodialogue/v1.0.0
git push origin LimbusAutodialogue/v1.0.0
```

Other tags follow the same format: `LimbusUltrawide/v0.9.57`, `LimbusUnnerfed/v0.4.20`, `LimbusCollectionExporter/v1.0.0`, and `LimbusBuild/v1.0.0`. Each version must match its project's declaration. Releases contain only the selected package and checksum.

Unnerfed's local test stages are hidden by default. Existing users can retain an explicitly enabled testing setting. Installed mod files are not changed by builds.
