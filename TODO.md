# TODO

Mods and work still to do. Each idea names the game code it builds on (paths in the local decompile, `decompiled/Assembly-CSharp/`). Suggested next: House Rules, Mixtapes, Night Visitors.

## Planned mods

- [ ] **Co-op**: 2 to 4 players in one house. Research done, see [docs/research/coop.md](docs/research/coop.md) (LiteNetLib over UDP, host-authoritative, remote players as player rigs driven by intents).
- [ ] **Twitch Integration**: chat commands and channel point rewards that act in the game. Can reuse Phone Expansion's actions and conditions as the command set ("chat rings the phone", "chat pops the fuses").
- [ ] **Randomizer**: shuffle night visitors, store prices, dream layouts and the like.
- [x] **Performance Fixes** (experimental): `mods/PerformanceFixes`, results in [docs/research/performance.md](docs/research/performance.md#whats-fixed).
  - [x] Peephole camera renders only near the front door.
  - [x] Shadow presets (room lights by default, or none).
  - [x] Save only the paint that changed, in the background.
  - [x] Stop the log spam: kinematic velocity writes, `print("restinput")`, stack traces for warnings.
  - [x] Fix the leaks: paint textures of every earlier house load, peephole render textures, copy textures, fire save subscriptions (in Unofficial NEET Patches).
  - [x] Frame cap and vsync: the game's own settings now stick (Unofficial NEET Patches, `FrameRateSettings`).
  - [x] Skip `PaintTarget.Update` on surfaces with nothing to do.
  - [x] Characters plan their path only when the target moves.
  - [x] Quit without the shutdown crash on 32-bit (in the loader).
  - [x] Optional 64-bit player and incremental GC in the installer.
  - [ ] Fewer allocations per frame: about 11 KB, spread over the character rigs' update code (see "Still open" on the performance page).
  - [ ] Find out where "Coroutine continue failure" comes from (once per session in the house).

## New content

- [ ] **Night Visitors**: new characters for the front door. Nights of visitors already exist (`EncounterSpawner.Encounter`: character, positions, delay, doormat text, `SpawnIfVisitedKnots`). New visitors get their own Ink conversations through the framework's `InkScripts`, and react to offerings in the candy bucket (`Candy_Bucket`, the `OfferingStates` list: sweet, cooked meat, drug and so on) with rewards or consequences.
- [ ] **Mixtapes**: your own MP3/OGG files as cassettes for the radio (`Cassette.musicClip`, `Radio`), sold in the store or found in the house. Same setup as Custom TV Stations: files in any mod's folder, through `ModContent`.
- [ ] **Your Own Pictures**: your images on picture frames, posters and shirts. The game already loads PNG/JPG files from `StreamingAssets/CustomDesigns` (`CustomImageLoader`).
- [x] **Email Expansion**: new emails from JSON or Ink, like phone calls, that can pay money, deliver items or set flags. Emails run on their own Ink story (`EmailHandler`), so `InkScripts` needs to support stories other than the dialogue one.
- [ ] **Email Expansion**: send the game's unused emails (`HateYou` after trading soul for a clone, `LoveYou`, `TaxReminder` when taxes are due). `TaxReminder` uses plain line breaks, so the game's reader shows only its first line; read it with the mod's reader.
- [ ] **Trick-or-Treat Night**: kids ring the doorbell; give candy or get the house egged and TP'd (paint splats). The pieces are in the code (`BrainTrickOrTreater`, `Candy_Bucket`, candy and pumpkin mask items). First check where the demo uses them.

## Gameplay and rules

- [ ] **House Rules**: difficulty settings with presets (Chill, Normal, Hardcore, maybe permadeath). Need drain rates are public statics (`PlayerStats.hungerDecreaseRate` -0.12, `bladderDecreaseRate` -0.06, `energyDecreaseRate` -0.06, `hygieneDecreaseRate` -0.04); also the 12% tax, time between prank calls (275 to 700 s, `Phone_Dispatch`), encounter timers, fire spread and food spoilage.
- [ ] **Achievements**: a viewer, unlock popups and an API for mods to add achievements. The game has a full system (`AchievementManager` with goals and levels, `AchievementViewer`); check how much the demo uses.
- [ ] **Pets**: name the cat, a needs panel, adopt more pets from the store (`BrainCat`, `CatStats`, `CatFoodBowl`, `BrainRat`, `RatStats`).
- [ ] **Stock Market Events**: news-driven crashes and booms, more stocks (`StockMarket`, `Stock`, `StockGraph`). Insider tips by phone through Phone Expansion.
- [ ] **Cooking**: recipes that combine ingredients (flour, dough, tomato, cheese, ...) and a cookbook app on the computer. Food already cooks, burns and spoils (`Food`, see [docs/game/items-usables.md](docs/game/items-usables.md)).

## Tools and quality of life

- [ ] **Photo Mode**: free camera, hidden HUD, paused time, screenshots saved to disk (`CameraControl`, `MaxCamera`, `Orbitcam`, `ZoomCam`). Also save photos from the in-game camera (`PhotoCamera`, `Picture`) as PNG files.
- [ ] **Dream Editor**: add walls, floors, furniture and materials to the dream level, or pick its layout (`DreamRandomizer` builds it from those lists).
- [ ] **Wardrobe**: a mirror menu to change body shape (fat, muscle, body type blend shapes) and clothes.

## Follow-ups for existing mods

- [ ] **Unofficial NEET Patches**: check whether `Spawner.concurrentSpawnIds` (only grows, saved every time) and `Locator.sitRestEnablers` (only grows) cause problems over a long game.
- [ ] **Unofficial NEET Patches**: `PaintTarget.ClearAllPaint(clearMemory: true)` destroys the textures of surfaces that have no saved picture but leaves them marked as set up, so painting on them later draws into destroyed textures. From the code; confirm in the game first.
- [ ] **Phone Expansion**: voicemail (missed calls leave a message to play back).
- [ ] **Phone Expansion**: a phone book app on the computer listing the numbers you've found.
- [ ] **Framework**: adding items to store pages needs a look at the scene hierarchy first (the store builds its buttons in the scene and wires them by index).
- [ ] **Framework**: the computer's app list (`AppDownloader`) is index based too; same check before adding apps to it.
- [ ] **Open House**: confirm in the game that the main menu's third door leads nowhere (going by the scene file it doesn't).
