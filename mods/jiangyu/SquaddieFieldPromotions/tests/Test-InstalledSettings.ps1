# Behavior tests for the production shared helper. Writes fixtures only in a fresh temp tree.
param([string]$Helpers, [string]$GamePath)
$ErrorActionPreference = 'Stop'
. $Helpers
$checks = 0
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('MENACE-SFP-dependency-test-' + [guid]::NewGuid().ToString('N'))
$realDll = Get-InstalledSettingsReference $GamePath '0.6.1'
if (-not (Test-Path -LiteralPath $realDll -PathType Leaf)) { throw 'Positive dependency readback failed.' }
$checks++
function Expect-Rejection([string]$Name, [string]$ManifestName, [string]$Version, [bool]$IncludeDll) {
    $case = Join-Path $fixture $Name
    $root = Join-Path $case 'Mods\MenaceModSettings'
    New-Item -ItemType Directory -Path (Join-Path $root 'code') -Force | Out-Null
    @{ name = $ManifestName; version = $Version } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'jiangyu.json') -Encoding UTF8
    if ($IncludeDll) { Copy-Item -LiteralPath $realDll -Destination (Join-Path $root 'code\MenaceModSettings.dll') }
    $rejected = $false
    try { $null = Get-InstalledSettingsReference $case '0.6.1' }
    catch { $rejected = $true }
    if (-not $rejected) { throw "Expected dependency rejection: $Name" }
    $script:checks++
}
Expect-Rejection 'wrong-name' 'OtherMod' '0.6.1' $true
Expect-Rejection 'too-old' 'MenaceModSettings' '0.5.0' $true
Expect-Rejection 'file-version-disagreement' 'MenaceModSettings' '99.0.0' $true
Expect-Rejection 'missing-dll' 'MenaceModSettings' '0.6.1' $false
Expect-Rejection 'malformed-version' 'MenaceModSettings' 'not-a-version' $true
Write-Host "PASS: $checks installed-settings helper checks (real helper/DLL metadata; game files read only)."