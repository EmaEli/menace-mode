# MENACE reusable single-component installer; MEN-001 profile.
# Adapted from the MEN-006 one-click integrated launcher/build/deploy workflow.
# The source revision is pinned by the bootstrap launcher and verified here.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$SourceCommit,
    [ValidateSet('SmokeOverhaul')]
    [string]$Component = 'SmokeOverhaul',
    [string]$ExpectedVersion = '0.6.3',
    [string]$GamePath = '',
    [switch]$BuildOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$Log = Join-Path ([IO.Path]::GetTempPath()) 'MENACE-MEN001-install.log'
$StartedTranscript = $false
$SourceCommit = $SourceCommit.ToLowerInvariant()

function Assert-File([string]$Path, [string]$Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description not found: $Path"
    }
}
function Assert-Directory([string]$Path, [string]$Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Description not found: $Path"
    }
}
function Read-ManifestVersion([string]$Path) {
    Assert-File $Path 'Modpack manifest'
    $manifest = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace([string]$manifest.version)) {
        throw "Manifest version is missing: $Path"
    }
    return [string]$manifest.version
}
function Assert-ManifestVersion([string]$Path, [string]$Version) {
    $actual = Read-ManifestVersion $Path
    if ($actual -ne $Version) {
        throw "Manifest version mismatch: $actual; expected $Version ($Path)"
    }
    Write-Host "Manifest version verified: $actual"
}
function Assert-DllVersion([string]$Path, [string]$Version) {
    Assert-File $Path 'Build DLL'
    $actual = [Reflection.AssemblyName]::GetAssemblyName($Path).Version.ToString()
    if ($actual -ne $Version) {
        throw "DLL assembly version mismatch: $actual; expected $Version ($Path)"
    }
    Write-Host "Assembly version verified: $actual"
}
function Resolve-Game([string]$Requested) {
    $candidates = @(
        $Requested,
        'M:\SteamLibrary\steamapps\common\Menace',
        'D:\SteamLibrary\steamapps\common\Menace',
        'C:\Program Files (x86)\Steam\steamapps\common\Menace',
        'C:\Program Files\Steam\steamapps\common\Menace'
    )
    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        if (Test-Path -LiteralPath (Join-Path $candidate 'Menace.exe') -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    $typed = (Read-Host 'Enter your MENACE folder containing Menace.exe').Trim('"')
    Assert-File (Join-Path $typed 'Menace.exe') 'MENACE executable'
    return (Resolve-Path -LiteralPath $typed).Path
}
function Assert-CorrectSource([string]$RepoPath, [string]$Version) {
    $suite = Join-Path $RepoPath 'mods\GameplaySuite'
    Assert-ManifestVersion (Join-Path $suite 'packaging\standalone\SmokeOverhaul\modpack.json') $Version
    $project = Join-Path $suite 'src\SmokeOverhaul\SmokeOverhaul.csproj'
    $plugin = Join-Path $suite 'src\SmokeOverhaul\Plugin.cs'
    $builder = Join-Path $suite 'tools\Build.ps1'
    Assert-File $project 'Smoke Overhaul project'
    Assert-File $plugin 'Smoke Overhaul source'
    Assert-File $builder 'Shared build script'
    # Read the .csproj as XML and compare to the requested *manifest* version.
    # NEVER hard-code an older release's <Version> into this source check.
    [xml]$projectXml = Get-Content -LiteralPath $project -Raw
    $projectVersionNode = $projectXml.SelectSingleNode('/Project/PropertyGroup/Version')
    if ($null -eq $projectVersionNode) {
        throw "Smoke Overhaul project XML has no /Project/PropertyGroup/Version: $project"
    }
    $projectVersion = $projectVersionNode.InnerText.Trim()
    if ($projectVersion -ne $Version) {
        throw "Smoke Overhaul project version mismatch: project=$projectVersion, manifest/expected=$Version ($project)"
    }
    Write-Host "Smoke Overhaul project version verified: $projectVersion"

    # Keep source/assembly checks coupled to the *same* expected version.
    $expectedCodeVersion = 'internal const string Version = "' + $Version + '";'
    $expectedAssemblyAttribute = 'AssemblyVersion("' + $Version + '.0")'
    $code = Get-Content -LiteralPath $plugin -Raw
    if (-not $code.Contains($expectedCodeVersion) -or
        -not $code.Contains($expectedAssemblyAttribute) -or
        -not $code.Contains('AllowSmokeWhilePinnedDown') -or
        -not $code.Contains('SimpleAISmokeRules')) {
        throw "MEN-001 expected source version/assembly metadata or smoke implementation is missing ($Version)."
    }
    if ((Get-Content -LiteralPath $builder -Raw) -notmatch '\[string\]\$Only') {
        throw 'Shared builder does not support safe single-component builds.'
    }
    return $suite
}

try {
    Start-Transcript -LiteralPath $Log -Append -ErrorAction Stop | Out-Null
    $StartedTranscript = $true
    $ExpectedAssemblyVersion = $ExpectedVersion + '.0'
    Write-Host ''
    Write-Host 'MEN-001: SMOKE OVERHAUL 0.6.3 - ONE-CLICK INSTALLER' -ForegroundColor Cyan
    Write-Host "Persistent installer log: $Log"
    Write-Host "Pinned source commit: $SourceCommit"
    Write-Host 'Only Smoke Overhaul will be replaced; other Gameplay Suite DLLs and camera will be kept.'
    Write-Host ''

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw '.NET SDK was not found. Install the .NET 6 or later SDK, then run the same installer again.'
    }
    $sdkOutput = (& dotnet --list-sdks 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0 -or -not ($sdkOutput -match '(?m)^([6-9]|[1-9][0-9])\.')) {
        throw "A .NET SDK >= 6 is required. Detected: $sdkOutput"
    }
    Write-Host "Available .NET SDK(s): $sdkOutput"
    $GamePath = Resolve-Game $GamePath
    $Mods = Join-Path $GamePath 'Mods'
    Assert-Directory $Mods 'MENACE Mods directory'
    Assert-File (Join-Path $Mods 'Menace.ModpackLoader.dll') 'ModpackLoader'
    Assert-File (Join-Path $GamePath 'MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll') 'IL2CPP references'
    Assert-File (Join-Path $GamePath 'MelonLoader\net6\MelonLoader.dll') 'MelonLoader runtime'
    Assert-File (Join-Path $GamePath 'MelonLoader\net6\0Harmony.dll') 'Harmony runtime'
    if (-not $BuildOnly -and (Get-Process -Name 'Menace' -ErrorAction SilentlyContinue)) {
        throw 'MENACE is running. Close the game before installing and rerun this installer.'
    }
    Write-Host "MENACE found: $GamePath"

    $Work = Join-Path ([IO.Path]::GetTempPath()) ('MENACE-MEN001-' + [guid]::NewGuid().ToString('N'))
    $Archive = Join-Path $Work 'source.zip'
    $Extract = Join-Path $Work 'source'
    New-Item -ItemType Directory -Path $Extract -Force | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Write-Host '1/4 Verifying GitHub source commit and downloading exact source...' -ForegroundColor Cyan
    $commitApi = 'https://api.github.com/repos/EmaEli/menace-mode/commits/' + $SourceCommit
    $commitInfo = Invoke-RestMethod -Uri $commitApi -Headers @{ 'User-Agent' = 'MENACE-MEN001-Installer' } -TimeoutSec 90
    if ($commitInfo.sha -ne $SourceCommit) { throw 'Published GitHub source commit does not match pinned SHA.' }
    $sourceUrl = 'https://codeload.github.com/EmaEli/menace-mode/zip/' + $SourceCommit
    Invoke-WebRequest -UseBasicParsing -Uri $sourceUrl -OutFile $Archive -TimeoutSec 180
    if ((Get-Item -LiteralPath $Archive).Length -lt 20000) { throw 'Downloaded source archive is unexpectedly small.' }
    $sourceHash = (Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash
    Write-Host "Source ZIP SHA256: $sourceHash"
    Expand-Archive -LiteralPath $Archive -DestinationPath $Extract -Force
    $roots = @(Get-ChildItem -LiteralPath $Extract -Directory)
    if ($roots.Count -ne 1 -or $roots[0].Name -ne ('menace-mode-' + $SourceCommit)) {
        throw 'Downloaded archive root does not match the pinned GitHub commit.'
    }
    $SuiteRoot = Assert-CorrectSource $roots[0].FullName $ExpectedVersion

    Write-Host '2/4 Building only the Smoke Overhaul DLL (no deployment)...' -ForegroundColor Cyan
    $builder = Join-Path $SuiteRoot 'tools\Build.ps1'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $builder -Mode Standalone -Only $Component -GamePath $GamePath 2>&1 |
        ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { throw "Smoke Overhaul build failed (exit $LASTEXITCODE). The game was not modified." }
    $Built = Join-Path $SuiteRoot 'artifacts\stage\SmokeOverhaul'
    $BuiltDll = Join-Path $Built 'dlls\SmokeOverhaul.dll'
    $BuiltSource = Join-Path $Built 'source\src\SmokeOverhaul'
    Assert-ManifestVersion (Join-Path $Built 'modpack.json') $ExpectedVersion
    Assert-DllVersion $BuiltDll $ExpectedAssemblyVersion
    Assert-Directory $BuiltSource 'Built Smoke Overhaul authoring source'
    if ((Get-FileHash -LiteralPath $BuiltDll -Algorithm SHA256).Hash -eq '') {
        throw 'Compiled DLL hash is unexpectedly empty.'
    }

    $Stage = Join-Path $Work 'staged'
    New-Item -ItemType Directory -Path $Stage -Force | Out-Null
    Copy-Item -LiteralPath $Built -Destination (Join-Path $Stage 'SmokeOverhaul') -Recurse -Force
    $Staged = Join-Path $Stage 'SmokeOverhaul'
    Assert-ManifestVersion (Join-Path $Staged 'modpack.json') $ExpectedVersion
    Assert-DllVersion (Join-Path $Staged 'dlls\SmokeOverhaul.dll') $ExpectedAssemblyVersion
    $ExpectedDllHash = (Get-FileHash -LiteralPath (Join-Path $Staged 'dlls\SmokeOverhaul.dll') -Algorithm SHA256).Hash
    Write-Host "Staged DLL SHA256: $ExpectedDllHash"
    Write-Host 'All build and stage checks passed. No game files have been changed.' -ForegroundColor Green
    if ($BuildOnly) {
        Write-Host "BUILD-ONLY SUCCESS. Staged folder: $Stage" -ForegroundColor Green
    }
    else {
        Write-Host '3/4 Preparing a targeted backup outside Mods...' -ForegroundColor Cyan
        $SuiteInstalled = Join-Path $Mods 'MenaceGameplaySuite'
        $InsideSuite = Test-Path -LiteralPath $SuiteInstalled -PathType Container
        $Replace = @()
        $RemoveConflicts = @()
        if ($InsideSuite) {
            Assert-File (Join-Path $SuiteInstalled 'modpack.json') 'Existing Gameplay Suite manifest'
            Assert-File (Join-Path $SuiteInstalled 'dlls\SmokeOverhaul.dll') 'Existing Suite Smoke Overhaul'
            $Replace = @(
                @{ Relative = 'MenaceGameplaySuite\dlls\SmokeOverhaul.dll'; Source = (Join-Path $Staged 'dlls\SmokeOverhaul.dll') },
                @{ Relative = 'MenaceGameplaySuite\source\src\SmokeOverhaul'; Source = (Join-Path $Staged 'source\src\SmokeOverhaul') }
            )
            $RemoveConflicts = @('SmokeOverhaul', 'SmokeOverhaul.dll')
            Write-Host ('Installed Suite manifest retained: ' + (Read-ManifestVersion (Join-Path $SuiteInstalled 'modpack.json')))
            Write-Host 'Installing only the Smoke DLL/source inside existing Suite. Other modules stay unchanged.'
        }
        else {
            $Replace = @(@{ Relative = 'SmokeOverhaul'; Source = $Staged })
            $RemoveConflicts = @('SmokeOverhaul.dll')
            Write-Host 'No Gameplay Suite found: installing the standalone Smoke Overhaul modpack.'
        }
        foreach ($item in $Replace) {
            if (-not (Test-Path -LiteralPath $item.Source)) { throw "Staging item missing: $($item.Source)" }
        }
        $Stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $Backup = Join-Path (Join-Path $GamePath '_MENACE_Backups\MEN001') ($Stamp + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
        New-Item -ItemType Directory -Path $Backup -Force | Out-Null
        $BackupEntries = New-Object 'System.Collections.Generic.List[string]'
        $WrittenEntries = New-Object 'System.Collections.Generic.List[string]'
        $RollbackErrors = New-Object 'System.Collections.Generic.List[string]'
        $TargetsToMove = @($Replace | ForEach-Object { $_.Relative }) + @($RemoveConflicts)
        $TransactionCompleted = $false
        try {
            foreach ($relative in $TargetsToMove) {
                $original = Join-Path $Mods $relative
                if (-not (Test-Path -LiteralPath $original)) { continue }
                $saved = Join-Path $Backup $relative
                New-Item -ItemType Directory -Path (Split-Path -Parent $saved) -Force | Out-Null
                Move-Item -LiteralPath $original -Destination $saved -ErrorAction Stop
                $BackupEntries.Add($relative)
                Write-Host "Backed up: $relative"
            }
            Write-Host '4/4 Installing and verifying staged files...' -ForegroundColor Cyan
            foreach ($item in $Replace) {
                $relative = [string]$item.Relative
                $destination = Join-Path $Mods $relative
                New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
                $WrittenEntries.Add($relative)
                Copy-Item -LiteralPath $item.Source -Destination $destination -Recurse -Force -ErrorAction Stop
                Write-Host "Installed: $relative"
            }
            if ($InsideSuite) {
                $InstalledDll = Join-Path $SuiteInstalled 'dlls\SmokeOverhaul.dll'
                Assert-File (Join-Path $SuiteInstalled 'modpack.json') 'Unchanged Gameplay Suite manifest'
            }
            else {
                $InstalledDll = Join-Path $Mods 'SmokeOverhaul\dlls\SmokeOverhaul.dll'
                Assert-ManifestVersion (Join-Path $Mods 'SmokeOverhaul\modpack.json') $ExpectedVersion
            }
            Assert-DllVersion $InstalledDll $ExpectedAssemblyVersion
            $diskHash = (Get-FileHash -LiteralPath $InstalledDll -Algorithm SHA256).Hash
            if ($diskHash -ne $ExpectedDllHash) { throw 'Installed DLL bytes differ from verified staged build.' }
            foreach ($conflict in $RemoveConflicts) {
                if (Test-Path -LiteralPath (Join-Path $Mods $conflict)) {
                    throw "Duplicate Smoke Overhaul is still present: $conflict"
                }
            }
            $TransactionCompleted = $true
        }
        catch {
            $failure = $_.Exception.Message
            Write-Warning "INSTALL FAILED: $failure. Attempting rollback..."
            for ($i = $WrittenEntries.Count - 1; $i -ge 0; $i--) {
                $created = Join-Path $Mods $WrittenEntries[$i]
                if (Test-Path -LiteralPath $created) {
                    try { Remove-Item -LiteralPath $created -Recurse -Force -ErrorAction Stop }
                    catch { $RollbackErrors.Add("Could not remove $created : $($_.Exception.Message)") }
                }
            }
            for ($i = $BackupEntries.Count - 1; $i -ge 0; $i--) {
                $relative = $BackupEntries[$i]
                $saved = Join-Path $Backup $relative
                $original = Join-Path $Mods $relative
                try {
                    if (Test-Path -LiteralPath $original) { throw "Destination is still occupied: $original" }
                    New-Item -ItemType Directory -Path (Split-Path -Parent $original) -Force | Out-Null
                    Move-Item -LiteralPath $saved -Destination $original -ErrorAction Stop
                }
                catch { $RollbackErrors.Add("Could not restore $relative : $($_.Exception.Message)") }
            }
            if ($RollbackErrors.Count -eq 0) {
                Write-Host 'ROLLBACK SUCCEEDED: previous files restored.' -ForegroundColor Yellow
            }
            else {
                Write-Host 'ROLLBACK INCOMPLETE: review the errors below; backup was preserved.' -ForegroundColor Red
                foreach ($problem in $RollbackErrors) { Write-Host "  $problem" -ForegroundColor Red }
            }
            throw "Installation failed: $failure. Rollback errors: $($RollbackErrors.Count). Backup: $Backup"
        }
        if (-not $TransactionCompleted) { throw 'Installation ended before transaction verification.' }
        Write-Host ''
        Write-Host 'SUCCESS: Smoke Overhaul 0.6.3 was installed and verified.' -ForegroundColor Green
        Write-Host ('Install target: ' + $(if ($InsideSuite) { 'Existing Gameplay Suite (Smoke DLL only)' } else { 'Standalone Smoke Overhaul modpack' }))
        Write-Host "Backup: $Backup"
        Write-Host "Installed assembly version: $ExpectedAssemblyVersion; SHA256: $ExpectedDllHash"
        Write-Host 'Other Gameplay Suite modules, Third Person Camera and saves were not replaced.'
        Write-Host 'In-game smoke use and AI performance remain to be tested.'
    }
}
catch {
    Write-Host ''
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Log: $Log" -ForegroundColor Yellow
    exit 1
}
finally {
    if ($StartedTranscript) { try { Stop-Transcript | Out-Null } catch {} }
}
