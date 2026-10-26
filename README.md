# NEET Mods

Mods, a mod loader and modding tools for **NEET Demo** (Steam app 2054460). The game's developer stopped working on it but kept the demo up, so this project keeps it going: a loader that needs no extra launcher, a framework for adding content, an in-game mod menu, and tools for testing.

Not affiliated with the game's developer.

## What's here

| Part | What it does |
| --- | --- |
| **NeetLoader** (`src/NeetLoader`) | Starts with the game through Unity's own startup files. Loads mods from `<game>/Mods`, in dependency order, and can load, unload and hot reload them while the game runs. |
| **NeetModKit** (`src/NeetModKit`) | The framework mods build on: game access, events, items, the anime card game, computer apps, save data, a UI toolkit built from the game's own menus, switchable features, and the mod menu (F1, or MODS in the pause menu). |
| **DevBridge** (`mods/DevBridge`) | A local socket for tools: read and change game state, spawn things, click UI, take screenshots, reload mods. |
| **Open Saves** (`mods/OpenSaves`) | Compressed, documented `.nsav` saves (about 16x smaller) and cleanup of old save files. |
| **No Feedback** (`mods/NoFeedback`) | Stops the pause menu sending feedback reports to the original developer. |
| **Card Expansion** (`mods/CardExpansion`) | Twelve Halloween cards, summons, an auto-clicker, a better crystal exchange and a fix for promoted cards in the computer's card game. |
| **Pumpkin Smash** (`mods/PumpkinSmash`) | A whack-a-pumpkin game on the in-game computer. |
| **neet** (`tools/NeetTool`) | Command-line tool: install the loader, start and stop the game, read logs, talk to DevBridge, convert saves, build the changelog, and an MCP server. |

## Quick start

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

## Changelog

[changelog.json](changelog.json) is generated from the commit history (`neet changelog --write`) and shown in game on the What's New page.
