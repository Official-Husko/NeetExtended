# TODO

Mods and work still to do. Each idea names the game code it builds on (paths in the local decompile, `decompiled/Assembly-CSharp/`). Suggested next: House Rules, Mixtapes, Night Visitors.

## Planned mods

- [ ] **Co-op**: 2 to 4 players in one house (`mods/Coop`, [docs/modding/coop.md](docs/modding/coop.md)). Done: direct-IP host and join, the house sent from memory, player bodies with poses and clothes, bodies kept for returning players, hot reload mid-session, world sync (items, NPCs, doors, money, fire, economy, emails, purchases), conversations and phones through the host, going down and reviving, enemies that pick the nearest player, the hitman line. Everything still missing is listed under [Known limitations](docs/modding/coop.md#known-limitations); keep that list current.
  - [ ] Steam (`mods/CoopSteam`): hosting and the relay work under GE-Proton; still to do: test with a second account, then lobbies, invites, rich presence, auth tickets. Works on both players (64-bit `steam_api64.dll` and 32-bit `steam_api.dll`).
- [ ] **Twitch Integration**: chat commands and channel point rewards that act in the game. Can reuse Phone Expansion's actions and conditions as the command set ("chat rings the phone", "chat pops the fuses").
  - [ ] Show the live chat on the living room TV (its world-space canvas, `HOUSE/FlatScreen/FlatScreen/Canvas`) and as a computer app (`ComputerApps.Register`), with names in their chat colours, badges and emotes (images loaded once with UnityWebRequestTexture and cached). In co-op, the host's chat shows for everyone.
- [x] **Randomizer**: shuffle night visitors, store prices, dream layouts and the like. (Dreams are already random in the game; visitors, prices, deliveries, the house and needs are done.)
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
  - [x] 64-bit player (installed by default; `use-32bit` goes back) and incremental GC in the installer.
  - [ ] Fewer allocations per frame: about 11 KB, spread over the character rigs' update code (see "Still open" on the performance page).
  - [ ] Find out where "Coroutine continue failure" comes from (once per session in the house).

## New content

- [ ] **Night Visitors**: new characters for the front door. Nights of visitors already exist (`EncounterSpawner.Encounter`: character, positions, delay, doormat text, `SpawnIfVisitedKnots`). New visitors get their own Ink conversations through the framework's `InkScripts`, and react to offerings in the candy bucket (`Candy_Bucket`, the `OfferingStates` list: sweet, cooked meat, drug and so on) with rewards or consequences.
- [x] **Mixtapes**: your own MP3/OGG files as cassettes for the radio (`Cassette.musicClip`, `Radio`), sold in the store or found in the house. Same setup as Custom TV Stations: files in any mod's folder, through `ModContent`.
- [x] **Update Checker**: asks GitHub for the newest release at start and every hour, adds a note to the version label, and nags by email and phone (once and twice per save). Guide: [docs/modding/update-checker.md](docs/modding/update-checker.md), with its [Known limitations](docs/modding/update-checker.md#known-limitations).
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

- [ ] **Weather**: storm states over the night (drizzle, normal, heavy, a clear spell), driven by what the house already has: the four rain particle systems (rain also puts out flames in the yard), fog density, the storm loop's volume and the thunder interval (`HouseManager.Thunder`, `noThunder`). Each effect a `ModFeature`: rain makes you wet in the yard (`PlayerStats.wetness`: drips, half burns, worse shocks), lightning flicks the TV to static, crackles the radio and (setting, off by default) pops a fuse. Settings for thunder frequency, rain and storm volume. In co-op the host owns the state (a SkySync message). Needs the thunder fixes below. Notes: [docs/game/world-environment.md](docs/game/world-environment.md#weather--sky). A day and night cycle (the disabled orange sun and `SunRotation`) is possible but large and fights the game's look; not planned.

## Tools and quality of life

- [ ] **Photo Mode**: free camera, hidden HUD, paused time, screenshots saved to disk (`CameraControl`, `MaxCamera`, `Orbitcam`, `ZoomCam`). Also save photos from the in-game camera (`PhotoCamera`, `Picture`) as PNG files.
- [ ] **Dream Editor**: add walls, floors, furniture and materials to the dream level, or pick its layout (`DreamRandomizer` builds it from those lists).
- [ ] **Wardrobe**: a mirror menu to change body shape (fat, muscle, body type blend shapes) and clothes.

## Follow-ups for existing mods

- [x] **Unofficial NEET Patches**: thunder stops for the rest of the session after the story's strike (`HouseManager.cs:345-352` stops the loop, an instant strike never schedules the next, `:520`). Measured: 8 strikes, then none.
- [x] **Unofficial NEET Patches**: the Moon stays at intensity 0 after the first strike (`HouseManager.cs:515`). Restoring it brings the moon's shadow cost back, which Performance Fixes' Moon shadows setting (on by default: only outdoors) now takes care of.
- [x] **Unofficial NEET Patches**: the lightning flash and lamp flicker are per-frame lerps, so they're shorter at high frame rates (`HouseManager.cs:500-514`, `LightSwitch.cs:155-175`).
- [x] **Performance Fixes**: a moon shadow option (on, off, only outdoors), the `MoonShadows` feature. Results in [docs/research/performance.md](docs/research/performance.md#whats-fixed).
- [x] **Framework**: a small `Sky` API (moon/sun, `Strike()`, an `OnStrike` event that survives house loads, the storm volume) and a `weather` rule condition plus `{{weather}}` text for content files (the framework and phone guides already use `Weather.Raining` as an example that doesn't exist).
- [ ] **Dev Bridge**: `sky.get`, `sky.strike`, `sky.set`, `rain.set` (today a strike needs `obj.call ... TriggerSequence ["Execute","Thunder"]`).
- [ ] **Cheat Menu**: strike lightning now, rain and thunder on or off, on the World page.
- [ ] **Unofficial NEET Patches**: check whether `Spawner.concurrentSpawnIds` (only grows, saved every time) and `Locator.sitRestEnablers` (only grows) cause problems over a long game.
- [ ] **Unofficial NEET Patches**: `PaintTarget.ClearAllPaint(clearMemory: true)` destroys the textures of surfaces that have no saved picture but leaves them marked as set up, so painting on them later draws into destroyed textures. From the code; confirm in the game first.
- [ ] **Phone Expansion**: voicemail (missed calls leave a message to play back).
- [ ] **Phone Expansion**: a phone book app on the computer listing the numbers you've found.
- [ ] **Framework**: adding items to store pages needs a look at the scene hierarchy first (the store builds its buttons in the scene and wires them by index).
- [ ] **Framework**: the computer's app list (`AppDownloader`) is index based too; same check before adding apps to it.
- [ ] **Open House**: confirm in the game that the main menu's third door leads nowhere (going by the scene file it doesn't).
