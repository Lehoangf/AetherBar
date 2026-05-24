@echo off
title AetherBar Installer
setlocal enabledelayedexpansion

echo ============================================
echo    AetherBar - Taskbar Widget Engine
echo    One-Click Installer
echo ============================================
echo.

:: Check if running as admin
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [WARNING] Not running as administrator.
    echo The app requires admin rights for taskbar embedding.
    echo Right-click and select "Run as administrator" if needed.
    echo.
)

:: Check .NET Runtime
echo [1/4] Checking .NET Runtime...
dotnet --list-runtimes 2>nul | findstr "Microsoft.NETCore.App 8." >nul
if %errorLevel% equ 0 (
    echo   .NET 8 Runtime is installed.
) else (
    echo   .NET 8 Runtime not found. Installing...
    echo   Downloading Windows Desktop Runtime 8.0...
    powershell -Command "& {Invoke-WebRequest -Uri 'https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.7-windows-x64' -OutFile '%TEMP%\dotnet-runtime.exe'; Start-Process -FilePath '%TEMP%\dotnet-runtime.exe' -ArgumentList '/quiet','/norestart' -Wait}"
    if %errorLevel% neq 0 (
        echo   [ERROR] Failed to install .NET Runtime.
        echo   Please install manually from: https://dotnet.microsoft.com/download/dotnet/8.0
    )
)

:: Restore NuGet packages
echo [2/4] Restoring NuGet packages...
dotnet restore AetherBar.slnx
if %errorLevel% equ 0 (
    echo   NuGet packages restored successfully.
) else (
    echo   [ERROR] Failed to restore NuGet packages.
    pause
    exit /b 1
)

:: Build solution
echo [3/4] Building AetherBar...
dotnet build AetherBar.slnx --configuration Release
if %errorLevel% equ 0 (
    echo   Build successful!
) else (
    echo   [ERROR] Build failed. Check errors above.
    pause
    exit /b 1
)

:: Publish self-contained package (optional)
echo [4/4] Creating deployment package...
powershell -ExecutionPolicy Bypass -File "%~dp0publish.ps1" -Configuration Release -OutputDir "%~dp0publish" -NoZip
if %errorLevel% equ 0 (
    echo   Published to: %~dp0publish
) else (
    echo   [WARNING] Publish step failed, but build succeeded.
    echo   You can run directly from the build output.
)

:: Register startup
echo.
set /p STARTUP="Add AetherBar to Windows startup? (Y/N): "
if /i "!STARTUP!"=="Y" (
    reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" ^
        /v "AetherBar" ^
        /t REG_SZ ^
        /d "%~dp0publish\AetherBar.UI.exe" ^
        /f
    echo   Added to startup.
)

:: Launch
echo.
set /p LAUNCH="Launch AetherBar now? (Y/N): "
if /i "!LAUNCH!"=="Y" (
    echo   Launching AetherBar...
    start "" "%~dp0publish\AetherBar.UI.exe"
)

echo.
echo ============================================
echo    Installation complete!
echo ============================================
echo.
echo   Run:       %~dp0publish\AetherBar.UI.exe
echo   Settings:  %%LOCALAPPDATA%%\AetherBar\settings.json
echo.
echo   For a proper Windows installer, install Inno Setup
echo   and compile setup.iss:
echo     iscc "%~dp0setup.iss"
echo.
pause
