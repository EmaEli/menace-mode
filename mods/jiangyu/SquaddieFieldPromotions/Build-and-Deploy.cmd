@echo off
setlocal EnableExtensions
cd /d "%~dp0"
if not exist "artifacts" mkdir "artifacts"
set "LOG=%CD%\artifacts\build-and-deploy.log"
> "%LOG%" echo Squaddie Field Promotions 0.1 / Jiangyu native authoring build
>>"%LOG%" echo Source: %CD%
where jiangyu >nul 2>&1
if errorlevel 1 (
    echo ERROR: The 'jiangyu' CLI is not in PATH.
    echo Install/configure Jiangyu CLI and its SDK, then rerun this file.
    >>"%LOG%" echo ERROR: jiangyu command unavailable
    exit /b 10
)

echo [1/4] Synchronising Jiangyu C# project...
jiangyu code sync >> "%LOG%" 2>&1
if errorlevel 1 goto failed

echo [2/4] Compiling Jiangyu native mod...
jiangyu compile >> "%LOG%" 2>&1
if errorlevel 1 goto failed

echo [3/4] Deploying into MENACE Mods...
jiangyu deploy >> "%LOG%" 2>&1
if errorlevel 1 goto failed

echo [4/4] Packaging compiled mod into artifacts...
jiangyu package --output artifacts >> "%LOG%" 2>&1
if errorlevel 1 goto failed

echo.
echo SUCCESS. Open a DISPOSABLE non-Ironman MENACE campaign.
echo Press F8 for read-only precheck, then follow README for F9.
echo Full log: "%LOG%"
exit /b 0

:failed
echo.
echo FAILED. Send artifacts\build-and-deploy.log for analysis.
echo Full log: "%LOG%"
exit /b 20
