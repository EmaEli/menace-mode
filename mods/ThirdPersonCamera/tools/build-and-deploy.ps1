param(
    [string]$Game = $env:MENACE_GAME,
    [switch]$BuildOnly
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$runtimeDir = Join-Path $root 'refs\runtime'
$artifactsDir = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force -Path $runtimeDir, $artifactsDir | Out-Null
$log = Join-Path $artifactsDir 'build-and-deploy.log'

function RunBuild {
    Write-Host '=========================================='
    Write-Host ' MENACE Third Person Camera 0.3.4'
    Write-Host '=========================================='

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet was not found. Install the .NET 6 SDK (or another SDK supporting net6.0).'
    }

    if ([string]::IsNullOrWhiteSpace($Game)) {
        $candidates = @(
            'M:\SteamLibrary\steamapps\common\Menace',
            'D:\SteamLibrary\steamapps\common\Menace',
            'C:\Program Files (x86)\Steam\steamapps\common\Menace',
            'C:\Program Files\Steam\steamapps\common\Menace'
        )
        $Game = $candidates | Where-Object { Test-Path (Join-Path $_ 'MelonLoader') } | Select-Object -First 1
    }

    if ([string]::IsNullOrWhiteSpace($Game)) {
        $Game = Read-Host 'Full path to your MENACE installation (containing MelonLoader)'
    }
    $Game = $Game.Trim('"').TrimEnd('\')
    if (-not (Test-Path (Join-Path $Game 'MelonLoader'))) {
        throw "MelonLoader directory not found below '$Game'."
    }
    Write-Host "Game: $Game"

    $melonRoot = Join-Path $Game 'MelonLoader'
    $dlls = @(Get-ChildItem -Path $melonRoot -Filter '*.dll' -File -Recurse -ErrorAction Stop)
    $names = @('MelonLoader.dll', 'Il2CppInterop.Runtime.dll', 'Il2CppInterop.Common.dll', 'MelonLoader.ModHandler.dll', 'MelonLoader.Utils.dll', '0Harmony.dll', 'HarmonyLib.dll')

    foreach ($name in $names) {
        $found = $dlls | Where-Object { $_.Name -ieq $name } | Sort-Object -Property `
            @{ Expression = { if ($_.FullName -match '[\\/]net6[\\/]') { 0 } else { 1 } } }, `
            @{ Expression = { $_.FullName.Length } } | Select-Object -First 1
        if ($found) {
            Copy-Item -LiteralPath $found.FullName -Destination (Join-Path $runtimeDir $name) -Force
        }
    }

    foreach ($required in @('MelonLoader.dll', 'Il2CppInterop.Runtime.dll', '0Harmony.dll')) {
        if (-not (Test-Path (Join-Path $runtimeDir $required))) {
            throw "$required could not be found in '$melonRoot'. The game's MelonLoader/IL2CPP install may be incomplete."
        }
    }

    # Use one coherent set of game-generated proxy assemblies. This follows
    # LargerMaps' proven GameDir/MelonLoader/Il2CppAssemblies build approach.
    # The downloaded game snapshot is a fallback when installed proxies are absent.
    # Never mix IL2CPP proxies from different generations in one build.
    $requiredGameReferences = @(
        'Assembly-CSharp.dll',
        'Il2Cppmscorlib.dll',
        'UnityEngine.CoreModule.dll',
        'Unity.InputSystem.dll'
    )
    $bundledGameRefs = Join-Path $root 'refs\game'
    $installedGameRefs = Join-Path $Game 'MelonLoader\Il2CppAssemblies'
    $gameRefs = $bundledGameRefs
    $installedMissing = @($requiredGameReferences | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $installedGameRefs $_) -PathType Leaf)
    })
    if ($installedMissing.Count -eq 0) {
        $installedAssemblyText = [System.Text.Encoding]::ASCII.GetString(
            [System.IO.File]::ReadAllBytes((Join-Path $installedGameRefs 'Assembly-CSharp.dll')))
        if ($installedAssemblyText.Contains('Il2CppMenace.Tactical') -and
            $installedAssemblyText.Contains('Il2CppMenace.States')) {
            $gameRefs = $installedGameRefs
            Write-Host 'Using current game-generated IL2CPP interop references (including Il2Cppmscorlib).'
        } else {
            Write-Warning 'Installed Assembly-CSharp.dll is not the expected IL2CPP proxy. Using coherent bundled snapshot.'
        }
        Remove-Variable installedAssemblyText
    } else {
        Write-Host 'Installed IL2CPP references incomplete; using coherent bundled snapshot.'
    }

    foreach ($name in $requiredGameReferences) {
        $referencePath = Join-Path $gameRefs $name
        if (-not (Test-Path -LiteralPath $referencePath -PathType Leaf)) {
            throw "Missing MENACE reference: $referencePath. Re-extract the ZIP or regenerate MelonLoader Il2CppAssemblies."
        }
    }

    $gameAssembly = Join-Path $gameRefs 'Assembly-CSharp.dll'
    $assemblyText = [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($gameAssembly))
    foreach ($name in @('Il2CppMenace.Tactical', 'Il2CppMenace.States', 'TacticalCamera', 'TacticalManager')) {
        if (-not $assemblyText.Contains($name)) {
            throw "Assembly-CSharp.dll lacks expected generated interop symbol '$name': $gameAssembly"
        }
    }
    Remove-Variable assemblyText

    # Catch the root cause of the 0.1.2 CS0012 failures up front.
    $mscorlibPath = Join-Path $gameRefs 'Il2Cppmscorlib.dll'
    if ((Get-Item -LiteralPath $mscorlibPath).Length -lt 100000) {
        throw "Il2Cppmscorlib.dll is abnormally small: $mscorlibPath"
    }

    $sourcePath = Join-Path $root 'src\ThirdPersonCameraMod.cs'
    $sourceText = [System.IO.File]::ReadAllText($sourcePath)
    if ($sourceText -match '(?m)^\s*using\s+Menace\.') {
        throw 'Source still imports original Menace.* namespaces. Generated interop DLL requires Il2CppMenace.* namespaces.'
    }
    foreach ($name in @('using Il2CppMenace.Tactical;', 'using Il2CppMenace.States;')) {
        if (-not $sourceText.Contains($name)) {
            throw "Source is missing required generated interop import '$name'."
        }
    }
    if (-not $sourceText.Contains("new HarmonyLib.Harmony(")) {
        throw "Harmony hook implementation must use the fully qualified HarmonyLib.Harmony type."
    }
    if (-not $sourceText.Contains('assembly.GetType("Menace.SDK.ModSettings", false)')) {
        throw 'Camera settings reader must look for Menace.SDK.ModSettings, otherwise menu values are ignored.'
    }
    Write-Host "Validated interop reference directory: $gameRefs"

    & dotnet build (Join-Path $root 'ThirdPersonCamera.csproj') -c Release --nologo -v minimal "-p:MenaceInteropDir=$gameRefs"
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }

    # A tiny ModpackLoader plugin provides the real game Mod Settings menu.
    # It is built and deployed only if the user already has the ModpackLoader.
    $modpackLoader = Join-Path $Game 'Mods\Menace.ModpackLoader.dll'
    $settingsProject = Join-Path $root 'src\ThirdPersonCameraSettings\ThirdPersonCameraSettings.csproj'
    $settingsDll = Join-Path $root 'src\ThirdPersonCameraSettings\bin\Release\ThirdPersonCameraSettings.dll'
    $installSettings = Test-Path -LiteralPath $modpackLoader -PathType Leaf
    if ($installSettings) {
        $settingsSource = Join-Path $root 'src\ThirdPersonCameraSettings\Plugin.cs'
        $settingsSourceText = [System.IO.File]::ReadAllText($settingsSource)
        # IModpackPlugin requires both methods. Detect the exact regressions
        # seen in the first 0.3.1 Mod Settings build before invoking dotnet.
        if (-not $settingsSourceText.Contains('public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)')) {
            throw 'Mod Settings plugin OnInitialize must explicitly use HarmonyLib.Harmony (avoids CS0118/CS0535).'
        }
        if (-not $settingsSourceText.Contains('public void OnSceneLoaded(int buildIndex, string sceneName)')) {
            throw 'Mod Settings plugin must implement IModpackPlugin.OnSceneLoaded(int, string) (avoids CS0535).'
        }
        # ModSettings is part of Menace.SDK, not the Menace.ModpackLoader namespace.
        # This guard prevents the CS0103 regression encountered with v0.3.2.
        if ($settingsSourceText.Contains('ModSettings.Register(') -and
            -not ($settingsSourceText -match '(?m)^\s*using\s+Menace\.SDK\s*;')) {
            throw 'Mod Settings plugin must import Menace.SDK for ModSettings.Register (avoids CS0103).'
        }
        Remove-Variable settingsSourceText
        & dotnet build $settingsProject -c Release --nologo -v minimal "-p:GameDir=$Game"
        if ($LASTEXITCODE -ne 0) { throw "Mod Settings build failed with exit code $LASTEXITCODE" }
        if (-not (Test-Path -LiteralPath $settingsDll -PathType Leaf)) {
            throw "Mod Settings DLL not found: $settingsDll"
        }
        Write-Host 'Compiled Mod Settings companion for Menace.ModpackLoader.'
    } else {
        Write-Warning 'Menace.ModpackLoader.dll not found: compiling standalone camera only. Configure probabilities in UserData\MelonPreferences.cfg.'
    }

    $builtDll = Join-Path $root 'bin\Release\MenaceThirdPersonCamera.dll'
    if (-not (Test-Path $builtDll)) { throw "Build output not found: $builtDll" }
    Write-Host "Output: $builtDll"

    if (-not $BuildOnly) {
        $destination = Join-Path $Game 'Mods'
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        Copy-Item -LiteralPath $builtDll -Destination (Join-Path $destination 'MenaceThirdPersonCamera.dll') -Force
        Write-Host "Installed in: $destination"
        if ($installSettings) {
            $modpackDestination = Join-Path $destination 'ThirdPersonCamera'
            $settingsDestination = Join-Path $modpackDestination 'dlls'
            $settingsManifest = Join-Path $root 'modpack\ThirdPersonCamera\modpack.json'
            New-Item -ItemType Directory -Force -Path $settingsDestination | Out-Null
            Copy-Item -LiteralPath $settingsDll -Destination (Join-Path $settingsDestination 'ThirdPersonCameraSettings.dll') -Force
            Copy-Item -LiteralPath $settingsManifest -Destination (Join-Path $modpackDestination 'modpack.json') -Force
            Write-Host "Installed in-game Mod Settings integration: $modpackDestination"
        }
    }
}

try {
    & {
        RunBuild
    } *>&1 | Tee-Object -FilePath $log
    if (-not $?) { exit 1 }
    exit 0
}
catch {
    ($_ | Out-String) | Tee-Object -FilePath $log -Append
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Full log: $log"
    exit 1
}
