@echo off
setlocal enabledelayedexpansion

echo ===============================================
echo   The Eye of Cthulhu - Build Script v1.0
echo ===============================================
echo.

:: Configuration
set VERSION=1.0.0
set SOLUTION_DIR=%~dp0
set BUILD_CONFIG=Release
set INNO_PATH="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

:: Check if Inno Setup is installed
if not exist %INNO_PATH% (
    echo [ERROR] Inno Setup 6 not found at %INNO_PATH%
    echo Please install Inno Setup 6 from https://jrsoftware.org/isdown.php
    pause
    exit /b 1
)

:: Step 1: Clean
echo [1/4] Cleaning previous builds...
dotnet clean "%SOLUTION_DIR%TheEyeOfCthulhu.sln" -c %BUILD_CONFIG% -v q
if errorlevel 1 (
    echo [ERROR] Clean failed!
    pause
    exit /b 1
)
echo      Done.
echo.

:: Step 2: Restore
echo [2/4] Restoring NuGet packages...
dotnet restore "%SOLUTION_DIR%TheEyeOfCthulhu.sln" -v q
if errorlevel 1 (
    echo [ERROR] Restore failed!
    pause
    exit /b 1
)
echo      Done.
echo.

:: Step 3: Build Release
echo [3/4] Building Release version...
dotnet build "%SOLUTION_DIR%TheEyeOfCthulhu.sln" -c %BUILD_CONFIG% -v q --no-restore
if errorlevel 1 (
    echo [ERROR] Build failed!
    pause
    exit /b 1
)
echo      Done.
echo.

:: Step 4: Create Setup
echo [4/4] Creating installer with Inno Setup...
if not exist "%SOLUTION_DIR%setup\output" mkdir "%SOLUTION_DIR%setup\output"
%INNO_PATH% "%SOLUTION_DIR%setup\TheEyeOfCthulhu.iss"
if errorlevel 1 (
    echo [ERROR] Inno Setup failed!
    pause
    exit /b 1
)
echo      Done.
echo.

:: Success
echo ===============================================
echo   BUILD SUCCESSFUL!
echo ===============================================
echo.
echo   Version: %VERSION%
echo   Output:  %SOLUTION_DIR%setup\output\
echo.
echo   Files created:
dir /b "%SOLUTION_DIR%setup\output\*.exe" 2>nul
echo.

pause
