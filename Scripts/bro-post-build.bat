@echo off
rem Unused variables are here for potential future changes
set "ProjectName=%~1"
set "TargetPath=%~2"
set "TargetDir=%~3"
set "TargetFileName=%~4"
set "TargetName=%~5"
set "Configuration=%~6"
set "Platform=%~7"
set "ProjectDir=%~8"
set "SolutionDir=%~9"
shift
set "OutDir=%~9"

del "%BROFORCEPATH%\BroMaker_Storage\%ProjectName%\*.cache"
XCOPY /Y /R "%TargetPath%" "%BROFORCEPATH%\BroMaker_Storage\%ProjectName%\%ProjectName%.dll*"