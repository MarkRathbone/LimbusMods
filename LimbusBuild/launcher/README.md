# Launcher source

This folder tracks the modded launcher and compatibility checks. Runtime state, game binaries, loader DLLs, logs, backups and Python dependencies are excluded.

For the existing installation, deploy `Start-ModdedLimbus.ps1` and `export_guard.py` to `BepInEx/maintenance`, alongside its existing `vendor` dependencies. Keep the existing validated `state/baseline.json`; do not initialize over it. Place `Launch-Modded-Limbus.cmd` in the game root. The opt-in bootstrap shim and compatible BepInEx installation are prerequisites; these scripts are not a standalone loader installer.

For tests, install Capstone in a Python environment and run `python -m unittest discover -s LimbusBuild/launcher -p test_export_guard.py` from the repository root. No game files are needed for unit tests.

The following describes the installed launcher's operation.
# Modded Limbus launcher

Double-click `Launch-Modded-Limbus.cmd` in the Limbus Company folder instead of launching the game directly. Keep Steam logged in and let it finish any updates first. The launcher checks the installed build; it does not download Steam updates.

The root CMD calls `BepInEx/maintenance/Start-ModdedLimbus.ps1`. Scripts, recorded build state, logs and repair backups stay together in `BepInEx/maintenance/`. Python 3 must be available as `python` (already installed on this machine). Capstone and its licence are bundled locally; no pip/network operation runs at startup.

## What happens before launch

1. Read the Steam build ID, GameAssembly SHA256, UnityPlayer SHA256 and hashes of the two compatibility loader DLLs.
2. If the binary hashes match the baseline, leave the loader unchanged.
3. If GameAssembly changed, validate its export layout and every mapped API instruction signature against the baseline. Normalize relocation destinations, but retain registers, constants and field offsets. Equivalent names pointing at the same address are treated as aliases.
4. For a validated export-name update, stage new IL2CPP imports and the chainloader runtime-invoke lookup. Back up both loader DLLs and the previous baseline. Check the staged imports before installation. Restore the old files and baseline if installation fails.
5. Recheck the game hash, launch the checked executable with Steam's app context, and watch for fresh BepInEx startup completion. Record the result. Do not suppress the interop-cache warning or change mod DLLs/settings.

A changed Steam build ID with an unchanged GameAssembly requires no remapping. The new build ID is recorded. A running game is never closed by the launcher.

## When it stops

The launcher refuses repair/launch if Steam is still updating, Unity executable sections or its version change, loader files changed independently, exports are missing/reordered incompatibly, unrecognized API instruction signatures change, or another launcher instance holds the lock. Read `logs/` for the reason. A startup failure or timeout also reports the log path; the launcher does not forcibly terminate an open game.

This is conservative automation for export-name changes, not a guarantee that every weekly update is compatible. The repeated, narrowly defined custom-attribute wrapper change is now recognized automatically as described below; other changes require review. Reinitialize only after manually validating a new installation; do not erase a failing baseline just to get past the check.

The export ordering and normalized instruction shapes are compatibility heuristics, not a proof of semantic identity. Engine changes and generated-wrapper incompatibilities can require a separate compatibility update. The launcher does not regenerate interop assemblies and does not change `assembly-hash.txt`.

Regular Steam Play, direct EXE launches, and ordinary launches through other launchers run without our BepInEx mods. `doorstop_config.ini` stays `enabled=false`; this CMD alone opts in using `--doorstop-enabled true` for the checked game process. It never temporarily enables the shared configuration. Doorstop's documented command-line override is supported by the installed 4.5.0 DLL: https://github.com/NeighTools/UnityDoorstop/blob/master/README.md

The installed proxy also requires `--limbus-modded-bootstrap=1`, supplied only by this CMD. Without it the shim's DllMain returns before logging, exception handlers, GameAssembly preloading, background threads or API hooks; Windows WinHTTP exports remain forwarded to the system DLL. This avoids interfering with other launchers while Doorstop is disabled. Do not add these arguments to Steam's launch options. Existing running sessions are unaffected.

This controls our bootstrap and BepInEx loader; another launcher can still inject its own mods.

Use this CMD for modded startup. The game is started directly after validation so Steam does not silently replace the checked binary as part of an `-applaunch` update. Steam must already be running and logged in.

## Commands

```powershell
# Check without repairing loader files or launching.
.\Launch-Modded-Limbus.cmd -CheckOnly

# Validate and perform a supported repair, without launching.
.\Launch-Modded-Limbus.cmd -NoLaunch
```

The installation is validated through Steam build **26156805** (29 October 2026). `state/baseline.json` records the exact fingerprints and export bindings. Backups are created only when a repair is needed. The launcher does not overwrite an existing baseline during initialization.

## Validation

Seven unit tests cover export renaming, RVA shifts, duplicate aliases, rejected export-count/signature changes, relocation normalization, retained field offsets and invalid PE input. An isolated copy of the current game DLL was modified to rename an export; the full PowerShell staging/backup/repair path updated the copied loader successfully. API-body and UnityPlayer changes were rejected with copied loader hashes unchanged. No simulated game executable was launched, and no live game binary was modified.

The installed current-build check passes without altering the live loader. The live game was left running during setup; the first actual launch through this CMD will perform the normal startup-log check.

Additional integration check: a write lock on the second loader DLL forced a partial installation failure. The first DLL was restored, the unchanged locked second DLL was preserved, and the previous baseline stayed byte-for-byte identical.


## Recognized weekly updates (29 October 2026)

The launcher now automatically accepts the recurring custom-attribute obfuscation change only when all three exports (`il2cpp_custom_attrs_has_attr`, `il2cpp_custom_attrs_get_attr`, `il2cpp_custom_attrs_construct`) change the same adjacent `mov eax,[rax+offset]` and `add/sub eax,constant` pair before `cdqe`. Every other recorded instruction must match. The offset must be aligned and within 0–4096; the constant must be 1–65535. Partial or inconsistent changes stop the launch. Export counts, ordering, installed loader hashes and import checks still apply.

A changed UnityPlayer hash is accepted only if the full ProductVersion and every executable section (name, address, size and SHA256) match the previous validated baseline. This accommodates non-code file changes without accepting a different engine executable. Other data/resource changes can still affect behavior: these fingerprints and instruction patterns are compatibility heuristics, not a proof that every mod remains correct.

Backups, staged DLL validation, concurrent-update checks, rollback on installation failure, and startup-log validation remain active. Startup failures are reported; the launcher does not close a running game or claim to repair gameplay changes. Steam/direct launches remain unmodded.

The currently validated build is 26156805, Unity 6000.3.12f1. Its baseline was extended with executable-section fingerprints after successful mod startup, without regenerating or suppressing interop warnings. Sixteen unit tests cover both accepted patterns and rejection boundaries. A separate copy of the game/loader passed the PowerShell staging, backup, repair and unchanged-recheck path with a simulated previous baseline; no test game was launched.
