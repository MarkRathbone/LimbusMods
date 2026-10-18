# Limbus Collection Exporter

Read-only export of your identities, E.G.O and facades to CSV and JSON. Leave the game running and logged in, then run `Export-Limbus.cmd`. Output goes in ignored `outputs/`; discovery addresses are cached locally and excluded from Git.

The exporter uses Windows process query/read APIs. It does not inject code, write process memory or contact the game API. It checks the expected layouts and stops if they no longer match. Game updates can require changes.

The CSV includes item IDs, names, owned levels/tiers, facade ownership, season/release dates and standard-banner rates. Rates use the loaded single-extraction pool, not the guaranteed tenth-pull rate. If the pool is not loaded, open Extraction and retry with `-RefreshDiscovery`.

```powershell
./Export-Limbus.ps1 -RefreshDiscovery
```

Keep `Export-Limbus.cmd`, `Export-Limbus.ps1`, `LimbusRead.cs` and `limbus-facade-metadata.json` together. Standalone facade dates use the small metadata file; unknown dates remain blank.

## Build and release

With `LimbusBuild` beside this project, run `../LimbusBuild/scripts/Build-Release.ps1 -Repository .`. It checks PowerShell syntax, compiles the C# reader without opening the game, and packages the scripts with a checksum. Push the matching `LimbusCollectionExporter/v1.0.0` tag to publish through the shared workflow.
