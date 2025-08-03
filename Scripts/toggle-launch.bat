@echo off
setlocal

set "TOGGLE_FILE=%~dp0no-launch.toggle"

if exist "%TOGGLE_FILE%" (
    del "%TOGGLE_FILE%"
    echo.
    echo ========================================
    echo   Broforce Auto-Launch: ENABLED
    echo   Game will launch after builds
    echo ========================================
    echo.
) else (
    echo. > "%TOGGLE_FILE%"
    echo.
    echo ========================================
    echo   Broforce Auto-Launch: DISABLED  
    echo   Game will NOT launch after builds
    echo ========================================
    echo.
)