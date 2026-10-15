@echo off
setlocal
if /i not "%~1"=="--stay-open" (
    start "MENACE installer" cmd.exe /k call "%~f0" --stay-open
    exit /b 0
)
set "MENACE_KIT_ROOT=%~dp0"
set "LOG=%TEMP%\MENACE-install.log"
echo MENACE bundled-source installer starting.>"%LOG%"
echo Full diagnostic log: "%LOG%"
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $r=$env:MENACE_KIT_ROOT; $k=Get-Content -LiteralPath (Join-Path $r 'kit.json') -Raw | ConvertFrom-Json; foreach($n in @('Install-IntegratedSuite.ps1','Install.Common.ps1','Install.Deployment.ps1')) { $p='source/mods/GameplaySuite/tools/'+$n; $f=@($k.files | Where-Object {$_.path -eq $p}); if($f.Count -ne 1) {throw 'Missing or duplicate installer hash'}; if((Get-FileHash -LiteralPath (Join-Path $r $p) -Algorithm SHA256).Hash -ne $f[0].sha256) {throw 'Installer integrity check failed'} }" >>"%LOG%" 2>&1
if errorlevel 1 goto failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0source\mods\GameplaySuite\tools\Install-IntegratedSuite.ps1" -BundleRoot "%~dp0."
if errorlevel 1 goto failed
echo SUCCESS. Console stays open; close it manually.
exit /b 0
:failed
echo FAILED. See "%LOG%". Console stays open.
if exist "%LOG%" start "MENACE install log" notepad.exe "%LOG%"
exit /b 1