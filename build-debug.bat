@echo off
echo ===============================================
echo   The Eye of Cthulhu - Debug Build
echo ===============================================
echo.

set SOLUTION_DIR=%~dp0

echo Building Debug version...
dotnet build "%SOLUTION_DIR%TheEyeOfCthulhu.sln" -c Debug

if errorlevel 1 (
    echo.
    echo [ERROR] Build failed!
    pause
    exit /b 1
)

echo.
echo ===============================================
echo   BUILD SUCCESSFUL!
echo ===============================================
echo.
echo   Output: %SOLUTION_DIR%src\TheEyeOfCthulhu.Lab\bin\Debug\net8.0-windows\
echo.

:: Optionnel: lancer l'application
set /p LAUNCH="Launch application? (Y/N): "
if /i "%LAUNCH%"=="Y" (
    start "" "%SOLUTION_DIR%src\TheEyeOfCthulhu.Lab\bin\Debug\net8.0-windows\TheEyeOfCthulhu.Lab.exe"
)
