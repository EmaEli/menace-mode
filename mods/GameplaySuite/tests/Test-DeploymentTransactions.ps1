param([Parameter(Mandatory=$true)][string]$ToolsRoot)
$ErrorActionPreference = 'Stop'
. (Join-Path $ToolsRoot 'Install.Common.ps1')
. (Join-Path $ToolsRoot 'Install.Deployment.ps1')

# Only newly-created synthetic files are touched. Never use the real game path.
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('MENACE-transaction-test-' + [guid]::NewGuid().ToString('N'))
$game = Join-Path $fixture 'game'
$stage = Join-Path $fixture 'stage'
foreach ($path in @('game\Mods\Alpha', 'game\UserData', 'game\Saves', 'stage\Alpha')) {
    New-Item -ItemType Directory -Path (Join-Path $fixture $path) -Force | Out-Null
}
$old = Join-Path $game 'Mods\Alpha\value.txt'
$legacy = Join-Path $game 'Mods\LegacyAlpha.dll'
$unrelated = Join-Path $game 'Mods\Unrelated.dll'
$settings = Join-Path $game 'UserData\fixture.txt'
$save = Join-Path $game 'Saves\fixture.txt'
[IO.File]::WriteAllText($old, 'old')
[IO.File]::WriteAllText($legacy, 'legacy')
[IO.File]::WriteAllText($unrelated, 'unrelated')
[IO.File]::WriteAllText($settings, 'settings')
[IO.File]::WriteAllText($save, 'save')
[IO.File]::WriteAllText((Join-Path $stage 'Alpha\value.txt'), 'new')

function Assert-Preserved {
    if ([IO.File]::ReadAllText($unrelated) -ne 'unrelated') { throw 'Unrelated mod changed.' }
    if ([IO.File]::ReadAllText($settings) -ne 'settings') { throw 'Settings changed.' }
    if ([IO.File]::ReadAllText($save) -ne 'save') { throw 'Save changed.' }
}
function Assert-Originals {
    if ([IO.File]::ReadAllText($old) -ne 'old') { throw 'Original package not restored.' }
    if ([IO.File]::ReadAllText($legacy) -ne 'legacy') { throw 'Legacy alias not restored.' }
    if (Test-Path -LiteralPath (Join-Path $game 'Mods\Alpha\partial.txt')) { throw 'Partial copy remained live.' }
    Assert-Preserved
}

Install-StagedMods $game $stage @('Alpha') @('LegacyAlpha.dll')
if ([IO.File]::ReadAllText($old) -ne 'new') { throw 'New package missing.' }
if (Test-Path -LiteralPath $legacy) { throw 'Legacy alias still live.' }
Assert-Preserved
$backup = Get-ChildItem -LiteralPath (Join-Path $game '_MENACE_Backups') -Directory | Select-Object -First 1
Restore-ModBackup $game $backup.FullName -CheckCurrent
Assert-Originals
Write-Output 'TRANSACTION_TEST: install, aliases, protected files and manual rollback passed.'

# Inject a copy failure only for this fixture's staged package. The production
# recovery path still performs the restore using the actual Copy-Item cmdlet.
$script:failSource = Join-Path $stage 'Alpha'
function Copy-Item {
    [CmdletBinding()]
    param([string]$LiteralPath, [string]$Destination, [switch]$Recurse)
    if ($LiteralPath -eq $script:failSource) {
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $Destination 'partial.txt'), 'incomplete')
        throw 'Injected fixture copy interruption after partial write.'
    }
    Microsoft.PowerShell.Management\Copy-Item @PSBoundParameters
}
$caught = $false
try { Install-StagedMods $game $stage @('Alpha') @('LegacyAlpha.dll') }
catch {
    if ($_.Exception.Message -notlike '*INSTALL FAILED; rollback verified*') { throw }
    $caught = $true
}
if (-not $caught) { throw 'Injected copy failure did not fail the installation.' }
Assert-Originals
Write-Output 'TRANSACTION_TEST: interrupted copy and automatic rollback passed.'
Write-Output ('Synthetic fixture/backups retained: ' + $fixture)