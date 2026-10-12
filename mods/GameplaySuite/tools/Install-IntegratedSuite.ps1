# MEN-006 integrated Gameplay Suite installer.
# Build both source projects from a pinned public GitHub commit, then back up
# and install a SINGLE logical Suite containing all camera components.
# No game files are changed until ALL builds and staging checks have passed.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SourceCommit,
    [string]$GamePath = 'M:\SteamLibrary\steamapps\common\Menace',
    [switch]$BuildOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Persist the full console transcript even when a build fails before deployment.
# The one-click launcher opens this exact file in Notepad on failure.
$Men006Log = Join-Path ([IO.Path]::GetTempPath()) 'MEN006-install.log'
try {
    Start-Transcript -LiteralPath $Men006Log -Append -ErrorAction Stop | Out-Null
    Write-Host ("Full installer diagnostic log: {0}" -f $Men006Log) -ForegroundColor DarkCyan
}
catch {
    Write-Warning ("Unable to start log transcript '{0}': {1}" -f $Men006Log, $_.Exception.Message)
}

function Assert-Exists([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "$Label not found: $Path" }
}
function Read-Version([string]$Path) {
    Assert-Exists $Path 'Manifest'
    return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json).version
}
function Assert-Version([string]$Path, [string]$Expected, [string]$Label) {
    $got = Read-Version $Path
    if ($got -ne $Expected) { throw "$Label version mismatch: $got, expected $Expected" }
    Write-Host "$Label $got" -ForegroundColor DarkCyan
}
function Assert-AssemblyVersion([string]$Path, [string]$Expected, [string]$Label) {
    Assert-Exists $Path $Label
    $got = [Reflection.AssemblyName]::GetAssemblyName($Path).Version.ToString()
    if ($got -ne $Expected) { throw "$Label DLL version mismatch: $got, expected $Expected. See MEN006-install.log for the exact failing step." }
    Write-Host "Verified DLL $Label $got" -ForegroundColor DarkCyan
}
function Run-Child([string]$Script, [string[]]$Arguments) {
    Assert-Exists $Script 'Build script'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Script @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE): $Script" }
}

if ($SourceCommit -notmatch '^[0-9a-fA-F]{40}$') { throw 'Expected a pinned 40-character Git commit SHA.' }
$SourceCommit = $SourceCommit.ToLowerInvariant()
Write-Host ''
Write-Host 'MEN-006: ONE-CLICK INTEGRATED GAMEPLAY SUITE' -ForegroundColor Cyan
Write-Host 'Suite 0.32.3 + Camera 0.3.4 + Overwatch + Coordinated Fire'
Write-Host 'Source: public GitHub, pinned commit (no Git login).'
Write-Host 'Existing mod files are backed up before replacement; other mods are not touched.'
Write-Host ''

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK is missing. Install .NET 6+ SDK; dotnet --version should succeed.'
}
if (-not (Test-Path -LiteralPath (Join-Path $GamePath 'Menace.exe') -PathType Leaf)) {
    $candidates = @(
        'M:\SteamLibrary\steamapps\common\Menace',
        'D:\SteamLibrary\steamapps\common\Menace',
        'C:\Program Files (x86)\Steam\steamapps\common\Menace',
        'C:\Program Files\Steam\steamapps\common\Menace'
    )
    $located = $candidates | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'Menace.exe') -PathType Leaf } | Select-Object -First 1
    if ($located) { $GamePath = $located }
}
if (-not (Test-Path -LiteralPath (Join-Path $GamePath 'Menace.exe') -PathType Leaf)) {
    $GamePath = (Read-Host 'MENACE directory containing Menace.exe').Trim('"')
}
Assert-Exists (Join-Path $GamePath 'Menace.exe') 'MENACE executable'
Assert-Exists (Join-Path $GamePath 'Mods\Menace.ModpackLoader.dll') 'ModpackLoader'
Assert-Exists (Join-Path $GamePath 'MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll') 'IL2CPP references'
if (-not $BuildOnly -and (Get-Process -Name 'Menace' -ErrorAction SilentlyContinue)) {
    throw 'MENACE is running. Close the game and run INSTALL-MEN006.cmd again.'
}

