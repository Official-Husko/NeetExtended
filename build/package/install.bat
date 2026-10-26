@echo off
rem Installs NEET Mods into NEET Demo (Steam, Windows).
rem Run it from the unpacked folder. Extra arguments go to the installer, e.g. --game "D:\Games\NEET Demo".
setlocal
set "HERE=%~dp0"
set "NEET=%HERE%tool\win-x64\neet.exe"

echo NEET Mods installer
echo.
echo This adds the mod loader and the mods to NEET Demo. Close the game first.
echo.

set "FLAGS="
echo Switch the game to 64-bit? It runs a little faster and can use more memory. This downloads
set /p "ANSWER=Unity's 64-bit player (about 180 MB) from unity3d.com. You can switch back later. [y/N] "
if /i "%ANSWER%"=="y" set "FLAGS=--64bit"
if /i "%ANSWER%"=="yes" set "FLAGS=--64bit"

set "ANSWER="
echo Clean up memory in small steps instead of freezing the game for a moment every so often
set /p "ANSWER=(incremental garbage collection)? Recommended. [Y/n] "
if /i not "%ANSWER%"=="n" if /i not "%ANSWER%"=="no" set "FLAGS=%FLAGS% --incremental-gc"

echo.
"%NEET%" install --mods "%HERE%Mods" %FLAGS% %*
if errorlevel 1 (
    echo.
    echo The installation didn't finish. See the message above.
    pause
    exit /b 1
)
echo.
echo Done. Start NEET Demo from Steam; F1 opens the mod menu.
pause
