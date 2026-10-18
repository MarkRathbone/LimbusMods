# Limbus Build

Shared build and release scripts for the projects in [MarkRathbone/LimbusMods](https://github.com/MarkRathbone/LimbusMods). This folder is part of that repository, alongside the individual mods.

```powershell
./scripts/Build-Release.ps1 -Repository ../LimbusUltrawide
```

Requires .NET 6 and .NET 10 SDKs. `-CoreDir path/to/BepInEx/core` uses existing dependencies; otherwise the builder downloads BepInEx IL2CPP build 755, verifies SHA256 `3616d6a67f5f595973ec4aa7bd7edaf7f799d5bb9926f7146a6dcc7b4abf478f`, and extracts it into the project's ignored `deps/`. No game binaries or generated interop assemblies are needed for compilation.

Each project has a `release.json` specifying its project, optional tests and explicit package-file list. Packages contain only the plugin/scripts, README and installer. Research, game files, dependencies and account exports are excluded. ZIPs and checksums are saved in each project's ignored `artifacts/` directory. `Test-Build.ps1` checks packaging and rejection of invalid tags, private paths, traversal and invalid PowerShell.

The shared reusable workflow is at the repository root: `.github/workflows/mod-release.yml`. `.github/workflows/ci.yml` selects projects and calls it. Branch pushes, pull requests and manual runs build all projects; project/version tags build and release the selected project after its tests pass.

```powershell
git tag LimbusUltrawide/v0.9.57
git push origin LimbusUltrawide/v0.9.57
```

The tag version must exactly match the project's version. Releases use project-specific names and include the ZIP and SHA256 file. Suffix versions create prereleases. GitHub's standard token handles publication; no personal token is stored in the repo. Existing releases are not silently replaced. `LimbusBuild/v1.0.0` packages these build scripts as a source release.
