@echo off
setlocal
set "DOTNET_ROOT=%~dp0.tools\dotnet"
set "DOTNET_ROOT_X64=%DOTNET_ROOT%"
if not exist "%~dp0artifacts\app\GitScope.exe" (
  echo Please run: powershell -File scripts\build.ps1 -Publish
  pause
  exit /b 1
)
start "" "%~dp0artifacts\app\GitScope.exe" %*
