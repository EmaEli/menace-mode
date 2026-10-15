# Recoverable, direct-child-only deployment. No recursive deletion of live data.
function Restore-ModBackup([string]$Game, [string]$Backup, [switch]$CheckCurrent, $Journal = $null) {
    Assert-GameClosed
    $allowedRoot = [IO.Path]::GetFullPath((Join-Path $Game '_MENACE_Backups')).TrimEnd('\', '/')
    if ([IO.Directory]::GetParent([IO.Path]::GetFullPath($Backup)).FullName -ne $allowedRoot) { throw 'Backup must be a direct child of this game backup directory.' }
    Assert-NotLink $Backup
    if ($null -eq $Journal) { $Journal = Get-Content -LiteralPath (Join-Path $Backup 'journal.json') -Raw | ConvertFrom-Json }
    $journal = $Journal
    if ($journal.game -ne [IO.Path]::GetFullPath($Game)) { throw 'Backup belongs to a different game directory.' }
    $mods = Join-Path $Game 'Mods'
    Assert-NotLink $mods
    foreach ($entry in $journal.targets) {
        Assert-SafeName $entry.name
        if ($entry.moved) { Assert-Inventory (Join-Path (Join-Path $Backup 'originals') $entry.name) @($entry.oldFiles) }
        if ($CheckCurrent -and $entry.moved -and -not $entry.installed -and (Test-Path -LiteralPath (Join-Path $mods $entry.name))) {
            throw "Legacy conflict target reappeared; refusing destructive rollback: $($entry.name)"
        }
        if ($CheckCurrent -and $entry.installed -and (Test-Path -LiteralPath (Join-Path $mods $entry.name))) {
            Assert-Inventory (Join-Path $mods $entry.name) @($entry.newFiles)
        }
    }
    $errors = New-Object 'System.Collections.Generic.List[string]'
    foreach ($entry in $journal.targets) {
        try {
            $live = Join-Path $mods $entry.name
            $old = Join-Path (Join-Path $Backup 'originals') $entry.name
            if ($entry.installed -and (Test-Path -LiteralPath $live)) {
                Assert-NotLink $live
                $failed = Join-Path $Backup ('replaced-' + [guid]::NewGuid().ToString('N'))
                New-Item -ItemType Directory -Path $failed | Out-Null
                Move-Item -LiteralPath $live -Destination (Join-Path $failed $entry.name) -ErrorAction Stop
            }
            if ($entry.moved) {
                Assert-Inventory $old @($entry.oldFiles)
                if (Test-Path -LiteralPath $live) { throw "Restore target unexpectedly exists: $live" }
                Copy-Item -LiteralPath $old -Destination $live -Recurse -ErrorAction Stop
                Assert-Inventory $live @($entry.oldFiles)
            }
        }
        catch { $errors.Add("$($entry.name): $($_.Exception.Message)") }
    }
    if ($errors.Count) { throw ('Rollback incomplete; originals remain in ' + $Backup + ': ' + ($errors -join '; ')) }
    Write-Host "ROLLBACK VERIFIED. Originals retained in: $Backup"
}

function Install-StagedMods([string]$Game, [string]$Stage, [string[]]$Names, [string[]]$Conflicts) {
    Assert-GameClosed
    $mods = Join-Path $Game 'Mods'
    Assert-NotLink $mods
    $backupRoot = Join-Path $Game '_MENACE_Backups'
    if (Test-Path -LiteralPath $backupRoot) { Assert-NotLink $backupRoot }
    $backup = Join-Path $backupRoot ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path (Join-Path $backup 'originals') -Force | Out-Null
    $targets = @()
    # Snapshot/check every target before moving the first live file.
    foreach ($name in @($Names + $Conflicts | Sort-Object -Unique)) {
        Assert-SafeName $name
        $live = Join-Path $mods $name
        $oldFiles = @()
        if (Test-Path -LiteralPath $live) { $oldFiles = @(Get-TreeInventory $live) }
        $newFiles = @()
        if ($Names -contains $name) { $newFiles = @(Get-TreeInventory (Join-Path $Stage $name)) }
        $targets += [pscustomobject]@{ name=$name; moved=$false; installed=$false; oldFiles=$oldFiles; newFiles=$newFiles }
    }
    $journal = [pscustomobject]@{ game=[IO.Path]::GetFullPath($Game); targets=$targets }
    $journalPath = Join-Path $backup 'journal.json'
    $journal | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $journalPath -Encoding UTF8
    try {
        foreach ($entry in $targets) {
            $live = Join-Path $mods $entry.name
            if (Test-Path -LiteralPath $live) {
                Move-Item -LiteralPath $live -Destination (Join-Path (Join-Path $backup 'originals') $entry.name) -ErrorAction Stop
                $entry.moved = $true
                $journal | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $journalPath -Encoding UTF8
            }
        }
        foreach ($entry in $targets) {
            if ($Names -notcontains $entry.name) { continue }
            $entry.installed = $true # includes partially copied files in rollback
            $journal | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $journalPath -Encoding UTF8
            Copy-Item -LiteralPath (Join-Path $Stage $entry.name) -Destination (Join-Path $mods $entry.name) -Recurse -ErrorAction Stop
        }
        foreach ($entry in $targets) {
            if ($entry.installed) { Assert-Inventory (Join-Path $mods $entry.name) @($entry.newFiles) }
        }
    }
    catch {
        $failure = $_.Exception.Message
        try { Restore-ModBackup $Game $backup -Journal $journal }
        catch { throw "INSTALL FAILED: $failure. $($_.Exception.Message)" }
        throw "INSTALL FAILED; rollback verified. Backup: $backup. Cause: $failure"
    }
    Write-Host "Backup and rollback journal: $backup"
}