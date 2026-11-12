# Limbus Collection Exporter

Exports your identities, E.G.O and facades to CSV and JSON. Leave the game running and logged in, then run `Export-Limbus.cmd`. Output goes in `outputs/`.

The exporter uses Windows process query/read APIs. It does not inject code, write process memory or contact the game API. It resolves identity/E.G.O catalog fields by their runtime metadata names and checks the account layouts before reading values. It stops on unsupported account layout changes. Game updates can require changes.

The collection CSV contains spreadsheet formulas in A (Sinner number), M (Percentage) and N (Order). Percentage uses column C to identify E.G.O, including its denominator. Formulas assume a header in row 1 and data starting in row 2. Import with formula conversion enabled; paste formulas rather than values when transferring A, E, M and N between Sheets tabs. Spreadsheet formatting and validation are not stored in CSV. Name cells use spreadsheet `HYPERLINK` formulas linking to the item’s Limbus Company wiki.gg page, with the original name as the visible label. Standalone façades link to their section on the Unique Façades list. JSON retains plain names and numeric rates from the game; spreadsheet formulas only affect CSV. CSV viewers without formula support display the formula instead of a clickable name. Links follow wiki title conventions; newly released or unusually named pages may need a correction.

The CSV includes names, owned levels/tiers, facade ownership, season/release dates and standard-banner rates. Rates use the single-extraction pool, not the guaranteed tenth-pull rate. If the pool is not loaded, open Extraction and retry with `-RefreshDiscovery`.

```powershell
./Export-Limbus.ps1 -RefreshDiscovery
```

Keep `Export-Limbus.cmd`, `Export-Limbus.ps1`, `LimbusRead.cs` and `limbus-facade-metadata.json` together. Standalone facade dates use the small metadata file; unknown dates remain blank.

## Build and release

With `LimbusBuild` beside this project, run `../LimbusBuild/scripts/Build-Release.ps1 -Repository .`. It checks PowerShell syntax, compiles the C# reader without opening the game, and packages the scripts with a checksum. Push the matching `LimbusCollectionExporter/v1.0.0` tag to publish through the shared workflow.
