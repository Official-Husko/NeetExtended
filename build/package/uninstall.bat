@echo off
rem Takes NeetExtended out of NEET Demo again: the loader, the 64-bit player and the boot setting.
rem The Mods folder stays unless you pass --remove-mods.
setlocal
"%~dp0tool\win-x64\neet.exe" uninstall %*
pause
