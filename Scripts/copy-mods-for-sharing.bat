@echo off
setlocal enabledelayedexpansion

REM Copy mods from game directory to temp folder and upload to Google Drive
echo Preparing mods for upload to Google Drive...
echo.

set "SOURCE=C:\Users\Alex\AppData\Roaming\r2modmanPlus-local\Broforce\profiles\Default\UMM"
set "DEST=%TEMP%\Broforce_Upload"

REM Create destination folders if they don't exist
if not exist "%DEST%\Mods" mkdir "%DEST%\Mods"
if not exist "%DEST%\BroMaker_Storage" mkdir "%DEST%\BroMaker_Storage"

REM Define which mods to copy
set MODS_TO_COPY=BroMaker "Control Enemies Mod" "Randomizer Mod" RocketLib "Swap Bros Mod" "Utility Mod"
set BROS_TO_COPY=Brostbuster "Captain Ameribro" "Drunken Broster" Furibrosa "Mission Impossibro" RJBrocready

REM Copy Mods folders
echo Copying Mods...
for %%F in (%MODS_TO_COPY%) do (
    set "FOLDER=%%~F"
    if exist "%SOURCE%\Mods\!FOLDER!" (
        echo   - Copying !FOLDER!
        robocopy "%SOURCE%\Mods\!FOLDER!" "%DEST%\Mods\!FOLDER!" /MIR /NFL /NDL /NJH /NJS
    ) else (
        echo   - WARNING: !FOLDER! not found in game directory
    )
)

echo.
echo Copying BroMaker_Storage...
for %%F in (%BROS_TO_COPY%) do (
    set "FOLDER=%%~F"
    if exist "%SOURCE%\BroMaker_Storage\!FOLDER!" (
        echo   - Copying !FOLDER!
        robocopy "%SOURCE%\BroMaker_Storage\!FOLDER!" "%DEST%\BroMaker_Storage\!FOLDER!" /MIR /NFL /NDL /NJH /NJS
    ) else (
        echo   - WARNING: !FOLDER! not found in game directory
    )
)

echo.
echo Cleaning up files not needed for sharing...

REM Delete dll.cache files (including numbered cache files like .dll.12345.cache)
echo   - Removing dll.cache files
del /S /Q "%DEST%\*.dll.*.cache" 2>nul

REM Delete Settings.json files
echo   - Removing Settings.json files
del /S /Q "%DEST%\Settings.json" 2>nul

REM Delete Settings.xml files
echo   - Removing Settings.xml files
del /S /Q "%DEST%\Settings.xml" 2>nul

REM Delete Settings.xml.backup files
echo   - Removing Settings.xml.backup files
del /S /Q "%DEST%\Settings.xml.backup" 2>nul

REM Delete Keybindings.json files
echo   - Removing Keybindings.json files
del /S /Q "%DEST%\Keybindings.json" 2>nul

REM Delete .bat files
echo   - Removing .bat files
del /S /Q "%DEST%\*.bat" 2>nul

REM Delete specific BroMaker unlock progress file
if exist "%DEST%\Mods\BroMaker\BroMaker_UnlockProgress.json" (
    echo   - Removing BroMaker_UnlockProgress.json
    del /Q "%DEST%\Mods\BroMaker\BroMaker_UnlockProgress.json"
)

REM Delete Utility Mod Profiles folder
if exist "%DEST%\Mods\Utility Mod\Profiles" (
    echo   - Removing Utility Mod Profiles folder
    rmdir /S /Q "%DEST%\Mods\Utility Mod\Profiles"
)

echo.
echo Creating Broforce.zip...
set "ZIPFILE=%TEMP%\Broforce.zip"
powershell -Command "Compress-Archive -Path '%DEST%\*' -DestinationPath '%ZIPFILE%' -Force"

if %ERRORLEVEL% EQU 0 (
    echo Zip created successfully. Uploading to Google Drive...
    rclone copy "%ZIPFILE%" "Google Drive:Broforce/" --progress

    if %ERRORLEVEL% EQU 0 (
        echo.
        echo Upload successful! Cleaning up temp files...
        rmdir /S /Q "%DEST%"
        del /Q "%ZIPFILE%"
        echo.
        echo Done! Broforce.zip uploaded to Google Drive under Broforce/
    ) else (
        echo.
        echo ERROR: Upload failed. Temp files not deleted.
        echo   Temp folder: %DEST%
        echo   Zip file: %ZIPFILE%
    )
) else (
    echo.
    echo ERROR: Failed to create zip file. Temp folder not deleted: %DEST%
)

pause
