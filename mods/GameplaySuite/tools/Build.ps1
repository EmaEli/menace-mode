param(
    [ValidateSet("All", "Standalone", "Suite")]
    [string]$Mode = "All",
    [switch]$Deploy,
    [string]$GamePath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$Root = Split-Path -Parent $PSScriptRoot
$Artifacts = Join-Path $Root "artifacts"
$Stage = Join-Path $Artifacts "stage"
$Log = Join-Path $Artifacts "build.log"

$Features = @(
    @{
        Name = "Squaddie Veterancy"
        Folder = "SquaddieVeterancy"
        Project = Join-Path $Root "src\SquaddieVeterancy\SquaddieVeterancy.csproj"
        Assembly = "SquaddieProbe.dll"
        ProjectDir = Join-Path $Root "src\SquaddieVeterancy"
        Template = Join-Path $Root "packaging\standalone\SquaddieVeterancy"
    },
    @{
        Name = "Overwatch"
        Folder = "Overwatch"
        Project = Join-Path $Root "src\Overwatch\Overwatch.csproj"
        Assembly = "Overwatch.dll"
        ProjectDir = Join-Path $Root "src\Overwatch"
        Template = Join-Path $Root "packaging\standalone\Overwatch"
    },
    @{
        Name = "Coordinated Fire"
        Folder = "CoordinatedFire"
        Project = Join-Path $Root "src\CoordinatedFire\CoordinatedFire.csproj"
        Assembly = "CoordinatedFire.dll"
        ProjectDir = Join-Path $Root "src\CoordinatedFire"
        Template = Join-Path $Root "packaging\standalone\CoordinatedFire"
    },
    @{
        Name = "Field Logistics"
        Folder = "FieldLogistics"
        Project = Join-Path $Root "src\FieldLogistics\FieldLogistics.csproj"
        Assembly = "FieldLogistics.dll"
        ProjectDir = Join-Path $Root "src\FieldLogistics"
        Template = Join-Path $Root "packaging\standalone\FieldLogistics"
    },
    @{
        Name = "Infantry Assault"
        Folder = "InfantryAssault"
        Project = Join-Path $Root "src\InfantryAssault\InfantryAssault.csproj"
        Assembly = "InfantryAssault.dll"
        ProjectDir = Join-Path $Root "src\InfantryAssault"
        Template = Join-Path $Root "packaging\standalone\InfantryAssault"
    },
    @{
        Name = "Stronger Cover"
        Folder = "StrongerCover"
        Project = Join-Path $Root "src\StrongerCover\StrongerCover.csproj"
        Assembly = "StrongerCover.dll"
        ProjectDir = Join-Path $Root "src\StrongerCover"
        Template = Join-Path $Root "packaging\standalone\StrongerCover"
    },
    @{
        Name = "Starting Options"
        Folder = "StartingOptions"
        Project = Join-Path $Root "src\StartingOptions\StartingOptions.csproj"
        Assembly = "StartingOptions.dll"
        ProjectDir = Join-Path $Root "src\StartingOptions"
        Template = Join-Path $Root "packaging\standalone\StartingOptions"
    },
    @{
        Name = "Building Combat Rules"
        Folder = "BuildingCombatRules"
        Project = Join-Path $Root "src\BuildingCombatRules\BuildingCombatRules.csproj"
        Assembly = "BuildingCombatRules.dll"
        ProjectDir = Join-Path $Root "src\BuildingCombatRules"
        Template = Join-Path $Root "packaging\standalone\BuildingCombatRules"
    },
    @{
        Name = "Forest Traversal"
        Folder = "ForestTraversal"
        Project = Join-Path $Root "src\ForestTraversal\ForestTraversal.csproj"
        Assembly = "ForestTraversal.dll"
        ProjectDir = Join-Path $Root "src\ForestTraversal"
        Template = Join-Path $Root "packaging\standalone\ForestTraversal"
    },
    @{
        Name = "Cover Destruction Rebalance"
        Folder = "CoverDestructionRebalance"
        Project = Join-Path $Root "src\CoverDestructionRebalance\CoverDestructionRebalance.csproj"
        Assembly = "CoverDestructionRebalance.dll"
        ProjectDir = Join-Path $Root "src\CoverDestructionRebalance"
        Template = Join-Path $Root "packaging\standalone\CoverDestructionRebalance"
    },
    @{
        Name = "Weapon Ranges Rebalanced"
        Folder = "WeaponRangesRebalanced"
        Project = Join-Path $Root "src\WeaponRangesRebalanced\WeaponRangesRebalanced.csproj"
        Assembly = "WeaponRangesRebalanced.dll"
        ProjectDir = Join-Path $Root "src\WeaponRangesRebalanced"
        Template = Join-Path $Root "packaging\standalone\WeaponRangesRebalanced"
    },
    @{
        Name = "Weapon Mechanics Rebalanced"
        Folder = "WeaponMechanicsRebalanced"
        Project = Join-Path $Root "src\WeaponMechanicsRebalanced\WeaponMechanicsRebalanced.csproj"
        Assembly = "WeaponMechanicsRebalanced.dll"
        ProjectDir = Join-Path $Root "src\WeaponMechanicsRebalanced"
        Template = Join-Path $Root "packaging\standalone\WeaponMechanicsRebalanced"
    },
    @{
        Name = "Garrisonable Buildings"
        Folder = "GarrisonableBuildings"
        Project = Join-Path $Root "src\GarrisonableBuildings\GarrisonableBuildings.csproj"
        Assembly = "GarrisonableBuildings.dll"
        ProjectDir = Join-Path $Root "src\GarrisonableBuildings"
        Template = Join-Path $Root "packaging\standalone\GarrisonableBuildings"
    },
    @{
        Name = "Buildings Block Line of Sight"
        Folder = "BuildingsBlockLineOfSight"
        Project = Join-Path $Root "src\BuildingsBlockLineOfSight\BuildingsBlockLineOfSight.csproj"
        Assembly = "BuildingsBlockLineOfSight.dll"
        ProjectDir = Join-Path $Root "src\BuildingsBlockLineOfSight"
        Template = Join-Path $Root "packaging\standalone\BuildingsBlockLineOfSight"
    },
    @{
        Name = "Smoke Overhaul"
        Folder = "SmokeOverhaul"
        Project = Join-Path $Root "src\SmokeOverhaul\SmokeOverhaul.csproj"
        Assembly = "SmokeOverhaul.dll"
        ProjectDir = Join-Path $Root "src\SmokeOverhaul"
        Template = Join-Path $Root "packaging\standalone\SmokeOverhaul"
    },
    @{
        Name = "Visibility Overhaul"
        Folder = "VisibilityOverhaul"
        Project = Join-Path $Root "src\VisibilityOverhaul\VisibilityOverhaul.csproj"
        Assembly = "VisibilityOverhaul.dll"
        ProjectDir = Join-Path $Root "src\VisibilityOverhaul"
        Template = Join-Path $Root "packaging\standalone\VisibilityOverhaul"
    }
)

function Write-Log([string]$Text) {
    $Text | Tee-Object -FilePath $Log -Append | ForEach-Object { Write-Host $_ }
}

function Copy-Tree([string]$Source, [string]$Destination) {
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Copy-Item -Path (Join-Path $Source "*") -Destination $Destination -Recurse -Force
}

function Copy-SourceTree([string]$Source, [string]$Destination) {
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Get-ChildItem -Path $Source -Recurse -File | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
    } | ForEach-Object {
        $Relative = $_.FullName.Substring($Source.Length).TrimStart([char[]]'\/')
        $Target = Join-Path $Destination $Relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $Target) -Force | Out-Null
        Copy-Item $_.FullName $Target -Force
    }
}

