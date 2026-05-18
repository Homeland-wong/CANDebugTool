@echo off
setlocal

set SOLUTION_DIR=%~dp0
set SOLUTION_FILE=%SOLUTION_DIR%CANDebugTool.sln

if not exist "%SOLUTION_FILE%" (
    echo [ERROR] Solution file not found: %SOLUTION_FILE%
    pause
    exit /b 1
)

echo Select build configuration:
echo   [1] Debug
echo   [2] Release
echo.
set /p CHOICE="Enter choice (1 or 2, default=1): "

if "%CHOICE%"=="2" (
    set CONFIG=Release
) else (
    set CONFIG=Debug
)

echo.
echo Building CANDebugTool (%CONFIG%)...
dotnet build "%SOLUTION_FILE%" -c %CONFIG%
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Build succeeded (%CONFIG%).
pause
