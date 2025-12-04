@echo off
setlocal EnableDelayedExpansion

:: Setup script to create symlinks for all _ModContent folders
:: Creates ModContents\<ModName> -> <ModName>\<ModName>\_ModContent
:: NOTE: Requires Administrator privileges or Developer Mode enabled

cd /d "%~dp0.."

:: Remove and recreate ModContents directory
if exist "ModContents" rmdir /s /q "ModContents"
mkdir "ModContents"

:: Process each mod directory
for /d %%M in (*) do (
    if exist "%%M\%%M\_ModContent" (
        set "MOD_NAME=%%M"
        set "TARGET=..\%%M\%%M\_ModContent"
        set "LINK=ModContents\!MOD_NAME!"

        :: Remove existing symlink if present
        if exist "!LINK!" rmdir "!LINK!" 2>nul

        :: Create directory symlink (target is relative to symlink location)
        mklink /D "!LINK!" "!TARGET!"
        echo Created: ModContents\!MOD_NAME! -^> !TARGET!
    )
)

echo.
echo Done!
pause
