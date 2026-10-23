@echo off
if "%~1"=="" (
  echo Drag a downloaded JBIDS extraction JSON file onto this command.
  pause
  exit /b 1
)
dotnet "%~dp0JBIDS.LocalHelper.dll" "%~f1"
pause
