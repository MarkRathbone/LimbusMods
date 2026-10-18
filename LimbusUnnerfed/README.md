# Limbus Unnerfed

Restores story-boss to their pre-nerf states and adds the ability to apply optional caps for identity levels, Uptie and E.G.O Threadspin.

## Features

- Boss restorations covering Aida, Kromer, Ambling Pearl, Queequeg, Ahab, Heathcliff, the Wild Hunt, Nelly, Kong Qiu, Lei Heng and Buffet. See `src/Data/restorations.json` for the implemented field changes.
- Independent combat caps. Levels must be positive; Level Sync uses the encounter difficulty. Uptie and Threadspin support tiers I to V. Sync uses tier III before July 20, 2023 and tier IV for later encounters. Caps never raise or lower your actual level or tier.
- Certain changes remain in place. Jia Mu's resistance changes and focused Nocturnal Sweeping encounters are retained.
- Optional manager-ID redaction. Set your own `Privacy.ManagerId` in the plugin configuration; the release has no account number built in.

## Install

When upgrading from an older name, use `Install.ps1` or remove the old plugin DLL before extracting this release. Configuration IDs are unchanged.

Requires a compatible BepInEx 6 IL2CPP loader for your current game build. Extract the release ZIP into the game folder or run `./Install.ps1 -GameDir "path/to/Limbus Company"` while the game is closed. Configuration is `BepInEx/config/local.limbus.unnerfed.cfg`.

Local test stages are hidden by default. To enable them, set `EnableReplayNodes = true` under `[Local Testing]` and restart. Existing configurations retain their saved value. T1 to 14 appear beside the last Canto VIII nodes; these encounters are local only, free to play and have no rewards.

The restorations are a reconstruction, I cannot claim they are a 1 to 1 match. Offline tests cover descriptions, caps, restoration rules and synthetic replay data. They do not replace live combat verification.

## Build and release

Install .NET 6 and .NET 10 SDKs and put the shared `LimbusBuild` folder beside this one:

```powershell
../LimbusBuild/scripts/Build-Release.ps1 -Repository .
```

Dependencies are downloaded and checksum-verified into ignored `deps/`. No sibling mod, private research fixture or game installation is needed. Output is a plugin ZIP and checksum in `artifacts/`.

Push a project/version tag, such as `LimbusUnnerfed/v0.4.20`, to build, test and publish a GitHub Release. Pull requests and branch pushes only build. Research and captured game data are excluded from Git and release packages.
