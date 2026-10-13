# MEN-002 / Larger Maps 1.9: one-click component installer.
# Reuses the pinned-source, build-first, verified-stage, backup/rollback pattern
# from GameplaySuite/tools/Install-Component.ps1 (MEN-001).
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$SourceCommit,
    [string]$GamePath = '',
    [switch]$BuildOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$Log = Join-Path ([IO.Path]::GetTempPath()) 'MEN002-LargerMaps-install.log'
$StartedTranscript = $false
$ExpectedVersion = '1.9'
$ExpectedAssemblyVersion = '1.9.0.0'
$SourceCommit = $SourceCommit.ToLowerInvariant()

function Assert-File([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "$Label missing: $Path" }
}
function Assert-Directory([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { throw "$Label missing: $Path" }
}
function Assert-Manifest([string]$Path) {
    Assert-File $Path 'Larger Maps manifest'
    $data = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ([string]$data.id -ne 'luca.largermaps' -or [string]$data.version -ne $ExpectedVersion) {
        throw "Unexpected modpack identity/version at $Path"
    }
}
function Assert-Assembly([string]$Path) {
    Assert-File $Path 'Larger Maps DLL'
    $actual = [Reflection.AssemblyName]::GetAssemblyName($Path).Version.ToString()
    if ($actual -ne $ExpectedAssemblyVersion) {
        throw "Larger Maps DLL assembly version $actual != $ExpectedAssemblyVersion at $Path"
    }
}
function Resolve-GamePath([string]$Requested) {
    foreach ($candidate in @(
        $Requested,
        'M:\SteamLibrary\steamapps\common\Menace',
        'D:\SteamLibrary\steamapps\common\Menace',
        'C:\Program Files (x86)\Steam\steamapps\common\Menace',
        'C:\Program Files\Steam\steamapps\common\Menace'
    )) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and
            (Test-Path -LiteralPath (Join-Path $candidate 'Menace.exe') -PathType Leaf)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    $typed = (Read-Host 'Enter the MENACE folder containing Menace.exe').Trim('"')
    Assert-File (Join-Path $typed 'Menace.exe') 'Game executable'
    return (Resolve-Path -LiteralPath $typed).Path
}
function Resolve-Interop([string]$Game, [string]$File) {
    $paths = @(
        (Join-Path $Game ("MelonLoader\net6\" + $File)),
        (Join-Path $Game ("MelonLoader\" + $File))
    )
    foreach ($p in $paths) {
        if (Test-Path -LiteralPath $p -PathType Leaf) { return $p }
    }
    $found = Get-ChildItem -LiteralPath (Join-Path $Game 'MelonLoader') -Filter $File -File -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $found) { throw "$File could not be found in MelonLoader." }
    return $found.FullName
}
function Assert-SourceVersion([string]$Project, [string]$Plugin) {
    Assert-File $Project 'C# project'
    Assert-File $Plugin 'C# source'
    [xml]$xml = Get-Content -LiteralPath $Project -Raw
    foreach ($field in @('Version','AssemblyVersion','FileVersion')) {
        $node = $xml.SelectSingleNode('/Project/PropertyGroup/' + $field)
        if ($null -eq $node) { throw "Missing project field $field" }
        $expected = if ($field -eq 'Version') { $ExpectedVersion } else { $ExpectedAssemblyVersion }
        if ($node.InnerText.Trim() -ne $expected) {
            throw "Project $field mismatch: $($node.InnerText) != $expected"
        }
    }
    $code = Get-Content -LiteralPath $Plugin -Raw
    if (-not $code.Contains('private const string Version = "1.9";') -or
        -not $code.Contains('PreserveBasicInfantryFirePositioning')) {
        throw 'Pinned source lacks MEN-002 1.9 experimental switch.'
    }
}
function Hash-File([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

try {
    Start-Transcript -LiteralPath $Log -Append -ErrorAction Stop | Out-Null
    $StartedTranscript = $true
    Write-Host 'MEN-002: LARGER MAPS 1.9 - ONE CLICK BUILD + DEPLOY' -ForegroundColor Cyan
    Write-Host "Full log: $Log"
    Write-Host "Pinned source: $SourceCommit"
    Write-Host 'MENACE must be closed. Only the LargerMaps modpack is replaced.'
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'Missing .NET SDK 6 or later. Install the SDK and rerun the launcher.'
    }
    $sdk = (& dotnet --list-sdks 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0 -or $sdk -notmatch '(?m)^([6-9]|[1-9][0-9])\.') {
        throw "Missing .NET SDK 6 or later. Detected: $sdk"
    }
    $GamePath = Resolve-GamePath $GamePath
    $Mods = Join-Path $GamePath 'Mods'
    Assert-Directory $Mods 'MENACE Mods directory'
    Assert-File (Join-Path $Mods 'Menace.ModpackLoader.dll') 'ModpackLoader'
    Assert-File (Join-Path $GamePath 'MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll') 'IL2CPP Assembly-CSharp'
    Assert-File (Join-Path $GamePath 'MelonLoader\Il2CppAssemblies\Il2Cppmscorlib.dll') 'IL2CPP mscorlib'
    Assert-File (Join-Path $GamePath 'MelonLoader\Il2CppAssemblies\UnityEngine.CoreModule.dll') 'Unity core'
    Assert-File (Join-Path $GamePath 'MelonLoader\Il2CppAssemblies\UnityEngine.TerrainModule.dll') 'Unity terrain'
    Assert-File (Join-Path $GamePath 'MelonLoader\net6\MelonLoader.dll') 'MelonLoader'
    Assert-File (Join-Path $GamePath 'MelonLoader\net6\0Harmony.dll') 'Harmony'
    if (-not $BuildOnly -and (Get-Process -Name 'Menace' -ErrorAction SilentlyContinue)) {
        throw 'MENACE is running. Exit the game before deploying this experimental mod.'
    }
    $Runtime = Resolve-Interop $GamePath 'Il2CppInterop.Runtime.dll'
    $Common = Resolve-Interop $GamePath 'Il2CppInterop.Common.dll'
    Write-Host "Game: $GamePath"

    $Work = Join-Path ([IO.Path]::GetTempPath()) ('MEN002-LargerMaps-' + [guid]::NewGuid().ToString('N'))
    $Zip = Join-Path $Work 'source.zip'
    $Extract = Join-Path $Work 'source'
    New-Item -ItemType Directory -Path $Extract -Force | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

    Write-Host '1/4 Verify pinned public commit and download sources...' -ForegroundColor Cyan
    $CommitInfo = Invoke-RestMethod -Uri ('https://api.github.com/repos/EmaEli/menace-mode/commits/' + $SourceCommit) -Headers @{'User-Agent'='MENACE-MEN002-Installer'} -TimeoutSec 90
    if ([string]$CommitInfo.sha -ne $SourceCommit) { throw 'GitHub commit SHA is not the pinned revision.' }
    Invoke-WebRequest -UseBasicParsing -Uri ('https://codeload.github.com/EmaEli/menace-mode/zip/' + $SourceCommit) -OutFile $Zip -TimeoutSec 180
    if ((Get-Item -LiteralPath $Zip).Length -lt 20000) { throw 'Downloaded repository ZIP is unexpectedly small.' }
    Write-Host ('Downloaded source SHA256: ' + (Hash-File $Zip))
    Expand-Archive -LiteralPath $Zip -DestinationPath $Extract -Force
    $Roots = @(Get-ChildItem -LiteralPath $Extract -Directory)
    if ($Roots.Count -ne 1 -or $Roots[0].Name -ne ('menace-mode-' + $SourceCommit)) {
        throw 'Unexpected GitHub archive root; refusing to build.'
    }
    $ModSrc = Join-Path $Roots[0].FullName 'mods\LargerMaps'
    $Project = Join-Path $ModSrc 'src\LargerMaps\LargerMaps.csproj'
    $Plugin = Join-Path $ModSrc 'src\LargerMaps\Plugin.cs'
    $Manifest = Join-Path $ModSrc 'modpack\LargerMaps\modpack.json'
    Assert-Manifest $Manifest
    Assert-SourceVersion $Project $Plugin

    Write-Host '2/4 Compile Larger Maps only (nothing installed yet)...' -ForegroundColor Cyan
    & dotnet build $Project --configuration Release "--property:GameDir=$GamePath" "--property:InteropRuntime=$Runtime" "--property:InteropCommon=$Common" 2>&1 |
        ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed, code $LASTEXITCODE. Game files are unchanged." }
    $BuiltDll = Join-Path $ModSrc 'src\LargerMaps\bin\Release\net6.0\LargerMaps.dll'
    Assert-Assembly $BuiltDll
    $SourceHash = Hash-File $BuiltDll

    Write-Host '3/4 Stage and verify the complete single-mod distribution...' -ForegroundColor Cyan
    $Stage = Join-Path $Work 'staged\LargerMaps'
    New-Item -ItemType Directory -Path (Join-Path $Stage 'dlls') -Force | Out-Null
    Copy-Item -LiteralPath $Manifest -Destination (Join-Path $Stage 'modpack.json') -Force
    Copy-Item -LiteralPath $BuiltDll -Destination (Join-Path $Stage 'dlls\LargerMaps.dll') -Force
    Assert-Manifest (Join-Path $Stage 'modpack.json')
    Assert-Assembly (Join-Path $Stage 'dlls\LargerMaps.dll')
    if ((Hash-File (Join-Path $Stage 'dlls\LargerMaps.dll')) -ne $SourceHash) {
        throw 'Staged build hash mismatch.'
    }
    $Packages = Join-Path $GamePath '_MENACE_Packages'
    New-Item -ItemType Directory -Path $Packages -Force | Out-Null
    $Package = Join-Path $Packages 'LargerMaps-1.9-MEN002-test.zip'
    $PackageTemp = Join-Path $Work 'LargerMaps-1.9-MEN002-test.zip'
    Compress-Archive -LiteralPath $Stage -DestinationPath $PackageTemp -CompressionLevel Optimal
    Assert-File $PackageTemp 'Staged ZIP'
    Copy-Item -LiteralPath $PackageTemp -Destination $Package -Force
    Write-Host "Test distribution ZIP: $Package"
    if ($BuildOnly) {
        Write-Host 'SUCCESS (build-only): no game mods were replaced.' -ForegroundColor Green
    }
    else {
        Write-Host '4/4 Back up and deploy just Larger Maps...' -ForegroundColor Cyan
        $Destination = Join-Path $Mods 'LargerMaps'
        $LegacyDll = Join-Path $Mods 'LargerMaps.dll'
        $Backup = Join-Path (Join-Path $GamePath '_MENACE_Backups\MEN002') ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
        New-Item -ItemType Directory -Path $Backup -Force | Out-Null
        $Moved = New-Object 'System.Collections.Generic.List[string]'
        $Installed = $false
        $RollbackErrors = New-Object 'System.Collections.Generic.List[string]'
        try {
            foreach ($Name in @('LargerMaps','LargerMaps.dll')) {
                $Old = Join-Path $Mods $Name
                if (Test-Path -LiteralPath $Old) {
                    Move-Item -LiteralPath $Old -Destination (Join-Path $Backup $Name) -ErrorAction Stop
                    $Moved.Add($Name)
                    Write-Host "Backed up: $Name"
                }
            }
            $Installed = $true
            Copy-Item -LiteralPath $Stage -Destination $Destination -Recurse -Force -ErrorAction Stop
            Assert-Manifest (Join-Path $Destination 'modpack.json')
            Assert-Assembly (Join-Path $Destination 'dlls\LargerMaps.dll')
            if ((Hash-File (Join-Path $Destination 'dlls\LargerMaps.dll')) -ne $SourceHash) {
                throw 'Installed DLL differs from the staged verified build.'
            }
            if (Test-Path -LiteralPath $LegacyDll) { throw 'Duplicate standalone LargerMaps.dll remained in Mods.' }
        }
        catch {
            $Cause = $_.Exception.Message
            Write-Warning "INSTALL FAILED: $Cause. Attempting rollback."
            if ($Installed -and (Test-Path -LiteralPath $Destination)) {
                try { Remove-Item -LiteralPath $Destination -Recurse -Force -ErrorAction Stop }
                catch { $RollbackErrors.Add("Cannot remove new LargerMaps folder: $($_.Exception.Message)") }
            }
            for ($i=$Moved.Count-1; $i -ge 0; $i--) {
                $Name = $Moved[$i]
                $Saved = Join-Path $Backup $Name
                $Restore = Join-Path $Mods $Name
                try {
                    if (Test-Path -LiteralPath $Restore) { throw 'Restore destination occupied.' }
                    Move-Item -LiteralPath $Saved -Destination $Restore -ErrorAction Stop
                }
                catch { $RollbackErrors.Add("Could not restore $Name : $($_.Exception.Message)") }
            }
            if ($RollbackErrors.Count -eq 0) { Write-Host 'ROLLBACK SUCCEEDED.' -ForegroundColor Yellow }
            else {
                Write-Host 'ROLLBACK INCOMPLETE. Backups are preserved.' -ForegroundColor Red
                foreach ($Problem in $RollbackErrors) { Write-Host $Problem -ForegroundColor Red }
            }
            throw "Deployment failed; review $Backup. Reason: $Cause. Rollback errors: $($RollbackErrors.Count)"
        }
        Write-Host ''
        Write-Host 'SUCCESS: Larger Maps 1.9 installed and verified.' -ForegroundColor Green
        Write-Host "Mod: $Destination"
        Write-Host "Backup: $Backup"
        Write-Host "Verified DLL assembly: $ExpectedAssemblyVersion"
        Write-Host "Verified DLL SHA256: $SourceHash"
        Write-Host 'Experimental AI option remains OFF by default.'
        Write-Host 'Test MEN-002 in game; this installer does not establish gameplay correctness.'
    }
}
catch {
    Write-Host ''
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Installer log: $Log" -ForegroundColor Yellow
    exit 1
}
finally {
    if ($StartedTranscript) { try { Stop-Transcript | Out-Null } catch {} }
}
