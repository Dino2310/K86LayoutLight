@echo off
setlocal
pushd "%~dp0"
if errorlevel 1 exit /b 1

where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: Install .NET 8 SDK or newer to build this utility.
    set "buildResult=1"
    goto finish
)

echo Building project: "%~dp0K86LayoutLight.csproj"
echo Mode: small single EXE for Windows x64, .NET Desktop Runtime 8 required.
dotnet publish "%~dp0K86LayoutLight.csproj" -c Release -p:PublishProfile=Compact --nologo --verbosity quiet
set "buildResult=%errorlevel%"
if not "%buildResult%"=="0" goto finish

echo.
if not exist "%~dp0Ready\K86LayoutLight.exe" (
    echo ERROR: Published EXE was not created.
    set "buildResult=1"
    goto finish
)

echo Ready: "%~dp0Ready\K86LayoutLight.exe"
echo Copy only this EXE. Install .NET Desktop Runtime 8 x64 on the target PC.
if /i not "%~1"=="--no-pause" start "" explorer.exe "%~dp0Ready"

:finish
if not "%buildResult%"=="0" echo Build failed. See the error above.
popd
if /i not "%~1"=="--no-pause" pause
exit /b %buildResult%
