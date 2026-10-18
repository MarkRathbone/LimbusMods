[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Repository,
    [string]$CoreDir,
    [string]$Tag
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Repository = (Resolve-Path -LiteralPath $Repository).Path
$config = Get-Content -LiteralPath (Join-Path $Repository 'release.json') -Raw | ConvertFrom-Json
if ($config.name -notmatch '^[A-Za-z][A-Za-z0-9]+$') { throw 'Invalid release name.' }
$name = $config.name

function Checked-Path([string]$Relative) {
    if ([IO.Path]::IsPathRooted($Relative)) { throw "Expected repository-relative path: $Relative" }
    $full = [IO.Path]::GetFullPath((Join-Path $Repository $Relative))
    if (!$full.StartsWith($Repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Path leaves repository: $Relative" }
    if ($Relative -match '(^|[\/])(research|deps|outputs|game-stage|backups)([\/]|$)') { throw "Private path cannot be packaged: $Relative" }
    return $full
}

function Invoke-DotNet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $($Arguments -join ' ')" }
}

if ($config.kind -eq 'plugin') {
    $project = Checked-Path $config.project
    [xml]$projectXml = Get-Content -LiteralPath $project -Raw
    $version = [string]$projectXml.Project.PropertyGroup.Version
    if (!$CoreDir) { $CoreDir = & (Join-Path $PSScriptRoot 'Get-BuildDependencies.ps1') -Repository $Repository }
    $CoreDir = (Resolve-Path -LiteralPath $CoreDir).Path
    foreach ($assembly in 'BepInEx.Core.dll','BepInEx.Unity.IL2CPP.dll','Il2CppInterop.Runtime.dll','Il2CppInterop.Common.dll','MonoMod.RuntimeDetour.dll') {
        if (!(Test-Path -LiteralPath (Join-Path $CoreDir $assembly))) { throw "Missing build dependency: $assembly" }
    }
} elseif ($config.kind -in @('powershell','source')) {
    $version = [string]$config.version
} else { throw "Unsupported release kind: $($config.kind)" }
if ($version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$') { throw "Invalid project version: $version" }
if ($Tag -and $Tag -cne "v$version") { throw "Tag $Tag does not match project version v$version." }

if ($config.kind -eq 'plugin') {
    Invoke-DotNet -Arguments @('build',$project,'--configuration','Release',"-p:CoreDir=$CoreDir")
    if ($config.PSObject.Properties.Name -contains 'tests') {
        Invoke-DotNet -Arguments @('run','--project',(Checked-Path $config.tests),'--configuration','Release')
    }
    $plugin = Join-Path (Split-Path -Parent $project) "bin/Release/net6.0/$name.dll"
    if (!(Test-Path -LiteralPath $plugin)) { throw "Plugin output missing: $plugin" }
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($plugin).Version
    if ($assemblyVersion.ToString(3) -ne ($version -split '-')[0]) { throw 'Compiled assembly version differs from project version.' }
}

foreach ($relative in @($config.packageFiles)) {
    $file = Checked-Path $relative
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Package file missing: $relative" }
    if ([IO.Path]::GetExtension($file) -eq '.ps1') {
        $tokens = $null; $parseErrors = $null
        [System.Management.Automation.Language.Parser]::ParseFile($file,[ref]$tokens,[ref]$parseErrors) | Out-Null
        if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
    }
}
if ($config.kind -eq 'source') { & (Join-Path $PSScriptRoot 'Test-Build.ps1') }
if ($config.kind -eq 'powershell') {
    Add-Type -Path (Checked-Path 'LimbusRead.cs')
}

$artifacts = Join-Path $Repository 'artifacts'
$package = Join-Path $artifacts ("package-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $package -Force | Out-Null
if ($config.kind -eq 'plugin') {
    $pluginDirectory = Join-Path $package "BepInEx/plugins/$name"
    New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null
    Copy-Item -LiteralPath $plugin -Destination (Join-Path $pluginDirectory "$name.dll")
}
foreach ($relative in @($config.packageFiles)) {
    $destination = Join-Path $package $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Checked-Path $relative) -Destination $destination
}
$zip = Join-Path $artifacts "$name-$version.zip"
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -Force
$checksum = Join-Path $artifacts "$name-$version.sha256"
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($checksum,"$hash  $([IO.Path]::GetFileName($zip))`n",[Text.UTF8Encoding]::new($false))
if ($env:GITHUB_OUTPUT) {
    Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "version=$version"
    Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "name=$name"
}
Write-Host "Built $name $version. Release assets: $zip and $checksum"

