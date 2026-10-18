$ErrorActionPreference = 'Stop'
$builder = Join-Path $PSScriptRoot 'Build-Release.ps1'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('limbus-build-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
Set-Content -LiteralPath (Join-Path $fixture 'README.md') -Value 'Build fixture'
Set-Content -LiteralPath (Join-Path $fixture 'LimbusRead.cs') -Value 'public class LimbusBuildTestReader {}'
Set-Content -LiteralPath (Join-Path $fixture 'Probe.ps1') -Value 'Write-Output "fixture"'
$config = @{name='BuildFixture';kind='powershell';version='1.2.3';packageFiles=@('README.md','LimbusRead.cs','Probe.ps1')}
function Save-Config { $config | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $fixture 'release.json') -Encoding UTF8 }
function Reject([scriptblock]$Action,[string]$Message) {
    $failure = $null
    try { & $Action } catch { $failure = $_.Exception.Message }
    if (!$failure -or $failure -notlike "*$Message*") { throw "Expected rejection '$Message'; got '$failure'." }
}
Save-Config
& $builder -Repository $fixture -Tag v1.2.3
$zip = Join-Path $fixture 'artifacts/BuildFixture-1.2.3.zip'
$sum = Get-Content -LiteralPath (Join-Path $fixture 'artifacts/BuildFixture-1.2.3.sha256')
if ($sum -notlike ((Get-FileHash -LiteralPath $zip).Hash.ToLowerInvariant() + '*')) { throw 'Release checksum is incorrect.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $names = @($archive.Entries | Where-Object Name | ForEach-Object FullName | Sort-Object)
    if (($names -join ',') -ne 'LimbusRead.cs,Probe.ps1,README.md') { throw 'Package contains unexpected files.' }
} finally { $archive.Dispose() }
Reject { & $builder -Repository $fixture -Tag v9.9.9 } 'does not match'
New-Item -ItemType Directory -Path (Join-Path $fixture 'research') | Out-Null
Set-Content -LiteralPath (Join-Path $fixture 'research/private.txt') -Value 'must not ship'
$config.packageFiles = @('research/private.txt'); Save-Config
Reject { & $builder -Repository $fixture } 'Private path cannot be packaged'
$config.packageFiles = @('../outside.txt'); Save-Config
Reject { & $builder -Repository $fixture } 'Path leaves repository'
$config.packageFiles = @('Probe.ps1'); Save-Config
Set-Content -LiteralPath (Join-Path $fixture 'Probe.ps1') -Value 'function Invalid {'
Reject { & $builder -Repository $fixture } 'Missing closing'
Write-Host 'PASS: release packaging, checksums, version-tag matching, private paths, path traversal and PowerShell validation.'
Write-Host "Isolated test artifacts: $fixture"
