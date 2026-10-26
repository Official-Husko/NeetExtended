NeetExtended
============

Mods for NEET Demo, with their own loader.

Install
-------
1. Close the game.
2. Linux: run install.sh. Windows: run install.bat.
3. Answer the two questions (64-bit player, incremental garbage collection).
4. Start the game from Steam. F1 opens the mod menu.

If the installer can't find the game, pass its folder:
    ./install.sh --game "/path/to/steamapps/common/NEET Demo"
    install.bat --game "D:\SteamLibrary\steamapps\common\NEET Demo"

Running install again updates the mods and keeps your settings.

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
install.*        installs everything
uninstall.*      takes it out again
