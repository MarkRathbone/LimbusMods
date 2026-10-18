param([string]$CoreDir)
$ErrorActionPreference = 'Stop'
$builder = Join-Path $PSScriptRoot 'LimbusBuild/scripts/Build-Release.ps1'
foreach ($repo in 'LimbusUltrawide','LimbusAutodialogue','LimbusUnnerfed','LimbusCollectionExporter') {
    & $builder -Repository (Join-Path $PSScriptRoot $repo) -CoreDir $CoreDir
}
