@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\build-and-deploy.ps1" %*
set "RESULT=%ERRORLEVEL%"
echo.
if not "%RESULT%"=="0" echo Build or deploy failed. Exit code: %RESULT%
if "%RESULT%"=="0" echo Build and deploy succeeded.
pause
exit /b %RESULT%
