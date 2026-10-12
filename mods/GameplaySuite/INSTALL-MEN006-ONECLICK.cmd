@echo off
setlocal
set "PIN=06fa5e6ba39738e24fc18ee7bb3ac3fb91a6ff0b"
set "EXPECTED_HASH=98DC40F7A0FA915D13F3CD7975BFE5962ED6349E3A6502E57A7B14AD0F1886F5"
set "INSTALLER=%TEMP%\MEN006-Integrated-Installer.ps1"
title MEN-006 - Integrated Gameplay Suite Installer

echo.
echo MEN-006 INTEGRATED GAMEPLAY SUITE - ONE CLICK
 echo Building and installing Suite 0.32.3 + Camera 0.3.4.
echo Automatically backs up previous versions; MENACE must be closed.
echo.
echo Downloading the verified installer from the public GitHub repository...
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
 "$ErrorActionPreference='Stop'; [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -UseBasicParsing -Uri 'https://raw.githubusercontent.com/EmaEli/menace-mode/%PIN%/mods/GameplaySuite/tools/Install-IntegratedSuite.ps1' -OutFile '%INSTALLER%' -TimeoutSec 120; $digest=(Get-FileHash -LiteralPath '%INSTALLER%' -Algorithm SHA256).Hash; if ($digest -ne '%EXPECTED_HASH%') { throw 'Installer SHA256 mismatch; refusing execution.' }"
if errorlevel 1 goto download_fail

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%INSTALLER%" -SourceCommit "%PIN%"
set "RESULT=%ERRORLEVEL%"
if exist "%INSTALLER%" del /q "%INSTALLER%" >nul 2>&1
if not "%RESULT%"=="0" goto build_fail

echo.
echo SUCCESS. You can launch MENACE; no manual ZIP copying required.
pause
exit /b 0

:download_fail
echo.
echo DOWNLOAD OR VERIFICATION FAILED. The game was not modified.
pause
exit /b 10

:build_fail
echo.
echo BUILD OR INSTALL FAILED. Review the error above.
echo Previous mod versions are preserved or restored in a dated backup folder.
pause
exit /b %RESULT%