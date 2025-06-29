@echo off
setlocal EnableDelayedExpansion

echo Creating links for all mods and bros...
echo.

REM Get the current directory (Releases folder)
set "RELEASES_DIR=%~dp0"

REM Loop through all subdirectories in the Releases folder
for /D %%F in ("%RELEASES_DIR%*") do (
    set "FOLDER_NAME=%%~nxF"
    set "FOLDER_PATH=%%F"
    
    REM Skip if the inner folder doesn't exist
    if exist "!FOLDER_PATH!\!FOLDER_NAME!" (
        REM Check if it's a bro (has .mod.json file)
        set "IS_BRO=0"
        set "IS_MOD=0"
        
        REM Check for .mod.json files (bros)
        for %%J in ("!FOLDER_PATH!\!FOLDER_NAME!\*.mod.json") do (
            set "IS_BRO=1"
        )
        
        REM Check for Info.json file (mods)
        if exist "!FOLDER_PATH!\!FOLDER_NAME!\Info.json" (
            set "IS_MOD=1"
        )
        
        REM Create appropriate link based on type
        if !IS_BRO!==1 (
            set "TARGET_DIR=%BROFORCEPATH%\BroMaker_Storage\!FOLDER_NAME!"
            
            REM Check if path exists
            if exist "!TARGET_DIR!" (
                REM Check if it's a symlink using dir attributes
                set "IS_SYMLINK=0"
                for /f "tokens=*" %%A in ('dir /AL /B "%BROFORCEPATH%\BroMaker_Storage" 2^>nul ^| findstr /I /C:"!FOLDER_NAME!"') do (
                    set "IS_SYMLINK=1"
                )
                
                if !IS_SYMLINK!==0 (
                    echo WARNING: Non-symlink folder already exists for bro: !FOLDER_NAME!
                    echo          Please remove or rename the existing folder at: !TARGET_DIR!
                )
            ) else (
                echo Creating bro link: !FOLDER_NAME!
                mklink /D "!TARGET_DIR!" "!FOLDER_PATH!\!FOLDER_NAME!"
                if errorlevel 1 (
                    echo WARNING: Failed to create link for bro: !FOLDER_NAME!
                )
            )
        ) else if !IS_MOD!==1 (
            set "TARGET_DIR=%BROFORCEPATH%\mods\!FOLDER_NAME!"
            
            REM Check if path exists
            if exist "!TARGET_DIR!" (
                REM Check if it's a symlink using dir attributes
                set "IS_SYMLINK=0"
                for /f "tokens=*" %%A in ('dir /AL /B "%BROFORCEPATH%\mods" 2^>nul ^| findstr /I /C:"!FOLDER_NAME!"') do (
                    set "IS_SYMLINK=1"
                )
                
                if !IS_SYMLINK!==0 (
                    echo WARNING: Non-symlink folder already exists for mod: !FOLDER_NAME!
                    echo          Please remove or rename the existing folder at: !TARGET_DIR!
                )
            ) else (
                echo Creating mod link: !FOLDER_NAME!
                mklink /D "!TARGET_DIR!" "!FOLDER_PATH!\!FOLDER_NAME!"
                if errorlevel 1 (
                    echo WARNING: Failed to create link for mod: !FOLDER_NAME!
                )
            )
        )
    )
)

echo.
echo Link creation complete.
pause