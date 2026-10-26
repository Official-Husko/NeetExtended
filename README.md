# NEET Mods

Mods, a mod loader and modding tools for **NEET Demo** (Steam app 2054460). The game's developer stopped working on it but kept the demo up, so this project keeps it going: a loader that needs no extra launcher, a framework for adding content, an in-game mod menu, and tools for testing.

Not affiliated with the game's developer.

## What's here

| Part | What it does |
| --- | --- |
| **NeetLoader** (`src/NeetLoader`) | Starts with the game through Unity's own startup files. Loads mods from `<game>/Mods`, in dependency order, and can load, unload and hot reload them while the game runs. |
| **NeetModKit** (`src/NeetModKit`) | The framework mods build on: game access, events, items, the anime card game, computer apps, dialogue scripts in the game's own dialogue system, save data, a UI toolkit built from the game's own menus, switchable features, and the mod menu (F1, or MODS in the pause menu). |
| **DevBridge** (`mods/DevBridge`) | A local socket for tools: read and change game state, spawn things, click UI, take screenshots, reload mods. |
| **Open Saves** (`mods/OpenSaves`) | Compressed, documented `.nsav` saves (about 16x smaller) and cleanup of old save files. |
| **No Feedback** (`mods/NoFeedback`) | Stops the pause menu sending feedback reports to the original developer. Always on, like the framework. |
| **Unofficial NEET Patches** (`mods/UnofficialNeetPatches`) | Fixes for bugs in the game itself: saves failing (dead house spiders, a leftover fuse, any one broken object), put-out fires coming back after loading, story events breaking after Grandpa dies or repeating every frame after an error, status effects that freeze, stick or depend on the frame rate, frame rate and vsync settings that don't stick, promoted cards losing their stats. Each fix can be switched off. |
| **Performance Fixes** (`mods/PerformanceFixes`) | Experimental. Saves without freezing (only changed paint, written in the background), frees the video memory the game leaks on every load, runs the front door's peephole camera only nearby, fewer shadowed lights, less log spam, idle paint surfaces and characters that don't redo work every frame. Each fix can be switched off. |
| **Card Expansion** (`mods/CardExpansion`) | Twelve Halloween cards, summons, an auto-clicker and a better crystal exchange for the computer's card game. |
| **Pumpkin Smash** (`mods/PumpkinSmash`) | A whack-a-pumpkin game on the in-game computer. |
| **Open House** (`mods/OpenHouse`) | Opens what the demo closes off: the pantry, the black basement door, dad's yard door, and optionally the attic, the roof and the demo's other limits. |
| **Phone Expansion** (`mods/PhoneExpansion`) | New numbers to dial and new calls that ring in, with choices and consequences: pizza delivery, Spookbusters, a deal with Satan, a call you shouldn't answer. Calls are JSON or Ink files any mod can ship. |
| **Custom TV Stations** (`mods/TvStations`) | Your own videos (MP4 or WebM) on the living room TV, next to or in place of the game's channels. |
| **Cheat Menu** (`mods/CheatMenu`) | Press Insert: god mode, needs, speed, teleport, money, crystals, spawning items and people, fire, power, doors, game speed. |
| **neet** (`tools/NeetTool`) | Command-line tool: install the loader and mods (optionally with a 64-bit player and incremental garbage collection), start and stop the game, read logs, talk to DevBridge, convert saves, build the changelog, and an MCP server. |

## Installing (players)

Get a release package (`NEET-Mods-<version>.zip`), unpack it, close the game and run `install.sh` (Linux) or `install.bat` (Windows). The installer asks whether to switch the game to 64-bit (it downloads Unity's player from Unity) and whether to turn on incremental garbage collection. `uninstall.sh` / `uninstall.bat` put the game back. The package is built with `build/package.sh`.

## Quick start (development)

Needs the .NET 10 SDK and the game installed through Steam.

```sh
dotnet build NeetMods.sln -c Release           # builds everything and copies it into the game
dotnet run --project tools/NeetTool -- install # once: hook the loader into the game
dotnet run --project tools/NeetTool -- launch  # start the game in a window
```

If the game isn't in the default Steam library, set `NEET_GAME_DIR` or create `build/Local.props` with a `<NeetGameDir>`.

With the game running, rebuild and the changed mods reload by themselves within about a second. Press **F1** in game for the mod menu.

To remove everything again: `dotnet run --project tools/NeetTool -- uninstall` (add `--remove-mods` to delete the Mods folder too).

## Docs

- [docs/README.md](docs/README.md) — index
- [Getting started](docs/modding/getting-started.md), including writing your first mod
- [Notes on the game's code](docs/game/) — how NEET's systems work, from the decompiled source

## Plans

[TODO.md](TODO.md) lists the mods that are planned or being considered, and follow-ups for the existing ones.

## Changelog

[changelog.json](changelog.json) is generated from the commit history (`neet changelog --write`) and shown in game on the What's New page.
