NeetExtended
============

Mods for NEET Demo, with their own loader.

Install
-------
1. Close the game.
2. Linux: run install.sh. Windows: run install.bat.
3. Answer the questions (64-bit player, incremental garbage collection, and whether to fetch the
   file Co-op over Steam needs).
4. Start the game from Steam. F1 opens the mod menu.

If the installer can't find the game, pass its folder:
    ./install.sh --game "/path/to/steamapps/common/NEET Demo"
    install.bat --game "D:\SteamLibrary\steamapps\common\NEET Demo"

Running install again updates the mods and keeps your settings.

Co-op over Steam needs steam_api.dll, a file from Valve that the pack can't include. The
installer offers to download it; you can also do it later with the neet tool:
    tool/linux-x64/neet steam          (Windows: tool\win-x64\neet.exe steam)
It is fetched from the Steamworks.NET release on GitHub and checked against a known hash.

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
uninstall.*      takes it out again

Source code, issues and new versions: https://github.com/Official-Husko/NeetExtended
