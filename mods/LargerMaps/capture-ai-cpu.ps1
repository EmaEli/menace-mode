# Optional external CPU sampling. No MENACE or mod files are changed.
# Use an elevated Windows PowerShell console when WPR requires elevation.
$ErrorActionPreference = 'Stop'
$wpr = Get-Command 'wpr.exe' -ErrorAction SilentlyContinue
if ($null -eq $wpr) { Write-Error 'Windows Performance Recorder (wpr.exe) is unavailable.'; exit 2 }
$folder = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$file = Join-Path $folder ('MENACE-AI-CPU-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.etl')
Write-Host 'Open the 96x96 mission and prepare for a slow enemy phase.'
Write-Host 'This is a system-wide CPU sample. Stop other WPR recordings first.'
Read-Host 'Press Enter to START the CPU trace'
$started = $false
try {
    & $wpr.Source -start CPU -filemode
    if ($LASTEXITCODE -ne 0) { throw ('WPR start failed: ' + $LASTEXITCODE) }
    $started = $true
    Write-Host 'Recording. Run ONE slow enemy phase in MENACE.'
    Read-Host 'Press Enter to STOP and save the trace'
    & $wpr.Source -stop $file
    if ($LASTEXITCODE -ne 0) { throw ('WPR stop failed: ' + $LASTEXITCODE) }
    $started = $false
    Write-Host ('Saved CPU trace: ' + $file)
}
finally {
    if ($started) {
        Write-Warning 'Cancelling the incomplete WPR recording.'
        & $wpr.Source -cancel | Out-Null
    }
}
