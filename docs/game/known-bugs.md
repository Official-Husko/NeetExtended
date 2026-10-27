# Known bugs in the game

Bugs in NEET Demo itself, found while modding, with their cause and the fix. Fixes live in the Unofficial NEET Patches mod (`mods/UnofficialNeetPatches`), one switchable feature per bug, so each can be turned off on the Mods page to check the game's own behaviour. Paths refer to the local decompile (`decompiled/Assembly-CSharp/`).

New bugs go here first, then into the patches mod as a new feature. When a fix catches a problem in play (a story event it stops or skips), it adds it to the error report (`NeetLoader/logs/errors.log`, the mod menu's Errors tab) with where it happened, so it can be reported.

| Bug | Fix (feature id) |
| --- | --- |
| Saving fails with a dead house spider around | `SpiderSaves` |
| Story events can't find a character whose first body is gone | `LostCharacters` |
| A timed story event that errors repeats every frame | `StuckSequences` |
| Promoted cards lose their doubled stats after a load | `PromotedCards` |
| Status effects freeze while a newer one runs | `StatusTimers` |
| Crouch and weight slowdowns get stuck after a load | `StuckSlowdowns` |
| Lasting effects are stronger at higher frame rates | `EffectStrength` |
| Put-out fires and replaced fuses stay in the save | `GhostSaves` |
| One object that can't be saved stops all saving | `SaveCrashes` |
| Frame rate and vsync settings don't stick | `FrameRateSettings` |
| The inbox is empty after loading a save | `LostInbox` |
| Spam emails come again after every load | `RepeatingSpam` |
| The spam timer starts over after loading | `SpamTimer` |
| Spam comes twice as often after loading | `DoubleSpam` |
| Poopman never says his hurt and disgusted lines | `KnotNames` |
| Grandpa is sent to a routine he doesn't have | `SegmentLines` |
| Market orders that don't go through can still bring the cops | `FailedOrderCops` |
| Custom cassettes cost 10$ more than shown | `CassettePrice` |
| The last achievement has no spot on the medal shelf | `LastMedal` |
| The money platinum medal reads another medal's progress | `MoneyMedal` |
| Picking up the thief's beanie throws and leaves a broken hand | `WigPickup` |
| Characters pick up things that belong to switched-off objects | `HiddenPickups` |
| No autosave after loading a save | `AutosaveAfterLoad` |
| Timed spawners stop after loading a save | `SpawnersAfterLoad` |
| Worn clothes fall out of the world after loading | `WornClothes` |

## Saving fails with a dead house spider around

**What happens:** every save throws `InvalidCastException: Specified cast is not valid.` while a house spider's body is in the scene. When this happens during death (`LoadSceneOnDeath.SwitchScene` saves first), the scene switch never happens either, and the player is left at 0 health in the house.

**Cause:** `Brain.SaveBasicBrain` (`Brain.cs:97-107`) saves the creature's other saveable components with the filter `x => x != (ISaveable)this && x.GetType() != typeof(Usable)`. Every brain class implements `ISaveable` except `Spider_House`, so the cast throws for spiders. The filter only runs for components that exist, and a living spider has none; `Spider_House.Die` (`Spider_House.cs:144`) adds a `BreakOnImpact` (which is `ISaveable`) to the body. Anything saveable parented to a living spider triggers it too.

**Fix:** a transpiler on the compiler-generated filter (`Brain.<SaveBasicBrain>b__14_0`, the only `castclass ISaveable` in the game) turns the cast into `isinst`. The filter keeps its meaning: a brain that isn't `ISaveable` can't be one of its own saveable components.

**Reproduce:** spawn `Spider_House`, kill it (or parent an item to it), then save.

## Story events can't find a character whose first body is gone

**What happens:** after Grandpa dies (or any character's object is destroyed), story events that address him by name throw `NullReferenceException` in `Dialogue.SwitchSegmentLine`. The thief's arrival (`SeqSpawnThiefIntro`) is one: it then runs again every frame (see the next bug), breaking the lounge window, enabling the thief and destroying the silhouette over and over, and flooding the log.

**Cause:** `HouseManager.characterStoriesDict` maps character names to their `CharacterStories`. `CharacterStories.Start` (`CharacterStories.cs:222-245`) only adds a name that isn't in the map yet, and nothing removes entries. Once the first object is destroyed, the entry points at a destroyed object, and a respawned copy logs "Duplicate characterstories" and isn't added. `Dialogue.SwitchSegmentLine` and `SetOverrideKnot` (`Dialogue.cs:345-369`) read the map without checking; `SwitchSegmentLine` also reads the brain before its own null check.

**Fix:** a prefix on `CharacterStories.Start` drops an entry whose object is gone, so the new copy registers. When the fix is switched on and whenever the house loads, entries whose object is gone are pointed at a living copy (or removed). Prefixes on `SwitchSegmentLine` and `SetOverrideKnot` skip the event, with one warning per character, when the character isn't there.

**Reproduce:** kill Grandpa, then call `Dialogue._.SwitchSegmentLine("Grandpa_NPC", "Thief_Intro")`, or spawn `Grandpa_NPC` and look for the "Duplicate characterstories" line.

## A timed story event that errors repeats every frame

**What happens:** a `TimerSequence` whose `RunSequence` throws runs again on the next frame, forever, repeating everything it did before the error.

**Cause:** `TimerSequence.Update` (`TimerSequence.cs:47-57`) calls `RunSequence()` and only then sets `active = false`.

**Fix:** a finalizer on `TimerSequence.Update` clears `active` when the sequence threw, and puts the error in the error report with the night and the player's room instead of letting it through.

## Promoted cards lose their doubled stats after a load

**What happens:** promoting a card in the computer's card game doubles its base income and price, but after loading a save the base values are back to the starting ones. The current income still looks right until the next upgrade recalculates it from the base.

**Cause:** `AnimeCard.PromoteCard` (`AnimeCard.cs:257-275`) doubles `initIncome` and `initPrice`, but `AnimeCard.Save` (`:170-173`) doesn't write them.

**Fix:** the patches mod keeps every card's base values (income, price, exp) in its own save data and puts them back after a load. This fix used to be part of Card Expansion.

## Status effects freeze while a newer one runs

**What happens:** a status effect (food, pills, diarrhea, the crouch and weight slowdowns) only counts down while it's the newest one. Taking a pill and then crouching freezes the pill until you stand up; while you're heavy, everything taken before stays on for good.

**Cause:** `StatusHandler.Update` (`StatusHandler.cs:148-189`) walks the effects from the newest, but only lowers `durationTimer` when `effects.Count - 1 == num` (`:175-178`), a check that was probably meant to make sure the effect is still in the list. It's only true for the last one. Crouching and being heavy add slowdowns that last 99,999 seconds, so anything older waits that long.

**Fix:** a prefix replaces the update with one that counts every effect down and removes it when it ends. `AddEffect` makes a copy of the effect each time; ended copies are destroyed, which also lets `Player_Movement` add its weight slowdown again later (it only does that once its old copy compares equal to null).

**Reproduce:** `status.add` two effects and watch `status.player`: only the newer one's `durationLeft` goes down.

## Crouch and weight slowdowns get stuck after a load

**What happens:** saving while crouched (or heavy) leaves the player at 80% speed after loading, for good.

**Cause:** saves store the player's effects, slowdowns included (`StatusHandler.Load`, `:74`). `Player_Movement` adds and removes those slowdowns through its own references (`Player_Movement.cs:150-158`, `:222-227`). After a load it doesn't own the loaded copies, so it never removes them, and it can't add its own since they don't stack.

**Fix:** a postfix on `StatusHandler.Load` drops loaded copies of the crouch and weight slowdowns. Movement adds them again on the next frame if they still apply.

**Reproduce:** crouch, save, reload: the speed stays low after standing up.

## Lasting effects are stronger at higher frame rates

**What happens:** effects that change a stat for as long as they run (diarrhea draining the bladder, for one) work 4 times as fast at 120 fps as at 30 fps.

**Cause:** persistent effects with the `linearDelta` operation apply their full size on every frame (`StatusEffect.OnTick`, `StatusEffect.cs:75`, `:97`), without `Time.deltaTime`.

**Fix:** a prefix on `OnTick` applies those effects per second, scaled so they match what they did at 120 fps, the rate the game caps itself at. Measured: 25 a second at both 120 and 30 fps, against 13 and 6 before.

## Put-out fires and replaced fuses stay in the save

**What happens:** a room fire you put out comes back as a fire entry in every later save. Each load starts it again for a moment, with the smoke alarms. Separately, a save can fail with `NullReferenceException` in `Usable.Save` for the rest of the session after a load, so nothing gets saved (dying and sleeping save too).

**Cause:** everything saveable subscribes to `SaveLoadHandler.OnSave`, and nothing checks whether a subscriber still exists.

- `FireSpreader` subscribes in `Start` (`FireSpreader.cs:100-105`) and never unsubscribes. `Clear` (`:240-252`) destroys the spreader, but its `Save` keeps writing the room with no flames. On load, `LoadFireSpreaders` (`SaveLoadHandler.cs:468-481`) calls `Locator.StartRoomFire` for that entry, which makes a new spreader, starts a fire (it ignores its `noStarterSphere` parameter) and raises the room's `OnFireStart`, which rings the smoke alarms (`SmokeDetector.cs:122-124`). The empty fire then goes out, is cleared, and the cycle repeats.
- `Usable` unsubscribes in `OnDestroy` (`Usable.cs:249-253`), which Unity only calls on objects that were active at some point. `SmokeDetector.Start` calls `Start` on the fuse inside it (`SmokeDetector.cs:132-135`), which subscribes that fuse while it sits inactive in the detector. When a save loads, `LoadUsables` destroys the house's own items to put the saved ones in their place; the fuse's `OnDestroy` never runs, and its `Save` throws from then on (`name` of a destroyed object).

**Fix:** a prefix on `AllSaveDataToJSon` unsubscribes every subscriber whose object is destroyed before the game calls them, and notes what it left out in the error report. A postfix on `FireSpreader.Clear` unsubscribes the spreader right away.

**Reproduce:** `fire.start` a room, put it out (or call `KillAllParticles` on its `FireSpreaderSphere`s until the spreader clears), save: the save has a `FireSpreader` entry with no `subSaveData`. `save.check` on the bridge lists destroyed subscribers; a destroyed `Usable Fuse` shows up right after loading.

## One object that can't be saved stops all saving

**What happens:** if any one saveable object throws in its `Save`, the whole save fails, and since the object stays around, every save after it fails too until the game restarts.

**Cause:** `AllSaveDataToJSon` calls every subscriber through `Delegate.DynamicInvoke` (`SaveLoadHandler.cs:90-97`) with nothing around it.

**Fix:** a transpiler routes each call through a wrapper that calls the delegate directly (no reflection) and, if it throws, leaves that object out of the save, logs a warning and adds it to the error report. The specific bugs above still have their own fixes, so the objects involved are saved properly; this one is the safety net for the ones nobody has found yet.

**Reproduce:** with `SpiderSaves` off, kill a house spider and save.

## Frame rate and vsync settings don't stick

**What happens:** the settings menu's frame rate and vsync choices are saved and applied when the menu loads, but the game runs at 120 fps without vsync again as soon as the house loads, and again whenever a character appears.

**Cause:** `CameraControl.Start` sets `QualitySettings.vSyncCount = 0` and `Application.targetFrameRate = 120` (`CameraControl.cs:102-103`). Every character rig has a `CameraControl`, and `BrainNeetAI` calls `Start` on its camera again (`BrainNeetAI.cs:239`). The menu (`Feedback_Sender`) saves the choices in PlayerPrefs as `VerticalSync` and `Framerate` (an index into its dropdown).

**Fix:** a transpiler on `CameraControl.Start` keeps the game's values only for players who never picked anything: with a saved `VerticalSync` that value is used, and with a saved `Framerate` the frame rate the menu applied is left alone.

**Reproduce:** turn vsync on in the settings, then check `QualitySettings.vSyncCount` after the house loads (`type.get`), or call `Start` on `Player/Main Camera`'s `CameraControl`.

## The inbox is empty after loading a save

**What happens:** after loading a save, the computer's email app shows no emails, only ones that arrive afterwards. The emails are still in the save and in `EmailHandler.deliveredEmails`.

**Cause:** `EmailApp` rebuilds the inbox in `CreateAllDelivered`, which it subscribes to `EmailHandler.OnPostLoad` in its own `Start` (`EmailApp.cs:14-22`). The save is loaded from `SaveLoadHandler.Start`, which runs before `EmailApp.Start`, so `OnPostLoad` has no subscriber when `EmailHandler.Load` raises it.

**Fix:** a postfix on `EmailApp.Start` calls `CreateAllDelivered` when there are delivered emails but no inbox buttons yet, and `UpdateInbox` for emails still pending.

**Reproduce:** with `LostInbox` off, get a few emails, save, and load the save (or `scene.load NEET_House`); the inbox is empty while `email.lists` shows them as delivered.

## Spam emails come again after every load

**What happens:** each of the five spam emails is meant to arrive once per game, but after loading a save they can all arrive again, so the inbox fills with copies.

**Cause:** `SendSpam` marks sent emails off in `randomEmailsInstance`, a copy of the `randomEmails` list made in `Awake`. `EmailHandler.Save` writes `randomEmails`, the untouched asset (`EmailHandler.cs:62-65`), and `Load` restores that into `randomEmailsInstance`, so nothing is marked as sent anymore.

**Fix:** a postfix on `EmailHandler.Save` puts `randomEmailsInstance` into the save data instead.

**Reproduce:** with `RepeatingSpam` off, let a spam email arrive, save, load, and check the random list with `email.lists`: every entry is `accessible` again.

## The spam timer starts over after loading

**What happens:** spam emails come every 200 to 550 seconds, but after loading a save the next one always arrives 15 seconds in.

**Cause:** `EmailHandler.Save` passes the time left to `SaveDataEmailHandler`, whose constructor never stores it (`EmailHandler.cs:23-29`), so the save always says 0. `Load` starts `SendSpam()` from the beginning, which waits 15 seconds and sends.

**Fix:** a postfix on `Save` stores the time left; a postfix on `Load` restarts `SendSpam` with that wait (minus the 15 seconds `SendSpam` adds before sending).

**Reproduce:** with `SpamTimer` off, note `timeToNextRandom` on the `EmailHandler` (`obj.get` on `House_Manager---------------`) and `Time.time`, save, load and compare.

## Spam comes twice as often after loading

**What happens:** after loading a save, spam emails arrive about twice as often for the rest of the session.

**Cause:** `EmailHandler.Load` starts the `SendSpam` coroutine (`EmailHandler.cs:96`). The save is loaded before `EmailHandler.Start` runs, and `Start` starts a second one (`EmailHandler.cs:115-119`), keeping only that one in `sendRandomCoroutine`. Both run.

**Fix:** a prefix and postfix on `EmailHandler.Start`: when the load already started one, the coroutine `Start` started is stopped, and the load's coroutine and its timer are kept.

**Reproduce:** with `DoubleSpam` off, load a save and watch `timeToNextRandom`: it jumps between two schedules as both coroutines write it.

## Poopman never says his hurt and disgusted lines

**What happens:** hitting a visitor or throwing something gross at them makes them react, if they have lines for it. Poopman has both ("You are weak.", "Oooohhh yessssss..."), but never says them.

**Cause:** `CharacterStories.OnCollisionEnter` asks for `characterName + "Harm"` / `"Disgust"` (`CharacterStories.cs:212,217`). His `characterName` is `Poopman`, the knots are `PoopManHarm` and `PoopManDisgust`. `ChoosePathString` fails and `Dialogue.InitCharacterDialogue` swallows the error for Harm/Disgust knots (`Dialogue.cs:410-416`).

**Fix:** a prefix on `Dialogue.InitCharacterDialogue`: when the knot asked for doesn't exist, one whose name only differs in upper and lower case is used.

**Reproduce:** with `KnotNames` off, spawn Poopman (`npcs.spawn`) and call `InitCharacterDialogue(poopman, "Poopman_Harm", "PoopmanHarm", true, true)` on `DialogueCanvas`: nothing. With it on: "You are weak."

## Grandpa is sent to a routine he doesn't have

**What happens:** when the bad thief's kill timer runs out, the game switches Grandpa to the routine `Thief_Intro_Blackout`. His routines are `Init`, `Thief_Intro` and `Blackout`. Looking the routine up throws, right away (`InitSegments`) and again whenever his behaviour tree reaches a target (`SetDestinationReachedRot`, `BrainNeetAI.cs:649`).

**Cause:** the switch comes from scene data (the thief's `OnDelayedKill` event calls `Dialogue.SwitchSegmentLine("Grandpa_NPC@Thief_Intro_Blackout")`), and `SwitchSegmentLine` doesn't check the name; `GetCurrentSegmentLine` uses `Single` (`BrainNeetAI.cs:119`). Found in the scene data, not seen in play.

**Fix:** a prefix on `SwitchSegmentLine(string, string)` (after `LostCharacters`' check): a missing routine becomes the one its name ends with (`Blackout`), or the switch is skipped and reported in the error report.

**Reproduce:** call `SwitchSegmentLine("Grandpa_NPC", "Thief_Intro_Blackout")` on `DialogueCanvas`. With `SegmentLines` on, the log says it used `Blackout`.

## Market orders that don't go through can still bring the cops

**What happens:** black and occult market orders have a 20% chance to bring a cop van, even when the order doesn't happen: in the demo (where those markets are closed), without enough money, or when clicking twice.

**Cause:** `Computer.OrderItem` rolls the van (`Computer.cs:245`) before it checks the demo block, the money and the half-second cooldown (`:249`).

**Fix:** during `OrderItem`, a prefix on `HouseManager.SpawnCopVan` holds the van back; a postfix calls it if the order went through.

**Reproduce:** set `HouseManager.copVan` to null and `demoMode` to true, then place black market orders (`CacheItemToOrder` and the confirm button). With `FailedOrderCops` off, a van comes within a few; with it on, none.

## Custom cassettes cost 10$ more than shown

**What happens:** the cassette site's custom tape shop shows the tape's price (75$) and charges 85$.

**Cause:** `CustomItemShop.CacheItemToOrder` adds 10$ to the shown price for custom pictures (type 0) and nothing for tapes (type 1), but both buy buttons order with `priceOffset` 10 (`CustomItemShop.cs:77-92`).

**Fix:** a postfix sets the tape's buy button up again with `priceOffset` 0, keeping the game's callback that puts the song on the tape.

**Reproduce:** order a custom tape and compare the money before and after with the label.

## The last achievement has no spot on the medal shelf

**What happens:** the medal shelf in the shed (`StarMedal_Case_Achievements`) shows wings and medals for nine of the ten achievements; the last (`pissCount`) never shows.

**Cause:** `AchievementViewer.Start` lays out achievements while `i * 5 + j < achievements.Count - 1` (`AchievementViewer.cs:26`).

**Fix:** a transpiler turns the `- 1` into `- 0`.

**Reproduce:** count `achievementWings` on the `AchievementViewer` after the house loads: 9 without `LastMedal`, 10 with it.

## The money platinum medal reads another medal's progress

**What happens:** the total money platinum goal starts as done if you earned the platinum for the biggest single money gain, and not done if you earned its own.

**Cause:** `AchievementManager.Awake` builds the `totalMoneyPlatinum` goal with `TryGetPlayerPrefBool("biggestMoneyGainPlatinum")` (`AchievementManager.cs:254`).

**Fix:** a transpiler replaces that key with `totalMoneyPlatinum`.

## Picking up the thief's beanie throws and leaves a broken hand

**What happens:** picking up the beanie of the thief (or of a character carrying one) throws a `NullReferenceException`. The beanie is in the hand but nobody holds it, and `Player_Abilities.FixedUpdate` fails on it every physics step.

**Cause:** `Wig.PickupTrigger` turns collisions with the wearer back on with `Physics.IgnoreCollision(GetComponent<BoxCollider>(), ignoreCollisionWith[0], false)` (`Wig.cs:53`). The beanie has a `SphereCollider`, so the first argument is null and the call throws. `Player_Abilities.Pickup` has already put the item in the hand (`Player_Abilities.cs:1144`) and runs the pickup triggers (`:1168`) before it sets who holds it (`SetPlayerRef`, `:1187`), so the hand keeps an item whose `player_Abilities` is null. `FixedUpdate` drops it from afar through that reference (`:890`) and throws each step.

**Fix:** a transpiler on `Wig.PickupTrigger` asks for any `Collider` (what `Usable.Start` does for the same list, `Usable.cs:304`) and makes the `IgnoreCollision` call skip a null collider.

**Reproduce:** with `WigPickup` off, spawn the thief's beanie (or take it from `Bad_Thief` after the story event) and pick it up: the error shows in the log and the hand is stuck. With it on, the beanie is picked up and the wig script removes itself as intended.

## Characters pick up things that belong to switched-off objects

**What happens:** a character picked up the beanie of the thief while `Bad_Thief` was still switched off.

**Cause:** a character that wants an item draws from its room memory, which is the live `Locator.usables` list (`Brain.cs:224`, `Locator.cs:45-62`) of everything that entered the room's trigger and hasn't left. `BrainNeetAI.FindObjInroom` (`:350-371`) takes any entry (its filter even lets destroyed ones through), and `PickupTargetObj` (`:403-419`) hands the object to `Player_Abilities.Pickup`, which with a given object checks neither distance nor whether it is switched on. `Bad_Thief` starts active and is switched off 0.2 s later (`Brain.cs:150`, `disableOnStart`), so its beanie can be listed in a room by then. Whether Unity ever sends the trigger exit for an object that is switched off wasn't checked in play; the code has nothing that removes it (guess).

**Fix:** a prefix on `BrainNeetAI.PickupTargetObj` gives up on a target that is not active in the scene and removes it from the rooms' lists, so the character picks something else next time. The player's own pickup is not touched (it can only reach what it can see), and neither is loading a save, where a switched-off character gets its held items back through the same call.

**Reproduce:** take the dev bridge's object list for `Bad_Thief` (inactive), put its beanie in a room's `usables` and give an NPC `pickupTgt` the beanie: without `HiddenPickups` the NPC holds it afterwards, with it the NPC's hands stay empty.

## No autosave after loading a save

**What happens:** after a save was loaded, the game never autosaves again that session (it still does in a game that started without a save).

**Cause:** `SaveLoadHandler.Update` autosaves when `timeToLastSave < 0`, the player is in the bedroom and `timeToLastLoad <= 0f` (`SaveLoadHandler.cs:80`). `LoadAll` sets `timeToLastLoad = Time.time` (`:151`) and nothing ever clears it, so the last check is false for good. The check can only mean "not just after a load".

**Fix:** a transpiler turns the read of `timeToLastLoad` in `Update` into a call that gives the real value for two seconds after a load and 0 after that. The five-minute timer (`timeToLastSave`, 300 s) and the bedroom rule are as before.

**Reproduce:** start the game with a save present, set `SaveLoadHandler.timeToLastSave` to 1, go to the bedroom and wait: with `AutosaveAfterLoad` on a new `neetSave_*.json` appears, off it doesn't.

## Timed spawners stop after loading a save

**What happens:** after a save was loaded, anything that spawns on a timer stops for the rest of the session: loose change and dust, sink water, toilet flushes, the skeleton parasite's spawns, held corpses dripping blood and the paint and blood sprays.

**Cause:** `Spawner.CanSpawn` allows a spawn when `timeToLastLoad == 0` or `timeToLastLoad - Time.time > 2f` (`Spawner.cs:274`). `timeToLastLoad` is the time of the load, in the past, so the difference is never positive. Every `Spawner.Update` goes through `CanSpawn` (`:181`; nothing sets `externalCallsOnly`). The spawners are `HouseManager.looseChangeSpawner`/`dustSpawner`, `SinkFaucet.cs:102`, `Toilet_Flush.cs:95`, `SkeletonParasite.cs:182`, `Player_Abilities.cs:1774` and `:1894`, and `Commons.SpawnPaintSpray` (`Commons.cs:244`).

**Fix:** a transpiler swaps the subtraction in `CanSpawn` for `Time.time - timeToLastLoad`: spawning starts once 2 s have passed since the load.

**Reproduce:** with a save loaded, read `Spawner.CanSpawn()` on `HouseManager.looseChangeSpawner` (and a sink's spawner after turning the tap on): false until the fix, true two seconds after the load with `SpawnersAfterLoad` on. In play: water runs from the sink after loading.

The same field is compared in three more places:

- `Toilet_Flush.cs:107` uses `Time.time >= timeToLastLoad + 0.5f`, which is correct.
- `DeliveryGuy.cs:53` deactivates the delivery guy in `Start` when `timeToLastLoad == 0 || timeToLastLoad > 0.5f`. After a load the second part is always true, so he is switched off even if the save had him on his way. It is probably meant as "load was more than half a second ago" but `Start` runs in the same frame as the load, so it was most likely meant the other way round. Left alone: the effect is a delivery guy that disappears when a save is loaded mid-delivery, and getting it wrong would leave him standing in the hall.
- `RefrigerationZone.cs:54,76` skips the freezer's `OnEnable`/`OnDisable` work when a load happened and `timeToLastLoad <= 0.2f`, which only holds for a load in the first 0.2 s of the game. Probably meant as "just loaded". The effect would be fridge counts changing while the load switches fuses and food, which nothing here has shown to be wrong. Left alone.

## Worn clothes fall out of the world after loading

**What happens:** clothes worn when the game was saved are ordinary falling items after the load, forever. One reached -20,000 km.

**Cause:** `Wearable.Wear` parks worn clothes at y = -500 with a kinematic body (`Wearable.cs:20-33`). The save stores `isKinematic` (`Usable.cs:241`) but the load code never applies it, and `Player_Abilities.Load` puts the loaded clothes on with `PutOnClothes` directly (`Player_Abilities.cs:361-369`), not through `Wear`. Putting them on yourself and the clothes a rig starts with (`:437-439`) go through `Wear` and are fine.

**Fix:** a postfix on `Player_Abilities.PutOnClothes` makes a worn piece kinematic and, if it was not, moves it back to the parking spot.

**Reproduce:** wear shoes, pants and shirt, save, load: read `Player_Abilities.clothings[i].transform.position.y` and `Rigidbody.isKinematic` a few seconds later. Without `WornClothes` y keeps dropping; with it the pieces stay at -500.

## Not bugs

- `SequenceManager`'s "Toggle" sets an object to the state it's already in (`SequenceManager.cs:137`), and "SpawnBrainAt" reads the rotation from the position (`:169`). Toggle is never used, and the one SpawnBrainAt in the story wants -720 degrees (no turn), so neither shows in the demo.
- `Door.Load` ignores the saved `unbreakable` flag (`Door.cs:170`); the scene's value is the one that's wanted anyway.
- `EncounterSpawner` checks visitor conditions (`SpawnIfVisitedKnots`) against its own copy of the story, which never moves (`EncounterSpawner.cs:156,219`). No visitor in the demo has conditions.
- Earned achievement medals only spawn when a goal completes, but they're items, so a save keeps them.
- `KnotList must be instantiated using the ScriptableObject.CreateInstance method` in the log after every load: the save loader rebuilds the spam list with `new`. Unity warns, but the list works.

- A locked card's medal is reset to 0 on load: the `Locked` setter (`AnimeCard.cs:147-166`) does that on purpose; unlocked cards keep their medal.
