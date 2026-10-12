@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
where py >nul 2>nul
if errorlevel 1 (
  echo ERROR: Python 3 launcher 'py' not found.
  exit /b 2
)
rem Optional: set MENACE_GAMEASSEMBLY to an original GameAssembly.dll.
rem Optional: set MENACE_EXTRACTED to an ExtractedData.zip containing mission templates.
rem Optional: set MENACE_DUMP_CS to a matching dump.cs.
py -3 -m unittest discover -s "%ROOT%tests" -p "test_*.py" -v
if errorlevel 1 exit /b 1
echo All offline source and fixture checks completed.
exit /b 0
