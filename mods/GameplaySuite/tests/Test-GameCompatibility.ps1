# Exercises the production native compatibility gate without changing game files.
[CmdletBinding()]
param([string]$ToolsRoot)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $ToolsRoot) { $ToolsRoot = Join-Path $PSScriptRoot '..\tools' }
foreach ($name in @('Install.Common.ps1', 'Install-IntegratedSuite.ps1')) {
    $tokens = $null; $errors = $null
    $null = [Management.Automation.Language.Parser]::ParseFile((Join-Path $ToolsRoot $name), [ref]$tokens, [ref]$errors)
    if ($errors.Count) { throw "PowerShell parse failed: $name" }
}
. (Join-Path $ToolsRoot 'Install.Common.ps1')
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('MENACE-CompatibilityTests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
$primary = Join-Path $fixtureRoot 'primary.txt'
$scoped = Join-Path $fixtureRoot 'scoped.txt'
$unknown = Join-Path $fixtureRoot 'unknown.txt'
foreach ($path in @($primary, $scoped, $unknown)) { [IO.File]::WriteAllText($path, $path) }
$catalog = [pscustomobject]@{
    gameVersion = 'primary'
    gameAssemblySha256 = (Get-FileHash -LiteralPath $primary -Algorithm SHA256).Hash
    standaloneGameBuilds = @([pscustomobject]@{
        gameVersion = 'scoped'
        gameAssemblySha256 = (Get-FileHash -LiteralPath $scoped -Algorithm SHA256).Hash
        modules = @('MenaceModSettings', 'GarrisonableBuildings')
    })
}
$checks = 0
function Verify([string]$Path, [string]$Product, [string[]]$Modules, [bool]$Accept) {
    $accepted = $true
    try { Assert-GameBuild $Path $catalog $Product $Modules }
    catch { $accepted = $false }
    if ($accepted -ne $Accept) { throw "Compatibility regression: $Product / $($Modules -join ',')" }
    $script:checks++
}
Verify $primary 'Suite' @() $true
Verify $primary 'Camera' @() $true
Verify $primary 'Standalone' @('OtherMod') $true
Verify $scoped 'Standalone' @('GarrisonableBuildings') $true
Verify $scoped 'Standalone' @('MenaceModSettings', 'GarrisonableBuildings') $true
Verify $scoped 'Suite' @('GarrisonableBuildings') $false
Verify $scoped 'Camera' @('GarrisonableBuildings') $false
Verify $scoped 'Standalone' @() $false
Verify $scoped 'Standalone' @('OtherMod') $false
Verify $scoped 'Standalone' @('GarrisonableBuildings', 'OtherMod') $false
Verify $unknown 'Standalone' @('GarrisonableBuildings') $false
# Companion compatibility is mandatory even when only the feature is explicitly selected.
$catalog.standaloneGameBuilds[0].modules = @('GarrisonableBuildings')
Verify $scoped 'Standalone' @('GarrisonableBuildings') $false
$catalog.PSObject.Properties.Remove('standaloneGameBuilds')
Verify $primary 'Suite' @() $true
Verify $scoped 'Standalone' @('GarrisonableBuildings') $false
Write-Host "Game compatibility checks passed: $checks. Fixture files retained in $fixtureRoot"