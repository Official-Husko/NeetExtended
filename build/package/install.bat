@echo off
rem Installs NeetExtended into NEET Demo (Steam, Windows).
rem Run it from the unpacked folder. Extra arguments go to the installer, e.g. --game "D:\Games\NEET Demo".
setlocal
set "HERE=%~dp0"
set "NEET=%HERE%tool\win-x64\neet.exe"

echo NeetExtended installer
echo.
echo This adds the mod loader and the mods to NEET Demo, and switches the game to Unity's 64-bit player
echo (downloaded from unity3d.com the first time, about 180 MB). If you went back to 32-bit with
echo use-32bit.bat, that stays. Close the game first.
echo.

set "FLAGS="
set "ANSWER="
echo Clean up memory in small steps instead of freezing the game for a moment every so often
set /p "ANSWER=(incremental garbage collection)? Recommended. [Y/n] "
if /i not "%ANSWER%"=="n" if /i not "%ANSWER%"=="no" set "FLAGS=--incremental-gc"

set "ANSWER="
set "STEAM="
echo.
echo Co-op over Steam needs two files from Valve (steam_api.dll and steam_api64.dll, about 560 KB)
set /p "ANSWER=that this pack can't include. Download them now from github.com (the Steamworks.NET release)? [y/N] "
if /i "%ANSWER%"=="y" set "STEAM=1"
if /i "%ANSWER%"=="yes" set "STEAM=1"

echo.
"%NEET%" install --mods "%HERE%Mods" %FLAGS% %*
if errorlevel 1 (
    echo.
    echo The installation didn't finish. See the message above.
    pause
    exit /b 1
)
if defined STEAM (
    "%NEET%" steam %*
    if errorlevel 1 echo Couldn't fetch Steam's files. Try again later with: neet steam
)
echo.
echo Done. Start NEET Demo from Steam; F1 opens the mod menu.
echo If the game doesn't start or keeps crashing on 64-bit, run use-32bit.bat to go back to its original
echo 32-bit player.
pause
