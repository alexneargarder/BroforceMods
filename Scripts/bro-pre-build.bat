@echo off
set "ProjectName=%~1"
set "Configuration=%~2"
set "Platform=%~3"
set "ProjectDir=%~4"
set "TargetPath=%~5"
set "CloseBroforceOnBuild=%~6"

if /i "%CloseBroforceOnBuild%"=="true" (
    taskkill /f /t /im Broforce_beta.exe 2> nul || set errorlevel=0
)