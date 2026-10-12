@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem MEN-001 - One-click Smoke Overhaul 0.6.1 installer.
rem No ZIP copying. No live Mods changes before successful build and validation.
rem The current Suite and MEN-006 camera are preserved; only Smoke is updated.

if /I not "%~1"=="--stay-open" (
    start "MENACE MEN-001 Installer" cmd.exe /k call "%~f0" --stay-open
    exit /b 0
)

set "PIN=874bff08443ebc2750c0c364c3ed97da536fe119"
set "BOOTSTRAP_HASH=F2117201286CCBDF46059DD1B160E301E01DB106C6091130B5604A99914EA56E"
set "BOOTSTRAP=%TEMP%\MENACE-MEN001-Install-Component.ps1"
set "LOG=%TEMP%\MENACE-MEN001-install.log"
title MEN-001 Smoke Overhaul 0.6.1 - One Click

>"%LOG%" echo MENACE MEN-001 Smoke Overhaul 0.6.1 installer
>>"%LOG%" echo Pinned source commit: %PIN%
>>"%LOG%" echo Bootstrap expected SHA256: %BOOTSTRAP_HASH%

echo.
echo ===============================================================
echo   MEN-001 SMOKE OVERHAUL 0.6.1 - ONE CLICK INSTALLER
echo ===============================================================
echo.
echo This updates only Smoke Overhaul (standalone or in your Suite).
echo It keeps Overwatch, Coordinated Fire and Third Person Camera.
echo MENACE must be closed. All affected files are backed up first.
echo.
echo Installer log: "%LOG%"
echo Source revision: %PIN%
echo.
echo Downloading and SHA256-verifying the pinned installer...

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
 "$ErrorActionPreference='Stop'; [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12; $u='https://raw.githubusercontent.com/EmaEli/menace-mode/' + $env:PIN + '/mods/GameplaySuite/tools/Install-Component.ps1'; Invoke-WebRequest -UseBasicParsing -Uri $u -OutFile $env:BOOTSTRAP -TimeoutSec 120; $actual=(Get-FileHash -LiteralPath $env:BOOTSTRAP -Algorithm SHA256).Hash; if ($actual -ne $env:BOOTSTRAP_HASH) { throw ('Bootstrap checksum mismatch. Expected ' + $env:BOOTSTRAP_HASH + ', got ' + $actual) }; Write-Output ('Bootstrap checksum verified: ' + $actual)" >>"%LOG%" 2>&1
if errorlevel 1 goto bootstrap_failed

echo Verified installer. Compiling, backing up and deploying automatically...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%BOOTSTRAP%" -SourceCommit "%PIN%" -Component SmokeOverhaul -ExpectedVersion 0.6.1
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" goto install_failed

echo.
echo ===============================================================
echo   SUCCESS: Smoke Overhaul 0.6.1 installed and verified on disk.
echo ===============================================================
echo.
echo Log: "%LOG%"
echo MENACE gameplay tests still need to be performed.
echo This console stays open until you close it manually.
exit /b 0

:bootstrap_failed
echo.
echo ===============================================================
echo   FAILED: Installer download or SHA256 verification.
echo   The game was NOT modified.
echo ===============================================================
echo.
echo The persistent error log will open in Notepad:
echo "%LOG%"
if exist "%LOG%" start "MEN-001 error log" notepad.exe "%LOG%"
echo The console stays open until you close it manually.
exit /b 10

:install_failed
echo.
echo ===============================================================
echo   FAILED: Build, staging or installation.
echo   Read the log for backup and rollback status.
echo ===============================================================
echo.
echo Log: "%LOG%"
if exist "%LOG%" start "MEN-001 error log" notepad.exe "%LOG%"
echo The console stays open until you close it manually.
exit /b %RC%
