@echo off
rem Switches NEET Demo back to its original 32-bit player, in case the 64-bit one gives you trouble.
rem Installing again keeps it that way; use-64bit.bat switches back to 64-bit.
rem Extra arguments go to the tool, e.g. --game "D:\Games\NEET Demo". Close the game first.
setlocal
"%~dp0tool\win-x64\neet.exe" install --32bit %*
pause
