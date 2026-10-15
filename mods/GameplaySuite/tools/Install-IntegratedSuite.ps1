# Canonical MEN-006 workflow adapted to the independently versioned Jiangyu packages (#34).
# Bundled source is verified, copied to a fresh work directory, rebuilt, staged, then installed.
[CmdletBinding()]
param(
    [string]$BundleRoot,
    [string]$GamePath = $env:MENACE_GAME,
    [ValidateSet('Suite', 'Camera', 'Standalone')][string]$Product = 'Suite',
    [string[]]$Modules = @(),
    [switch]$BuildOnly,
    [string]$RollbackPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$log = Join-Path ([IO.Path]::GetTempPath()) 'MENACE-install.log'
$lock = $null
$transcript = $false
$buildLocation = $false
$processEnvironment = $null
try {
    Start-Transcript -LiteralPath $log -Append -ErrorAction Stop | Out-Null
    $transcript = $true
    Write-Host "Installer log: $log"
    if (-not $BundleRoot) { throw 'Run INSTALL-SUITE.cmd from the verified bundled-source BuildKit.' }
    $BundleRoot = (Resolve-Path -LiteralPath $BundleRoot).Path
    $kit = Get-Content -LiteralPath (Join-Path $BundleRoot 'kit.json') -Raw | ConvertFrom-Json
    # Verify helpers before dot-sourcing them; no source/helper executes before this check.
    $scriptRoot = Join-Path $BundleRoot 'source\mods\GameplaySuite\tools'
    if ([IO.Path]::GetFullPath($PSScriptRoot) -ne [IO.Path]::GetFullPath($scriptRoot)) { throw 'Execute the bundled installer, not an external copy.' }
    foreach ($name in @('Install-IntegratedSuite.ps1', 'Install.Common.ps1', 'Install.Deployment.ps1')) {
        $relative = 'source/mods/GameplaySuite/tools/' + $name
        $entries = @($kit.files | Where-Object { $_.path -eq $relative })
        if ($entries.Count -ne 1) { throw "Missing/duplicate helper hash: $name" }
        $path = Join-Path $scriptRoot $name
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entries[0].sha256) { throw "Helper integrity failed: $path" }
    }
    . (Join-Path $PSScriptRoot 'Install.Common.ps1')
    . (Join-Path $PSScriptRoot 'Install.Deployment.ps1')
    $processEnvironment = Save-BuildEnvironment
    Assert-Bundle $BundleRoot $kit
    $catalog = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'distribution.json') -Raw | ConvertFrom-Json
    if (-not $GamePath) {
        $GamePath = @('M:\SteamLibrary\steamapps\common\Menace', 'D:\SteamLibrary\steamapps\common\Menace', 'C:\Program Files (x86)\Steam\steamapps\common\Menace') |
            Where-Object { Test-Path -LiteralPath (Join-Path $_ 'Menace.exe') } | Select-Object -First 1
    }
    if (-not $GamePath) { $GamePath = (Read-Host 'MENACE directory containing Menace.exe').Trim('"') }
    $GamePath = (Resolve-Path -LiteralPath $GamePath).Path
    Assert-NotLink $GamePath
    Assert-Leaf (Join-Path $GamePath 'Menace.exe')
    Assert-GameClosed
    $lock = [IO.File]::Open((Join-Path $GamePath '_MENACE-install.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    if ($RollbackPath) { Restore-ModBackup $GamePath $RollbackPath -CheckCurrent; return }
    Assert-Hash (Join-Path $GamePath 'GameAssembly.dll') $catalog.gameAssemblySha256
    $refs = Join-Path $GamePath 'MelonLoader\Il2CppAssemblies'
    $runtime = Join-Path $GamePath 'MelonLoader\net6'
    foreach ($name in @('Assembly-CSharp.dll', 'Il2Cppmscorlib.dll', 'UnityEngine.CoreModule.dll')) { Assert-Leaf (Join-Path $refs $name) }
    foreach ($name in @('MelonLoader.dll', 'Il2CppInterop.Runtime.dll', '0Harmony.dll')) { Assert-Leaf (Join-Path $runtime $name) }
    foreach ($property in $catalog.dotnetSdk.PSObject.Properties) {
        if ($kit.dotnetSdk.($property.Name) -ne $property.Value) { throw 'Kit/catalog SDK pin mismatch.' }
    }
    # First build step: automatic, private SDK bootstrap. No preinstalled SDK required.
    Initialize-PrivateDotnet $BundleRoot $kit.dotnetSdk
    $selected = @($catalog.modules)
    if ($Product -eq 'Camera') { $selected = @('MenaceModSettings', 'CinematicActionCamera') }
    if ($Product -eq 'Standalone') {
        if (-not $Modules.Count) { throw 'Standalone requires -Modules.' }
        foreach ($name in $Modules) { if ($catalog.modules -notcontains $name) { throw "Unknown module: $name" } }
        $selected = @('MenaceModSettings') + @($Modules | Where-Object { $_ -ne 'MenaceModSettings' } | Select-Object -Unique)
    }
    if ($Product -ne 'Suite' -and (Test-Path -LiteralPath (Join-Path $GamePath 'Mods\MenaceGameplaySuite'))) {
        throw 'Legacy monolithic Suite detected: use the Suite product for an atomic cutover, not a partial duplicate.'
    }
    $work = Join-Path ([IO.Path]::GetTempPath()) ('MENACE-Build-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work | Out-Null
    $source = Join-Path $work 'source'
    Copy-Item -LiteralPath (Join-Path $BundleRoot 'source') -Destination $source -Recurse
    Push-Location $source
    $buildLocation = $true
    $archive = Join-Path $work 'jiangyu-cli-win-x64.zip'
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri $catalog.cliUrl -OutFile $archive -TimeoutSec 180
    Assert-Hash $archive $catalog.cliSha256
    $toolchain = Join-Path $work 'toolchain'
    Expand-Archive -LiteralPath $archive -DestinationPath $toolchain
    $sdk = Join-Path $toolchain 'sdk'
    Assert-Leaf (Join-Path $sdk 'Jiangyu.Sdk.dll')
    $loader = Join-Path $toolchain 'loader\user\Jiangyu.Loader.dll'
    Assert-Hash $loader $catalog.loaderSha256
    $stage = Join-Path $work 'stage'
    New-Item -ItemType Directory -Path $stage | Out-Null
    foreach ($name in $selected) {
        $root = Join-Path (Join-Path $source 'mods\jiangyu') $name
        $manifest = Get-Content -LiteralPath (Join-Path $root 'jiangyu.json') -Raw | ConvertFrom-Json
        $compiled = Join-Path $root 'compiled'
        $builtManifest = Get-Content -LiteralPath (Join-Path $compiled 'jiangyu.json') -Raw | ConvertFrom-Json
        if ($manifest.version -ne $builtManifest.version -or $manifest.name -ne $name) { throw "Manifest mismatch: $name" }
        $project = Join-Path $root ('code\' + $name + '.csproj')
        if (Test-Path -LiteralPath $project) {
            [xml]$xml = Get-Content -LiteralPath $project -Raw
            $versionNode = $xml.SelectSingleNode('/Project/PropertyGroup/Version')
            if ($null -eq $versionNode) { throw "Missing project Version: $name" }
            $actual = $versionNode.InnerText.Trim()
            if ($actual -ne $manifest.version) { throw "Project/manifest mismatch: $name" }
            Invoke-Dotnet @('build', $project, '-c', 'Release', '--nologo', "-p:GameDir=$GamePath", "-p:GameAssembliesDir=$refs", "-p:MelonLoaderDir=$runtime", "-p:JiangyuSdkDir=$sdk", '-p:JiangyuDev=false')
            $assemblyNode = $xml.SelectSingleNode('/Project/PropertyGroup/AssemblyName')
            $assembly = $name
            if ($assemblyNode) { $assembly = $assemblyNode.InnerText.Trim() }
            $dll = Join-Path $root ("code\bin\Release\$assembly.dll")
            $identity = ''
            if ($name -eq 'MenaceModSettings') { $identity = '1.0.0.0' }
            Assert-Dll $dll $manifest.version $identity
            Copy-Item -LiteralPath $dll -Destination (Join-Path $compiled "code\$assembly.dll") -Force
        }
        Copy-Item -LiteralPath $compiled -Destination (Join-Path $stage $name) -Recurse
        Write-Host "Staged $name $($manifest.version)"
    }
    $names = @($selected)
    if ($selected -contains 'CinematicActionCamera') {
        $camera = Join-Path $source 'mods\ThirdPersonCamera'
        Invoke-Dotnet @('build', (Join-Path $camera 'ThirdPersonCamera.csproj'), '-c', 'Release', '--nologo', "-p:MenaceInteropDir=$refs", "-p:MenaceRuntimeDir=$runtime", '-p:DebugType=none', '-p:DebugSymbols=false')
        $dll = Join-Path $camera 'bin\Release\MenaceThirdPersonCamera.dll'
        $cameraVersion = (Get-Content -LiteralPath (Join-Path $stage 'CinematicActionCamera\jiangyu.json') -Raw | ConvertFrom-Json).version
        Assert-Dll $dll $cameraVersion
        Copy-Item -LiteralPath $dll -Destination (Join-Path $stage 'MenaceThirdPersonCamera.dll')
        $names += 'MenaceThirdPersonCamera.dll'
    }
    Copy-Item -LiteralPath $loader -Destination (Join-Path $stage 'Jiangyu.Loader.dll')
    $names += 'Jiangyu.Loader.dll'
    $conflicts = @()
    foreach ($name in $selected) {
        $conflicts += "$name.dll"
        if ($catalog.legacyAliases.PSObject.Properties.Name -contains $name) { $conflicts += @($catalog.legacyAliases.$name) }
    }
    if ($Product -eq 'Suite') { $conflicts += 'MenaceGameplaySuite' }
    # Preserve the legacy override at the path the existing runtime migration already reads.
    # Do not write UserData here, and do not overwrite an existing modern override.
    if ($selected -contains 'WeaponRangesRebalanced') {
        $legacyOverride = Join-Path $GamePath 'Mods\WeaponRangesRebalanced\weapon-overrides.json'
        if (Test-Path -LiteralPath $legacyOverride -PathType Leaf) {
            Assert-NotLink $legacyOverride
            Copy-Item -LiteralPath $legacyOverride -Destination (Join-Path $stage 'WeaponRangesRebalanced\weapon-overrides.json') -Force
        }
    }
    Assert-NoUnknownDuplicates (Join-Path $GamePath 'Mods') $stage $names $conflicts
    # Validate staged trees including symlinks before the first live move.
    foreach ($name in $names) { $null = @(Get-TreeInventory (Join-Path $stage $name)) }
    Write-Host "All builds/versions passed. Staged distribution: $stage"
    if ($BuildOnly) { Write-Host 'BUILD-ONLY: no live mod changed.'; return }
    Install-StagedMods $GamePath $stage $names $conflicts
    Write-Host 'SUCCESS: files installed and verified. In-game acceptance remains pending.'
}
catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Full diagnostic log: $log"
    if (Test-Path -LiteralPath $log) { Start-Process notepad.exe -ArgumentList @($log) -ErrorAction SilentlyContinue }
    exit 1
}
finally {
    if ($buildLocation) { Pop-Location }
    if ($null -ne $processEnvironment) { Restore-BuildEnvironment $processEnvironment }
    if ($lock) { $lock.Dispose() }
    if ($transcript) { Stop-Transcript | Out-Null }
}