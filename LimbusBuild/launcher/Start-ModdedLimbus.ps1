param(
    [string]$GameDir = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [switch]$Initialize,
    [switch]$CheckOnly,
    [switch]$NoLaunch
)
$ErrorActionPreference = 'Stop'
$GameDir = [IO.Path]::GetFullPath($GameDir)
$stateDir = Join-Path $PSScriptRoot 'state'
$coreDir = Join-Path $GameDir 'BepInEx/core'
$statePath = Join-Path $stateDir 'baseline.json'
$names = @('Il2CppInterop.Runtime.dll', 'BepInEx.Unity.IL2CPP.dll')
$launchLock = $null
$transcriptStarted = $false

function Hash([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Game-Running {
    $expected = [IO.Path]::GetFullPath((Join-Path $GameDir 'LimbusCompany.exe'))
    foreach ($process in @(Get-Process LimbusCompany -ErrorAction SilentlyContinue)) {
        # An inaccessible process path is treated as running, conservatively.
        try { if (!$process.Path -or [IO.Path]::GetFullPath($process.Path) -eq $expected) { return $true } }
        catch { return $true }
    }
    return $false
}
function Save-State($State) {
    $State | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath "$statePath.tmp" -Encoding UTF8
    Move-Item -LiteralPath "$statePath.tmp" -Destination $statePath -Force
}
function Read-Imports([string]$Path) {
    $a = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Path)
    try {
        $result = @{}
        foreach ($m in $a.MainModule.GetType('Il2CppInterop.Runtime.IL2CPP').Methods) {
            if ($m.PInvokeInfo) { $result[$m.Name] = $m.PInvokeInfo.EntryPoint }
        }
        return $result
    } finally { $a.Dispose() }
}
function Run-Guard([string]$Action, [string]$Output, [string]$ImportsPath) {
    & $script:python (Join-Path $PSScriptRoot 'export_guard.py') $Action --game $GameDir --imports $ImportsPath --state $statePath --output $Output --build $script:buildId --engine-version $script:engineVersion
    if ($LASTEXITCODE -ne 0) { throw 'Compatibility validation failed. No automatic launch will occur; see the log above.' }
}
function Patch-Staged([string]$Stage, $Map, $OldImports) {
    $runtime = Join-Path $Stage $names[0]
    $rp = New-Object Mono.Cecil.ReaderParameters
    $rp.InMemory = $true
    $a = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($runtime, $rp)
    try {
        foreach ($m in $a.MainModule.GetType('Il2CppInterop.Runtime.IL2CPP').Methods) {
            $p = $Map.PSObject.Properties[$m.Name]
            if ($m.PInvokeInfo -and $p) { $m.PInvokeInfo.EntryPoint = [string]$p.Value }
        }
        $a.Write($runtime)
    } finally { $a.Dispose() }
    $chainloader = Join-Path $Stage $names[1]
    $a = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($chainloader, $rp)
    try {
        foreach ($type in $a.MainModule.Types) {
            if ($type.FullName -ne 'BepInEx.Unity.IL2CPP.IL2CPPChainloader') { continue }
            foreach ($method in $type.Methods) {
                if ($method.Name -ne 'Initialize' -or !$method.HasBody) { continue }
                foreach ($ins in $method.Body.Instructions) {
                    if ($ins.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldstr -and
                        ([string]$ins.Operand -eq $OldImports['il2cpp_runtime_invoke'] -or [string]$ins.Operand -eq 'il2cpp_runtime_invoke')) {
                        $ins.Operand = [string]$Map.il2cpp_runtime_invoke
                    }
                }
            }
        }
        $a.Write($chainloader)
    } finally { $a.Dispose() }
    $verified = Read-Imports $runtime
    foreach ($p in $Map.PSObject.Properties) {
        if ($verified[$p.Name] -ne [string]$p.Value) { throw ('Staged import validation failed: ' + $p.Name) }
    }
}

try {
    New-Item -ItemType Directory -Path $stateDir, (Join-Path $PSScriptRoot 'logs') -Force | Out-Null
    $launchLock = [IO.File]::Open((Join-Path $stateDir 'launcher.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss-fff')
    Start-Transcript -Path (Join-Path $PSScriptRoot "logs/$stamp.log") | Out-Null
    $transcriptStarted = $true
    foreach ($path in @((Join-Path $GameDir 'GameAssembly.dll'), (Join-Path $GameDir 'UnityPlayer.dll'), (Join-Path $GameDir 'LimbusCompany.exe'))) {
        if (!(Test-Path -LiteralPath $path)) { throw "Required game file missing: $path" }
    }
    $manifest = Join-Path (Split-Path -Parent (Split-Path -Parent $GameDir)) 'appmanifest_1973530.acf'
    if (!(Test-Path -LiteralPath $manifest)) { throw 'Steam app manifest not found. Check GameDir.' }
    $app = Get-Content -LiteralPath $manifest -Raw
    $script:buildId = [regex]::Match($app, '"buildid"\s+"(\d+)"').Groups[1].Value
    $flags = [regex]::Match($app, '"StateFlags"\s+"(\d+)"').Groups[1].Value
    if ($flags -ne '4') { throw 'Steam has an update/install pending. Finish it in Steam, then run this launcher again.' }
    if (!$script:buildId) { throw 'Steam build ID is missing.' }
    $script:engineVersion = (Get-Item -LiteralPath (Join-Path $GameDir 'UnityPlayer.dll')).VersionInfo.ProductVersion
    $script:python = (Get-Command python -ErrorAction Stop).Source
    Add-Type -Path (Join-Path $coreDir 'Mono.Cecil.dll')
    $imports = Read-Imports (Join-Path $coreDir $names[0])
    $importsPath = Join-Path $stateDir 'current-imports.json'
    $imports | ConvertTo-Json | Set-Content -LiteralPath $importsPath -Encoding UTF8
    if ($Initialize) {
        if (Test-Path -LiteralPath $statePath) { throw 'A baseline already exists. Review it before replacing it.' }
        $initial = Join-Path $stateDir 'initial.json'
        Run-Guard 'initialize' $initial $importsPath
        $state = Get-Content -LiteralPath $initial -Raw | ConvertFrom-Json
        $state | Add-Member -NotePropertyName recordedUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('o'))
        Save-State $state
        Write-Host "Recorded installed Steam build $script:buildId and compatible export bindings."
        return
    }
    if (!(Test-Path -LiteralPath $statePath)) { throw 'No validated baseline. Initialize this installation after verifying its loader compatibility.' }
    $planPath = Join-Path $stateDir 'plan.json'
    Run-Guard 'plan' $planPath $importsPath
    $plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
    Write-Host "Steam build $script:buildId; export check: $($plan.status)."
    if ($plan.engineMetadataChanged) { Write-Host 'Unity version and executable sections match; accepting non-code engine-file changes.' }
    if (@($plan.acceptedAttributeChanges).Count) { Write-Host ('Recognized synchronized custom-attribute wrapper update: ' + ($plan.acceptedAttributeChanges -join ', ')) }
    if ($CheckOnly) { return }
    if (Game-Running) { throw 'Limbus Company is already running. This launcher will not close it or replace locked loader files.' }
    if ($plan.status -eq 'repair') {
        $backup = Join-Path $PSScriptRoot "backups/$stamp"
        $stage = Join-Path $stateDir "stage-$stamp"
        New-Item -ItemType Directory -Path $backup, $stage -Force | Out-Null
        Copy-Item -LiteralPath $statePath -Destination (Join-Path $backup 'baseline.json')
        foreach ($name in $names) {
            Copy-Item -LiteralPath (Join-Path $coreDir $name) -Destination (Join-Path $backup $name)
            Copy-Item -LiteralPath (Join-Path $coreDir $name) -Destination (Join-Path $stage $name)
        }
        Patch-Staged $stage $plan.mapping $imports
        if ((Hash (Join-Path $GameDir 'UnityPlayer.dll')) -ne $plan.unityPlayerSha256) { throw 'UnityPlayer changed during validation.' }
        # Refuse a concurrent Steam update or an external loader change.
        if ((Hash (Join-Path $GameDir 'GameAssembly.dll')) -ne $plan.gameAssemblySha256) { throw 'Game changed during validation. Rerun after Steam finishes.' }
        foreach ($name in $names) {
            if ((Hash (Join-Path $coreDir $name)) -ne [string]$plan.coreHashes.PSObject.Properties[$name].Value) { throw 'Loader changed during validation.' }
        }
        try {
            foreach ($name in $names) { Copy-Item -LiteralPath (Join-Path $stage $name) -Destination (Join-Path $coreDir $name) -Force }
            $updated = Read-Imports (Join-Path $coreDir $names[0])
            $updated | ConvertTo-Json | Set-Content -LiteralPath $importsPath -Encoding UTF8
            $newPath = Join-Path $stateDir 'updated.json'
            Run-Guard 'initialize' $newPath $importsPath
            $state = Get-Content -LiteralPath $newPath -Raw | ConvertFrom-Json
            if ($state.gameAssemblySha256 -ne $plan.gameAssemblySha256 -or $state.unityPlayerSha256 -ne $plan.unityPlayerSha256) { throw 'Game changed while installing repair.' }
            $state | Add-Member -NotePropertyName recordedUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('o'))
            Save-State $state
        } catch {
            $repairFailure = $_
            $rollbackErrors = @()
            foreach ($name in $names) {
                try {
                    $original = Join-Path $backup $name
                    $live = Join-Path $coreDir $name
                    # The second file may have been locked before it changed.
                    # Do not attempt to overwrite an already-original locked file.
                    if (!(Test-Path -LiteralPath $live) -or (Hash $live) -ne (Hash $original)) {
                        Copy-Item -LiteralPath $original -Destination $live -Force
                    }
                } catch { $rollbackErrors += ($name + ': ' + $_.Exception.Message) }
            }
            if ($rollbackErrors.Count) { throw ("Repair failed; restore from $backup. Rollback errors: " + ($rollbackErrors -join '; ')) }
            Copy-Item -LiteralPath (Join-Path $backup 'baseline.json') -Destination $statePath -Force
            throw $repairFailure
        }
        Write-Host "Updated $(@($plan.mapping.PSObject.Properties).Count) bindings; originals saved to $backup."
    } else {
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        $state.steamBuildId = $script:buildId
        Save-State $state
    }
    if ($NoLaunch) { return }
    if (!(Get-Process steam -ErrorAction SilentlyContinue)) { throw 'Open and log into Steam first, then rerun the launcher.' }
    # Launch the checked installed executable rather than asking Steam to update
    # and launch a potentially different binary after validation.
    if ((Hash (Join-Path $GameDir 'GameAssembly.dll')) -ne $plan.gameAssemblySha256 -or (Hash (Join-Path $GameDir 'UnityPlayer.dll')) -ne $plan.unityPlayerSha256) { throw 'Game changed before launch. Rerun the check.' }
    $started = [DateTime]::UtcNow
    $oldAppId = $env:SteamAppId
    $oldGameId = $env:SteamGameId
    $oldDoorstopDisable = $env:DOORSTOP_DISABLE
    try {
        $env:SteamAppId = '1973530'
        $env:SteamGameId = '1973530'
        $env:DOORSTOP_DISABLE = $null
        # Opt in for this process only. Keep doorstop_config.ini disabled so
        # Steam/direct launches never depend on this launcher cleaning up.
        Start-Process -FilePath (Join-Path $GameDir 'LimbusCompany.exe') -ArgumentList '--doorstop-enabled', 'true', '--limbus-modded-bootstrap=1' -WorkingDirectory $GameDir
    } finally {
        $env:SteamAppId = $oldAppId
        $env:SteamGameId = $oldGameId
        $env:DOORSTOP_DISABLE = $oldDoorstopDisable
    }
    $log = Join-Path $GameDir 'BepInEx/LogOutput.log'
    for ($i = 0; $i -lt 90; $i++) {
        Start-Sleep -Seconds 1
        if (!(Test-Path -LiteralPath $log) -or (Get-Item -LiteralPath $log).LastWriteTimeUtc -lt $started) { continue }
        $text = Get-Content -LiteralPath $log -Raw
        if ($text -match '\[Fatal|EntryPointNotFoundException') { throw "Loader startup failed. See $log. The game has not been forcibly closed." }
        if ($text -match 'Chainloader startup complete') {
            $state.startupVerified = $true
            $state | Add-Member -NotePropertyName startupVerifiedUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('o')) -Force
            Save-State $state
            Write-Host 'Game started; mod loader initialization verified.'
            return
        }
    }
    throw 'Startup was not confirmed within 90 seconds. The game has been left as-is; inspect BepInEx/LogOutput.log.'
} catch {
    Write-Host ('LAUNCH STOPPED: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
} finally {
    if ($transcriptStarted) { Stop-Transcript | Out-Null }
    if ($launchLock) { $launchLock.Dispose() }
}
