NeetExtended
============

Mods for NEET Demo, with their own loader.

Install
-------
1. Close the game.
2. Linux: run install.sh. Windows: run install.bat.
3. Answer the questions (incremental garbage collection, and whether to fetch the files Co-op
   over Steam needs).
4. Start the game from Steam. F1 opens the mod menu.

The installer switches the game to Unity's 64-bit player, which runs a little faster and can use
more memory. Unity doesn't let mods include it, so it's downloaded from unity3d.com the first time
(about 180 MB) and checked against a known hash. The original 32-bit files are kept. If the
download fails, the game keeps its 32-bit player for now (the mods work on both); running install
again tries again.

If the installer can't find the game, pass its folder:
    ./install.sh --game "/path/to/steamapps/common/NEET Demo"
    install.bat --game "D:\SteamLibrary\steamapps\common\NEET Demo"

Running install again updates the mods and keeps your settings.

Co-op over Steam needs steam_api.dll and steam_api64.dll, files from Valve that the pack can't
include. The installer offers to download them; you can also do it later with the neet tool:
    tool/linux-x64/neet steam          (Windows: tool\win-x64\neet.exe steam)
They are fetched from the Steamworks.NET release on GitHub and checked against known hashes.

32-bit or 64-bit
----------------
If the game doesn't start or keeps crashing with the 64-bit player, close it and run use-32bit.sh
(Windows: use-32bit.bat). That puts the game's original 32-bit player back, and running install
again later keeps it. use-64bit.sh / use-64bit.bat switch to 64-bit again. Both pass extra
arguments on, like --game.

Uninstall
---------
Run uninstall.sh or uninstall.bat. It removes the loader and puts back the original
32-bit player and boot settings. Your mods folder and saves stay; add --remove-mods to
delete the mods too. Steam's "Verify integrity of game files" also restores the game.

What it does
------------
FEATURES.md lists everything the mods add, with their switches and settings.

What's in here
--------------
Mods/            the mods, one folder each
tool/            the installer and modding tool (neet) for Linux and Windows
FEATURES.md      what the mods add
LICENSE          GPL-3.0
install.*        installs everything
use-32bit.*      switches the game back to its original 32-bit player
use-64bit.*      switches it to the 64-bit player again
uninstall.*      takes it out again

Source code, issues and new versions: https://github.com/Official-Husko/NeetExtended