$Mods = Join-Path $GamePath 'Mods'
$Work = Join-Path ([IO.Path]::GetTempPath()) 'MEN006_Integrated_Suite_Build'
$RepoArchive = Join-Path $Work 'source.zip'
$Extract = Join-Path $Work 'source'
$Stage = Join-Path $Work 'integrated'
$Downloads = Join-Path $GamePath '_MEN006_Packages'
New-Item -ItemType Directory -Path $Work, $Downloads -Force | Out-Null
Write-Host '1/3 Downloading exact public GitHub source...' -ForegroundColor Cyan
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$url = 'https://codeload.github.com/EmaEli/menace-mode/zip/' + $SourceCommit
Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $RepoArchive -TimeoutSec 180
if ((Get-Item -LiteralPath $RepoArchive).Length -lt 20000) { throw 'GitHub source archive was unexpectedly small.' }
if (Test-Path -LiteralPath $Extract) { Remove-Item -LiteralPath $Extract -Recurse -Force }
New-Item -ItemType Directory -Path $Extract -Force | Out-Null
Expand-Archive -LiteralPath $RepoArchive -DestinationPath $Extract -Force
$roots = @(Get-ChildItem -LiteralPath $Extract -Directory | Where-Object {
    (Test-Path -LiteralPath (Join-Path $_.FullName 'mods\GameplaySuite\tools\Build.ps1')) -and
    (Test-Path -LiteralPath (Join-Path $_.FullName 'mods\ThirdPersonCamera\tools\build-and-deploy.ps1'))
})
if ($roots.Count -ne 1) { throw 'Public source archive does not contain exactly one MENACE source tree.' }
$Repo = $roots[0].FullName
$Suite = Join-Path $Repo 'mods\GameplaySuite'
$Camera = Join-Path $Repo 'mods\ThirdPersonCamera'
Assert-Version (Join-Path $Suite 'packaging\suite\MenaceGameplaySuite\modpack.json') '0.32.3' 'Gameplay Suite'
Assert-Version (Join-Path $Suite 'packaging\standalone\Overwatch\modpack.json') '0.32.3.3' 'Overwatch'
Assert-Version (Join-Path $Suite 'packaging\standalone\CoordinatedFire\modpack.json') '1.6.9' 'Coordinated Fire'
Assert-Version (Join-Path $Camera 'modpack\ThirdPersonCamera\modpack.json') '0.3.4' 'Third Person Camera'
$CameraSrc = Get-Content -LiteralPath (Join-Path $Camera 'src\ThirdPersonCameraMod.cs') -Raw
$BridgeSrc = Get-Content -LiteralPath (Join-Path $Suite 'src\Shared\ThirdPersonCameraBridge.cs') -Raw
if (-not $CameraSrc.Contains('public static bool IsCinematicViewActive') -or
    -not $BridgeSrc.Contains('Delegate.CreateDelegate')) {
    throw 'MEN-006 source compatibility API is missing. Aborting before compilation.'
}

Write-Host '2/3 Compiling the complete Suite and camera...' -ForegroundColor Cyan
Run-Child (Join-Path $Suite 'tools\Build.ps1') @('-Mode','Suite','-GamePath',$GamePath)
$SuiteBuilt = Join-Path $Suite 'artifacts\stage\MenaceGameplaySuite'
Assert-Exists (Join-Path $SuiteBuilt 'modpack.json') 'Compiled Suite modpack'
Run-Child (Join-Path $Camera 'tools\build-and-deploy.ps1') @('-Game',$GamePath,'-BuildOnly')
$CameraDll = Join-Path $Camera 'bin\Release\MenaceThirdPersonCamera.dll'
$SettingsDll = Join-Path $Camera 'src\ThirdPersonCameraSettings\bin\Release\ThirdPersonCameraSettings.dll'
Assert-AssemblyVersion $CameraDll '0.3.4.0' 'Third Person Camera'
Assert-Exists $SettingsDll 'Third Person Camera settings DLL'
Assert-AssemblyVersion (Join-Path $SuiteBuilt 'dlls\Overwatch.dll') '0.32.3.3' 'Overwatch'
Assert-AssemblyVersion (Join-Path $SuiteBuilt 'dlls\CoordinatedFire.dll') '1.6.9.0' 'Coordinated Fire'
if (Test-Path -LiteralPath $Stage) { Remove-Item -LiteralPath $Stage -Recurse -Force }
$StagedMods = Join-Path $Stage 'Mods'
New-Item -ItemType Directory -Path $StagedMods -Force | Out-Null
# The Suite now owns the camera distribution; ModpackLoader will only see
# MenaceGameplaySuite's single modpack, including the camera settings plugin.
Copy-Item -LiteralPath $SuiteBuilt -Destination (Join-Path $StagedMods 'MenaceGameplaySuite') -Recurse -Force
$StagedSuite = Join-Path $StagedMods 'MenaceGameplaySuite'
Copy-Item -LiteralPath $CameraDll -Destination (Join-Path $StagedMods 'MenaceThirdPersonCamera.dll') -Force
Copy-Item -LiteralPath $SettingsDll -Destination (Join-Path $StagedSuite 'dlls\ThirdPersonCameraSettings.dll') -Force
# Include the camera authoring source within the unified Suite source snapshot.
$CameraSourceRoot = Join-Path $StagedSuite 'source\ThirdPersonCamera'
New-Item -ItemType Directory -Path (Join-Path $CameraSourceRoot 'src\ThirdPersonCameraSettings') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $Camera 'src\ThirdPersonCameraMod.cs') -Destination (Join-Path $CameraSourceRoot 'src\ThirdPersonCameraMod.cs') -Force
Copy-Item -LiteralPath (Join-Path $Camera 'src\ThirdPersonCameraSettings\Plugin.cs') -Destination (Join-Path $CameraSourceRoot 'src\ThirdPersonCameraSettings\Plugin.cs') -Force
Copy-Item -LiteralPath (Join-Path $Camera 'ThirdPersonCamera.csproj') -Destination $CameraSourceRoot -Force
Copy-Item -LiteralPath (Join-Path $Camera 'README.md') -Destination $CameraSourceRoot -Force
Assert-Exists (Join-Path $StagedSuite 'dlls\ThirdPersonCameraSettings.dll') 'Integrated camera settings'
Assert-Exists (Join-Path $StagedMods 'MenaceThirdPersonCamera.dll') 'Integrated camera runtime'
$OutputZip = Join-Path $Downloads 'MenaceGameplaySuite-0.32.3-INTEGRATED.zip'
if (Test-Path -LiteralPath $OutputZip) { Remove-Item -LiteralPath $OutputZip -Force }
Compress-Archive -Path $StagedMods -DestinationPath $OutputZip -CompressionLevel Optimal
Assert-Exists $OutputZip 'Integrated ZIP'
Write-Host "Integrated single package: $OutputZip" -ForegroundColor Green
if ($BuildOnly) {
    Write-Host 'Build-only requested; nothing installed in MENACE.' -ForegroundColor Yellow
    exit 0
}

