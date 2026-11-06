@echo off
setlocal
cd /d "%~dp0"

if exist "%ProgramFiles%\dotnet\dotnet.exe" set "PATH=%ProgramFiles%\dotnet;%PATH%"
dotnet run --urls http://localhost:5187
