param([Parameter(Mandatory=$true)][string]$Repository)
$ErrorActionPreference = 'Stop'
$Repository = (Resolve-Path -LiteralPath $Repository).Path
$deps = Join-Path $Repository 'deps'
$core = Join-Path $deps 'BepInEx/core'
$archive = Join-Path $deps 'BepInEx-755.zip'
$expected = '3616D6A67F5F595973EC4AA7BD7EDAF7F799D5BB9926F7146A6DCC7B4ABF478F'
New-Item -ItemType Directory -Path $deps -Force | Out-Null
if (!(Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expected) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $url = 'https://builds.bepinex.dev/projects/bepinex_be/755/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.755%2B3fab71a.zip'
    & curl.exe --fail --location --retry 3 --output $archive $url
    if ($LASTEXITCODE -ne 0) { throw 'BepInEx dependency download failed.' }
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expected) { throw 'BepInEx archive checksum mismatch.' }
Expand-Archive -LiteralPath $archive -DestinationPath $deps -Force
if (!(Test-Path -LiteralPath (Join-Path $core 'BepInEx.Core.dll'))) { throw 'BepInEx archive did not contain the expected core files.' }
return $core
