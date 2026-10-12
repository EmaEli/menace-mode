@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\build-and-deploy.ps1" -BuildOnly %*
set "RESULT=%ERRORLEVEL%"
echo.
if not "%RESULT%"=="0" echo Build failed. Exit code: %RESULT%
if "%RESULT%"=="0" echo Build succeeded.
pause
exit /b %RESULT%
