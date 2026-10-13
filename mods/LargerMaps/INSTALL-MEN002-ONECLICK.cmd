@echo off
setlocal EnableExtensions
if /i not "%~1"=="--stay-open" (
    start "MEN-002 Larger Maps Installer" cmd.exe /k call "%~f0" --stay-open
    exit /b 0
)

set "PIN=50f59ac8015010f11fd1c10e3f03a2437f576da3"
set "EXPECTED_HASH=243B926DA4E56B39C3A46B2DD40B033D179CA1F56FC470033BA7474247636CD9"
set "INSTALLER=%TEMP%\MEN002-LargerMaps-installer.ps1"
set "LOG=%TEMP%\MEN002-LargerMaps-install.log"
title MEN-002 - Larger Maps 1.9 One-Click Installer
echo.
echo MEN-002 / LARGER MAPS 1.9 - EXPERIMENTAL
echo Compiles and installs ONLY Larger Maps, with backup and rollback.
echo Close MENACE before continuing.
echo.
echo Log: "%LOG%"
echo Pinned GitHub commit: %PIN%
>"%LOG%" echo MEN-002 One-click install, pinned source %PIN%
>>"%LOG%" echo Bootstrap started %DATE% %TIME%
echo Downloading the verified installer script...
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
 "$ErrorActionPreference='Stop'; [Net.ServicePointManager]::SecurityProtocol=[Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -UseBasicParsing -Uri 'https://raw.githubusercontent.com/EmaEli/menace-mode/%PIN%/mods/LargerMaps/tools/Install-OneClick.ps1' -OutFile '%INSTALLER%' -TimeoutSec 120; $digest=(Get-FileHash -LiteralPath '%INSTALLER%' -Algorithm SHA256).Hash; if ($digest -ne '%EXPECTED_HASH%') { throw 'Installer checksum mismatch; no script executed.' }" >>"%LOG%" 2>&1
if errorlevel 1 goto DOWNLOAD_FAILED

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%INSTALLER%" -SourceCommit "%PIN%"
set "RESULT=%ERRORLEVEL%"
if not "%RESULT%"=="0" goto INSTALL_FAILED
echo.
echo SUCCESS: Larger Maps 1.9 has been built and installed.
echo This does NOT mean MEN-002 has been verified in the game.
echo Console remains open; close the window manually.
exit /b 0

:DOWNLOAD_FAILED
echo.
echo FAILED: Could not download or verify the installer. No mod files changed.
if exist "%LOG%" start "MEN-002 error log" notepad.exe "%LOG%"
echo Console remains open; close the window manually.
exit /b 10

:INSTALL_FAILED
echo.
echo FAILED: Build or install failed. Check the detailed log.
echo Existing mod is preserved or a rollback was attempted.
if exist "%LOG%" start "MEN-002 error log" notepad.exe "%LOG%"
echo Console remains open; close the window manually.
exit /b %RESULT%
