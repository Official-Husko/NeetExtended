# TODO

Mods and work still to do. Each idea names the game code it builds on (paths in the local decompile, `decompiled/Assembly-CSharp/`). Suggested next: House Rules, Mixtapes, Night Visitors.

## Planned mods

- [ ] **Co-op**: 2 to 4 players in one house. Research done, see [docs/research/coop.md](docs/research/coop.md) (LiteNetLib over UDP, host-authoritative, remote players as player rigs driven by intents).
- [ ] **Twitch Integration**: chat commands and channel point rewards that act in the game. Can reuse Phone Expansion's actions and conditions as the command set ("chat rings the phone", "chat pops the fuses").
- [ ] **Randomizer**: shuffle night visitors, store prices, dream layouts and the like.
- [ ] **Performance Fixes** (experimental): measured in [docs/research/performance.md](docs/research/performance.md). In order of gain:
  - [ ] Peephole camera renders only while someone looks through it (about 17% of the frame in the living room).
  - [ ] Fewer shadowed point lights, with presets (shadows are about 1,600 of 2,500 draw calls).
  - [ ] Save only the paint that changed (saves freeze the game for 150 to 400 ms, 90% of it paint PNGs).
  - [ ] Stop the log spam: kinematic velocity writes, `print("restinput")`, stack traces for warnings.
  - [ ] Fewer garbage collections (each freezes the game 9 to 17 ms): cut per-frame allocations, collect at natural pauses.
  - [ ] Fix the leaks: peephole render textures, paint textures, fire save subscriptions.
  - [ ] Let the player choose the frame cap and vsync (the game forces 120 fps on every camera start).
  - [ ] Skip `PaintTarget.Update` on surfaces with nothing to do (1,236 calls a frame).
  - [ ] Quit without the shutdown crash on 32-bit.
  - [ ] Optional 64-bit mode in the tool: download Unity's 2022.3.17f1 Windows player and swap it in (works, about 5% faster, no exit crash).

## New content

- [ ] **Night Visitors**: new characters for the front door. Nights of visitors already exist (`EncounterSpawner.Encounter`: character, positions, delay, doormat text, `SpawnIfVisitedKnots`). New visitors get their own Ink conversations through the framework's `InkScripts`, and react to offerings in the candy bucket (`Candy_Bucket`, the `OfferingStates` list: sweet, cooked meat, drug and so on) with rewards or consequences.
- [ ] **Mixtapes**: your own MP3/OGG files as cassettes for the radio (`Cassette.musicClip`, `Radio`), sold in the store or found in the house. Same setup as Custom TV Stations: files in any mod's folder, through `ModContent`.
- [ ] **Your Own Pictures**: your images on picture frames, posters and shirts. The game already loads PNG/JPG files from `StreamingAssets/CustomDesigns` (`CustomImageLoader`).
- [ ] **Email Expansion**: new emails from JSON or Ink, like phone calls, that can pay money, deliver items or set flags. Emails run on their own Ink story (`EmailHandler`), so `InkScripts` needs to support stories other than the dialogue one.
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

- [ ] **Unofficial NEET Patches**: amplifier status effects stay subscribed to their stat after they end (`StatusHandler.AddEffect` / `RemoveEffect`), so they keep amplifying and their cost grows every time one is applied.
- [ ] **Phone Expansion**: voicemail (missed calls leave a message to play back).
- [ ] **Phone Expansion**: a phone book app on the computer listing the numbers you've found.
- [ ] **Framework**: adding items to store pages needs a look at the scene hierarchy first (the store builds its buttons in the scene and wires them by index).
- [ ] **Framework**: the computer's app list (`AppDownloader`) is index based too; same check before adding apps to it.
- [ ] **Open House**: confirm in the game that the main menu's third door leads nowhere (going by the scene file it doesn't).
