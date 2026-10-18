param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
$GameDir = (Resolve-Path -LiteralPath $GameDir).Path
if (!(Test-Path -LiteralPath (Join-Path $GameDir 'LimbusCompany.exe'))) { throw 'Choose the Limbus Company game folder.' }
if (Get-Process LimbusCompany -ErrorAction SilentlyContinue) { throw 'Close the game before installing.' }
$mod = 'LimbusUnnerfed'
$source = Join-Path $PSScriptRoot "BepInEx/plugins/$mod/$mod.dll"
if (!(Test-Path -LiteralPath $source)) { $source = Join-Path $PSScriptRoot "src/bin/Release/net6.0/$mod.dll" }
if (!(Test-Path -LiteralPath $source)) { throw 'Build the project or extract a release ZIP first.' }
if (!(Test-Path -LiteralPath (Join-Path $GameDir 'BepInEx/core/BepInEx.Unity.IL2CPP.dll'))) { throw 'Install a compatible BepInEx IL2CPP loader first.' }
$destination = Join-Path $GameDir "BepInEx/plugins/$mod/$mod.dll"
if (Test-Path -LiteralPath $destination) {
    $backup = Join-Path $GameDir ("BepInEx/backups/$mod/" + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.dll')
    New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
    Copy-Item -LiteralPath $destination -Destination $backup
}
$legacyName = 'LimbusCompanyUnnerfed'
foreach ($relative in @("BepInEx/plugins/$legacyName/$legacyName.dll", "BepInEx/plugins/$legacyName.dll")) {
    $oldPlugin = Join-Path $GameDir $relative
    if (Test-Path -LiteralPath $oldPlugin) {
        $saved = Join-Path $GameDir ("BepInEx/backups/$mod/legacy-" + [Guid]::NewGuid().ToString('N') + '.dll')
        New-Item -ItemType Directory -Path (Split-Path -Parent $saved) -Force | Out-Null
        Move-Item -LiteralPath $oldPlugin -Destination $saved
    }
}
New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
Copy-Item -LiteralPath $source -Destination $destination -Force
Write-Host "Installed $mod. Launch using your compatible mod loader."