function Get-Version([string]$Manifest) {
    return (Get-Content -Raw $Manifest | ConvertFrom-Json).version
}

function Build-Feature($Feature) {
    Write-Log "Building $($Feature.Name)..."
    & dotnet build $Feature.Project -c Release "-p:GamePath=$GamePath" 2>&1 | Tee-Object -FilePath $Log -Append | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for $($Feature.Name). See $Log"
    }

    $Dll = Join-Path $Feature.ProjectDir ("bin\Release\net6.0\" + $Feature.Assembly)
    if (-not (Test-Path $Dll)) {
        throw "Expected build output was not created: $Dll"
    }
    return $Dll
}

function Stage-Standalone($Feature, [string]$Dll) {
    $Dest = Join-Path $Stage $Feature.Folder
    if (Test-Path $Dest) { Remove-Item $Dest -Recurse -Force }
    Copy-Tree $Feature.Template $Dest

    $DllDir = Join-Path $Dest "dlls"
    New-Item -ItemType Directory -Path $DllDir -Force | Out-Null
    Copy-Item $Dll (Join-Path $DllDir $Feature.Assembly) -Force

    $SourceDir = Join-Path $Dest "source"
    New-Item -ItemType Directory -Path $SourceDir -Force | Out-Null
    Copy-SourceTree $Feature.ProjectDir (Join-Path $SourceDir ("src\" + $Feature.Folder))
    Copy-SourceTree (Join-Path $Root "src\Shared") (Join-Path $SourceDir "src\Shared")
    Copy-Item (Join-Path $Root "Directory.Build.props") $SourceDir -Force

    $Version = Get-Version (Join-Path $Dest "modpack.json")
    $Zip = Join-Path $Artifacts ($Feature.Folder + "-" + $Version + ".zip")
    Get-ChildItem -Path $Artifacts -Filter ($Feature.Folder + "-*.zip") -File -ErrorAction SilentlyContinue |
        Remove-Item -Force
    Compress-Archive -Path $Dest -DestinationPath $Zip -CompressionLevel Optimal
    Write-Log "Packaged $Zip"
    return $Dest
}

function Stage-Suite($BuiltDlls) {
    $Template = Join-Path $Root "packaging\suite\MenaceGameplaySuite"
    $Dest = Join-Path $Stage "MenaceGameplaySuite"
    if (Test-Path $Dest) { Remove-Item $Dest -Recurse -Force }
    Copy-Tree $Template $Dest

    $DllDir = Join-Path $Dest "dlls"
    New-Item -ItemType Directory -Path $DllDir -Force | Out-Null
    foreach ($Feature in $Features) {
        Copy-Item $BuiltDlls[$Feature.Folder] (Join-Path $DllDir $Feature.Assembly) -Force
    }

    $SourceRoot = Join-Path $Dest "source"
    New-Item -ItemType Directory -Path $SourceRoot -Force | Out-Null
    Copy-SourceTree (Join-Path $Root "src\Shared") (Join-Path $SourceRoot "src\Shared")
    foreach ($Feature in $Features) {
        Copy-SourceTree $Feature.ProjectDir (Join-Path $SourceRoot ("src\" + $Feature.Folder))
    }
    Copy-Item (Join-Path $Root "Directory.Build.props") $SourceRoot -Force
    Copy-Item (Join-Path $Root "README.md") $SourceRoot -Force

    $Version = Get-Version (Join-Path $Dest "modpack.json")
    $Zip = Join-Path $Artifacts ("MenaceGameplaySuite-" + $Version + ".zip")
    Get-ChildItem -Path $Artifacts -Filter "MenaceGameplaySuite-*.zip" -File -ErrorAction SilentlyContinue |
        Remove-Item -Force
    Compress-Archive -Path $Dest -DestinationPath $Zip -CompressionLevel Optimal
    Write-Log "Packaged $Zip"
    return $Dest
}

function Deploy-Tree([string]$Source, [string]$FolderName) {
    $Mods = Join-Path $GamePath "Mods"
    $Dest = Join-Path $Mods $FolderName
    if (Test-Path $Dest) { Remove-Item $Dest -Recurse -Force }
    Copy-Item $Source $Dest -Recurse -Force
    Write-Log "Deployed $FolderName to $Dest"
}

if ([string]::IsNullOrWhiteSpace($GamePath)) {
    $GamePath = "M:\SteamLibrary\steamapps\common\Menace"
}
if (-not (Test-Path (Join-Path $GamePath "Menace.exe"))) {
    $GamePath = Read-Host "Enter your MENACE folder"
}
if (-not (Test-Path (Join-Path $GamePath "Menace.exe"))) {
    throw "Menace.exe was not found under '$GamePath'."
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK was not found in PATH."
}
if (-not (Test-Path (Join-Path $GamePath "MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll"))) {
    throw "MelonLoader IL2CPP assemblies were not found under '$GamePath'."
}
if (-not (Test-Path (Join-Path $GamePath "Mods\Menace.ModpackLoader.dll"))) {
    throw "Menace.ModpackLoader.dll was not found under '$GamePath\Mods'."
}
if ($Deploy -and $Mode -eq "All") {
    throw "Choose either Suite or Standalone when using -Deploy."
}

New-Item -ItemType Directory -Path $Artifacts -Force | Out-Null
if (Test-Path $Stage) { Remove-Item $Stage -Recurse -Force }
New-Item -ItemType Directory -Path $Stage -Force | Out-Null
"MENACE unified repository build" | Set-Content $Log
Write-Log "Mode=$Mode Deploy=$Deploy GamePath=$GamePath"

$BuiltDlls = @{}
foreach ($Feature in $Features) {
    $BuiltDlls[$Feature.Folder] = Build-Feature $Feature
}

$StandaloneStages = @{}
if ($Mode -eq "All" -or $Mode -eq "Standalone") {
    foreach ($Feature in $Features) {
        $StandaloneStages[$Feature.Folder] = Stage-Standalone $Feature $BuiltDlls[$Feature.Folder]
    }
}

$SuiteStage = $null
if ($Mode -eq "All" -or $Mode -eq "Suite") {
    $SuiteStage = Stage-Suite $BuiltDlls
}

if ($Deploy) {
    $Mods = Join-Path $GamePath "Mods"
    if ($Mode -eq "Suite") {
        foreach ($Feature in $Features) {
            $Old = Join-Path $Mods $Feature.Folder
            if (Test-Path $Old) {
                Remove-Item $Old -Recurse -Force
                Write-Log "Removed standalone $($Feature.Folder) to prevent duplicate loading."
            }
        }

        # Cover Destruction Rebalance originally shipped as a direct Melon DLL in Mods\.
        # Remove that legacy copy as well or the suite plugin cannot load the same assembly name.
        $LegacyCoverDestructionDll = Join-Path $Mods "CoverDestructionRebalance.dll"
        if (Test-Path $LegacyCoverDestructionDll) {
            Remove-Item $LegacyCoverDestructionDll -Force
            Write-Log "Removed legacy root CoverDestructionRebalance.dll to prevent duplicate loading."
        }

        Deploy-Tree $SuiteStage "MenaceGameplaySuite"
    }
    elseif ($Mode -eq "Standalone") {
        $Suite = Join-Path $Mods "MenaceGameplaySuite"
        if (Test-Path $Suite) {
            Remove-Item $Suite -Recurse -Force
            Write-Log "Removed MenaceGameplaySuite to prevent duplicate loading."
        }
        foreach ($Feature in $Features) {
            Deploy-Tree $StandaloneStages[$Feature.Folder] $Feature.Folder
        }
    }
}

Write-Log "BUILD COMPLETED SUCCESSFULLY"
Write-Host ""
Write-Host "Done. Packages are in: $Artifacts"
