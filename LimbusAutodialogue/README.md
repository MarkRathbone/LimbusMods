# Limbus Autodialogue

Remembers the last manually selected story dialogue Auto dialogue mode.

## Install

When upgrading from an older name, use `Install.ps1` or remove the old plugin DLL before extracting this release. Configuration IDs are unchanged.

Requires a compatible BepInEx 6 IL2CPP loader. Extract the release ZIP into the game folder, or run `./Install.ps1 -GameDir "path/to/Limbus Company"` with the game closed. Configuration is saved in `BepInEx/config/local.limbus.retainautoplay.cfg` under `[Story]`.

## Build and release

Install the .NET 6 and .NET 10 SDKs. With `LimbusBuild` beside this project, run:

```powershell
../LimbusBuild/scripts/Build-Release.ps1 -Repository .
```

This downloads verified build dependencies into ignored `deps/` and produces a plugin ZIP and checksum in `artifacts/`. A manual build can use `dotnet build src -c Release -p:CoreDir="path/to/BepInEx/core"`.

Push the matching project/version tag (`LimbusAutodialogue/v1.0.0`) to publish a GitHub Release through the shared workflow. Branch pushes and pull requests build without publishing.
