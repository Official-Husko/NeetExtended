@echo off
rem Switches NEET Demo to Unity's 64-bit player (what the installer sets up), after use-32bit.bat.
rem The first time it downloads the player from unity3d.com (about 180 MB).
rem Extra arguments go to the tool, e.g. --game "D:\Games\NEET Demo". Close the game first.
setlocal
"%~dp0tool\win-x64\neet.exe" install --64bit %*
pause
