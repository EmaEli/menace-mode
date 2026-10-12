@echo off
setlocal EnableExtensions
cd /d "%~dp0"
%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Build.ps1" -Mode All
set "EC=%ERRORLEVEL%"
if not "%EC%"=="0" pause
exit /b %EC%
