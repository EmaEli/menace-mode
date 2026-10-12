@echo off
setlocal EnableExtensions EnableDelayedExpansion
set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\LargerMaps\LargerMaps.csproj"
set "MODSRC=%ROOT%modpack\LargerMaps"
set "LOG=%ROOT%artifacts\build-and-deploy.log"
if not exist "%ROOT%artifacts" mkdir "%ROOT%artifacts"
if "%MENACE_GAME%"=="" set "MENACE_GAME=M:\SteamLibrary\steamapps\common\Menace"
set "GAME=%MENACE_GAME%"
set "DEST=%GAME%\Mods\LargerMaps"
> "%LOG%" echo Larger Maps 1.6.1 build/deploy
>>"%LOG%" echo Started: %date% %time%
>>"%LOG%" echo Working directory: %ROOT%
>>"%LOG%" echo GAME=%GAME%
if not exist "%GAME%\Menace.exe" (
  echo ERROR: MENACE not found at "%GAME%"
  echo Set MENACE_GAME to your game folder and run again.
  pause
  exit /b 2
)
if not exist "%GAME%\Mods\Menace.ModpackLoader.dll" (
  echo ERROR: Menace.ModpackLoader.dll not found in "%GAME%\Mods".
  pause
  exit /b 3
)
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: dotnet SDK not found.
  pause
  exit /b 4
)
set "INTEROP_RUNTIME=%GAME%\MelonLoader\net6\Il2CppInterop.Runtime.dll"
if not exist "%INTEROP_RUNTIME%" set "INTEROP_RUNTIME=%GAME%\MelonLoader\Il2CppInterop.Runtime.dll"
set "INTEROP_COMMON=%GAME%\MelonLoader\net6\Il2CppInterop.Common.dll"
if not exist "%INTEROP_COMMON%" set "INTEROP_COMMON=%GAME%\MelonLoader\Il2CppInterop.Common.dll"
if not exist "%INTEROP_RUNTIME%" (
  for /r "%GAME%\MelonLoader" %%F in (Il2CppInterop.Runtime.dll) do if not defined INTEROP_RUNTIME_FOUND set "INTEROP_RUNTIME_FOUND=%%~fF"
  if defined INTEROP_RUNTIME_FOUND set "INTEROP_RUNTIME=%INTEROP_RUNTIME_FOUND%"
)
if not exist "%INTEROP_COMMON%" (
  for /r "%GAME%\MelonLoader" %%F in (Il2CppInterop.Common.dll) do if not defined INTEROP_COMMON_FOUND set "INTEROP_COMMON_FOUND=%%~fF"
  if defined INTEROP_COMMON_FOUND set "INTEROP_COMMON=%INTEROP_COMMON_FOUND%"
)
dotnet build "%PROJECT%" -c Release -p:GameDir="%GAME%" -p:InteropRuntime="%INTEROP_RUNTIME%" -p:InteropCommon="%INTEROP_COMMON%" >>"%LOG%" 2>&1
if errorlevel 1 (
  echo BUILD FAILED. See "%LOG%"
  type "%LOG%"
  pause
  exit /b 5
)
if not exist "%DEST%" mkdir "%DEST%"
if not exist "%DEST%\dlls" mkdir "%DEST%\dlls"
rem Back up previous build ONCE for easy rollback.
if exist "%DEST%\dlls\LargerMaps.dll" if not exist "%ROOT%artifacts\LargerMaps-before-1.6.1.dll" copy /Y "%DEST%\dlls\LargerMaps.dll" "%ROOT%artifacts\LargerMaps-before-1.6.1.dll" >>"%LOG%" 2>&1
if exist "%DEST%\modpack.json" if not exist "%ROOT%artifacts\modpack-before-1.6.1.json" copy /Y "%DEST%\modpack.json" "%ROOT%artifacts\modpack-before-1.6.1.json" >>"%LOG%" 2>&1
copy /Y "%ROOT%src\LargerMaps\bin\Release\net6.0\LargerMaps.dll" "%DEST%\dlls\LargerMaps.dll" >>"%LOG%" 2>&1
if errorlevel 1 goto DEPLOY_FAILED
copy /Y "%MODSRC%\modpack.json" "%DEST%\modpack.json" >>"%LOG%" 2>&1
if errorlevel 1 goto DEPLOY_FAILED
echo SUCCESS: Larger Maps 1.6.1 (SIMPLIFIED COMBAT AI EXPERIMENT) deployed to "%DEST%"
echo Before playing, back up your save. Look for "simplified large-map combat planning" in Latest.log.
echo AI attack positioning is simplified; the native pathfinder and player movement are unchanged.
echo If the exact native build is incompatible, the mod will use a 64x64 fallback instead.
echo For rollback, quit MENACE and restore DLL + modpack from artifacts\*before-1.6.1*.
pause
exit /b 0

:DEPLOY_FAILED
echo DEPLOY FAILED - see "%LOG%". Restoring original files when backup exists.
if exist "%ROOT%artifacts\LargerMaps-before-1.6.1.dll" copy /Y "%ROOT%artifacts\LargerMaps-before-1.6.1.dll" "%DEST%\dlls\LargerMaps.dll" >>"%LOG%" 2>&1
if exist "%ROOT%artifacts\modpack-before-1.6.1.json" copy /Y "%ROOT%artifacts\modpack-before-1.6.1.json" "%DEST%\modpack.json" >>"%LOG%" 2>&1
type "%LOG%"
pause
exit /b 6
