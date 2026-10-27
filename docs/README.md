# Docs

## Modding

| Guide | Covers |
| --- | --- |
| [Getting started](modding/getting-started.md) | Building, installing, the edit-build-reload loop, writing your first mod |
| [Loader](modding/loader.md) | `mod.json`, mod lifecycle, hot reload, logging, runtime control |
| [Framework](modding/framework.md) | NeetModKit: game access, events, dialogue and the email story, shared conditions/actions/flags, items, cards, saves, features, settings, content folders |
| [UI and the mod menu](modding/ui-and-menu.md) | The UI toolkit, themes, menu pages, settings pages |
| [Computer apps](modding/computer-apps.md) | The in-game computer's apps, URLs, adding new apps and games |
| [Phone calls](modding/phone.md) | The Phone Expansion mod: call files, conditions, actions, Ink calls, the C# API |
| [Emails](modding/email.md) | The Email Expansion mod: email files, replies, triggers, Ink emails, the C# API |
| [Writing style](modding/writing-style.md) | Writing calls, emails and other game text in NEET's voice |
| [TV stations](modding/tv-stations.md) | The Custom TV Stations mod: video files, stations.json, the remote |
| [Save format](modding/save-format.md) | The `.nsav` format and the Open Saves mod |
| [Dev bridge](modding/dev-bridge.md) | Controlling the running game from tools and scripts |
| [Tools](modding/tools.md) | The `neet` command-line tool, the changelog and the MCP server |

## The game

Notes on how NEET Demo works, written from its decompiled code. Paths like `decompiled/Assembly-CSharp/HouseManager.cs` refer to a local decompile (`ilspycmd -p -o decompiled/Assembly-CSharp <game>/NEET_Data/Managed/Assembly-CSharp.dll`), which is not part of this repository.

| Note | Covers |
| --- | --- |
| [Core systems](game/core-systems.md) | HouseManager, scenes, rooms, sequences, spawners, registries |
| [Save system](game/save-system.md) | How the game saves and loads, save data types |
| [Hooks index](game/hooks-index.md) | Singletons, registries, events, enums and other extension points |
| [Player](game/player.md) | Stats, needs, status effects, abilities, movement, camera |
| [Items](game/items-usables.md) | Usables, food, liquids, tools, wearables |
| [AI](game/ai-brains.md) | NPC brains and behaviour trees |
| [Computer and apps](game/computer-and-apps.md) | The computer, the anime card game, stores, email, stocks, taxes |
| [World](game/world-environment.md) | Doors, fire, power, physics, misc objects |
| [Dialogue and rendering](game/dialogue-and-rendering.md) | Ink dialogue, paint and decals, rendering helpers |
| [Third-party libraries](game/third-party-libs.md) | The in-game console and the other bundled libraries |
| [Known bugs](game/known-bugs.md) | Bugs in the game itself, why they happen, and how Unofficial NEET Patches fixes them |

## Research

- [Co-op](research/coop.md) — what a 2-4 player co-op mod would take
- [Performance](research/performance.md) — why the game feels laggy (measured), what a Performance Fixes mod should do, and running it as 64-bit