Write-Host '3/3 Backing up older versions and installing the complete Suite...' -ForegroundColor Cyan
$Stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$Backup = Join-Path (Join-Path $GamePath '_MEN006_Backups') ($Stamp + '-' + [guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Path $Backup -Force | Out-Null
# Back up only known old copies that conflict with this one integrated Suite.
# The backup lives OUTSIDE Mods so ModpackLoader cannot load old plugins.
$Conflicts = @('MenaceGameplaySuite','ThirdPersonCamera','MenaceThirdPersonCamera.dll',
               'Overwatch','CoordinatedFire','Overwatch.dll','CoordinatedFire.dll')
$Moved = New-Object 'System.Collections.Generic.List[string]'
$Installed = New-Object 'System.Collections.Generic.List[string]'
try {
    foreach ($Name in $Conflicts) {
        $Old = Join-Path $Mods $Name
        if (Test-Path -LiteralPath $Old) {
            Move-Item -LiteralPath $Old -Destination (Join-Path $Backup $Name) -ErrorAction Stop
            $Moved.Add($Name)
        }
    }
    $NewSuite = Join-Path $Mods 'MenaceGameplaySuite'
    $Installed.Add('MenaceGameplaySuite')
    Copy-Item -LiteralPath $StagedSuite -Destination $NewSuite -Recurse -Force -ErrorAction Stop
    $NewCamera = Join-Path $Mods 'MenaceThirdPersonCamera.dll'
    $Installed.Add('MenaceThirdPersonCamera.dll')
    Copy-Item -LiteralPath (Join-Path $StagedMods 'MenaceThirdPersonCamera.dll') -Destination $NewCamera -Force -ErrorAction Stop
    Assert-Version (Join-Path $NewSuite 'modpack.json') '0.32.3' 'Installed Gameplay Suite'
    Assert-AssemblyVersion $NewCamera '0.3.4.0' 'Installed camera'
    Assert-Exists (Join-Path $NewSuite 'dlls\ThirdPersonCameraSettings.dll') 'Installed camera menu'
}
catch {
    $reason = $_
    Write-Warning "Installation interrupted: $reason. Attempting rollback..."
    foreach ($Name in $Installed) {
        $Bad = Join-Path $Mods $Name
        if (Test-Path -LiteralPath $Bad) { Remove-Item -LiteralPath $Bad -Recurse -Force -ErrorAction SilentlyContinue }
    }
    foreach ($Name in $Moved) {
        $Saved = Join-Path $Backup $Name
        if (Test-Path -LiteralPath $Saved) {
            Move-Item -LiteralPath $Saved -Destination (Join-Path $Mods $Name) -ErrorAction Continue
        }
    }
    throw "Installation failed and rollback was attempted. Check $Backup. Error: $reason"
}
Write-Host ''
Write-Host 'SUCCESS: Integrated Gameplay Suite 0.32.3 installed with Third Person Camera 0.3.4.' -ForegroundColor Green
Write-Host 'Third Person Camera settings are now inside the Gameplay Suite modpack.'
Write-Host "Backup: $Backup"
Write-Host "Single distributable ZIP: $OutputZip"
Write-Host 'Restart MENACE and test an automatic F7 movement cinematic.'
Write-Host 'Expected game log: Gameplay Suite v0.32.3 and MENACE Third Person Camera v0.3.4.'
Write-Host 'MEN-006 is not considered resolved until the cinematic buttons disappear in game.'