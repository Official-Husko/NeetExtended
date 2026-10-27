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
| Dying with a mask on gets stuck half way | `MaskDeath` |
| Only the first mask comes back after loading | `MasksAfterLoad` |
| Skeletons ruin every pair of black pants | `SkeletonPants` |
| The clothes you die in are lost for good | `DeathDrops` |
| Camera photos replace each other and come back blank | `CameraPhotos` |
| Paint loaded from a save doesn't show | `LoadedPaint` |
| A TV that was on comes back dark after loading | `TvAfterLoad` |
| A custom shirt looks plain once it's worn | `CustomShirts` |
| A pan stays hot for good after food burned in it | `PanCooling` |

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

Clothes that were parked when the game saved but that nobody puts on again fall the same way: the clothes of characters who died or left (their bodies are gone, the parked clothes stay in the save forever) and, in co-op, other players' clothes the host keeps for their return. A postfix on `SaveLoadHandler.LoadAll` parks every `Wearable` still below y -400 after the load: kinematic, hidden, back at -500. Seen in a co-op session as nine falling pieces (the thief's striped shirt among them) at y -1.9e8 on the host.

**Reproduce:** wear shoes, pants and shirt, save, load: read `Player_Abilities.clothings[i].transform.position.y` and `Rigidbody.isKinematic` a few seconds later. Without `WornClothes` y keeps dropping; with it the pieces stay at -500.

## Dying with a mask on gets stuck half way

**What happens:** a player or character who dies while wearing a mask doesn't finish dying: no death animation, no reaping, no respawn, and an `InvalidOperationException` ("Collection was modified") in the log. They can't die again afterwards.

**Cause:** `Player_Abilities.DieAccessor` sets `dead = true`, then takes the masks off with `foreach (GameObject mask in masks) mask.GetComponent<MaskFace>().RemoveMask(gameObject)` (`Player_Abilities.cs:1720-1724`). `MaskFace.RemoveMask` removes the mask from that same `masks` list, so the loop throws on its next step and the rest of `DieAccessor` never runs. Since `dead` is already true, the next `DieAccessor` returns at once.

**Fix:** a last prefix on `DieAccessor` takes the masks off from a copy of the list first, so the game's loop finds it empty. It runs last and only when the death goes ahead, so a death that something else turns away (god mode, co-op's going down) keeps the masks on.

**Reproduce:** wear a mask, then die (`player.set` health 0 with god mode off): with `MaskDeath` off the death stops with the error above; with it on the body dies and respawns as usual. Found in co-op testing, where a downed player bled out wearing one.

## Only the first mask comes back after loading

**What happens:** wear a hat and a cigarette (or glasses), save and load: only one of them is on your head afterwards. The other hangs in the air where it was, or drops.

**Cause:** `Player_Abilities.GetClothingIds` saves every mask after the shoes, pants and shirt (`clothingIds[3]`, `[4]`, ...), but `Player_Abilities.Load` only reads index 3 (`Player_Abilities.cs:371-374`). The rest are loaded as ordinary items at their saved place, and since the load never applies the saved kinematic flag they fall. In co-op the same happens every time a player comes back.

**Fix:** a postfix on `Player_Abilities.Load` puts on every other saved mask with the game's own `MaskFace.PutOnMask`. What can't be worn together (two non-stackable masks) is refused by `PutOnMask` as it is for a click.

**Reproduce:** put on a cap and a cigarette (`items.spawn`, then `MaskFace.PutOnMask`), save, load: `Player_Abilities.masks` has both with `MasksAfterLoad` on and one without.

## Skeletons ruin every pair of black pants

**What happens:** when a skeleton has to crap (its bladder runs out after eating), the log shows an `IndexOutOfRangeException` from `Player_Abilities.Shit`, and from then on every pair of black pants that spawns (store orders, item spawns) is dirty: the shitty material, sticky to the hand, and painting what it touches. It lasts until the game restarts.

**Cause:** `Shit` dirties whatever is in `clothings[1]` and copies its material into the body's second material slot (`Player_Abilities.cs:1564-1569`). Skeletons have `noClothUVs` set: their body (`SkeletonNEETNPC`) has a single material, and their `clothings` hold the outfit prefabs themselves (Shoes_Gray, Pants_Black, Shirt_Red), not worn copies. So `Wearable.MakeDirty` runs on the Pants_Black prefab, and `materials[1]` then throws. Every other clothes path (`PutOnClothes`, `RemoveClothing`, `Player_Abilities.Start`, the pee in `FluidGun`) checks `noClothUVs`; `Shit` doesn't.

**Fix:** a prefix hides `clothings[1]` for the call when the body has `noClothUVs`, and a finalizer puts it back. The game then takes its own no-pants path and drops a turd behind the skeleton.

**Reproduce:** `obj.call` `PlayerEat` with `[260]` on a `SkeletonCorpseDad`'s `Player_Abilities`. With `SkeletonPants` off: the error above, the Pants_Black prefab's `Wearable.dirty` is true, and `items.spawn Pants_Black` comes out dirty. With it on: a turd, no error, clean pants. Found in co-op testing.

## The clothes you die in are lost for good

**What happens:** after the player dies and comes back, the clothes they wore are gone: the new body wears the default outfit, and the old pieces never show up again. They aren't destroyed either: every save keeps them, hidden under the house. Being skeletonized or blown up (Obliterate) can also take the masks or what was in the hands.

**Cause:** worn clothes are separate items parked at y -500, kinematic and hidden (`Wearable`). Every death of the player ends in `HouseManager.CreateAndSetNewPlayer(prevPlayer)` (`HouseManager.cs:441-467`), which moves the old body 600 m up, makes a new body that puts on fresh copies of the prefab's outfit (`Player_Abilities.Start`), and the old body is destroyed a frame later. Nothing ever frees the old pieces. The normal death drops the hands and takes the masks off in `DieAccessor`, but `Skeletonize` moves the body to y 500 first (`Player_Abilities.cs:1914`) and leaves the masks on, and `Obliterate` (`:1655`) drops nothing.

**Fix:** prefixes on `DieAccessor`, `SkeletonizeAccessor` and `Obliterate` remember where this game's player died (the first one wins), and a prefix on `CreateAndSetNewPlayer` drops whatever the old body still holds or wears there: hands with the game's `Drop`, masks with `RemoveMask`, clothes with `RemoveClothing(noHandInsert: true)`, each scattered a little around the spot. In co-op each game does this for its own player, and the items are shared like any other.

**Reproduce:** hold something, die (`player.set` health 0 with god mode off; in co-op you go down first and die 15 s later), or skeletonize (`obj.call` `SkeletonizeAccessor` on the player). With `DeathDrops` off the old clothes stay at y -500; with it on they lie where you died, and the new body has the default outfit.

## Camera photos replace each other and come back blank

**What happens:** after about 20 seconds of play, every photo from the camera is saved to the same file, `StreamingAssets/photograph_-2147483648.jpg`, so a new photo also changes the picture on every older one. After loading a save, camera photos are blank.

**Cause:** `PhotoCamera.PrintPicture` names the file `"photograph_" + Mathf.RoundToInt(100000000f * Time.realtimeSinceStartup)` (`PhotoCamera.cs:31`); past about 21.5 seconds that's more than an int holds and comes out as -2147483648. The picture keeps the file's full path in `Picture.storedImagePath`, and the save stores it. On load, `Picture.loadImage("")` only looks for `StreamingAssets/<storedImagePath>.jpg` and then for a texture of that name in Resources (`Picture.cs:44-53`), which a full path never is.

**Fix:** a transpiler on `PrintPicture`'s state machine takes the number from a counter that starts after the highest `photograph_N.jpg` already there. A prefix on `Picture.loadImage` loads a stored full path from disk when the file exists. It leaves calls that pass a path alone: setting `storedImagePath` before `Picture.Start` runs makes `Start` load the photo a second time, and that load sets the colour back to white and cuts the fade-in.

**Reproduce:** hold a camera (`PhotoCamera`) and use it twice: with `CameraPhotos` off both photos go to `photograph_-2147483648.jpg` (once the game has run 21 s), with it on to `photograph_1.jpg` and `photograph_2.jpg`. Spawn a `Picture`, set its `storedImagePath` to a photo's full path and call `Start`: blank with the fix off, the photo with it on.

## Paint loaded from a save doesn't show

**What happens:** after loading, some surfaces don't show the blood, scorch marks or other paint they had when the game saved, although the save has the picture. The next splat on such a surface draws nothing and wipes the picture. The surface only recovers once it moves (a door). In single player it's some of them (37 of 88 in one test save); on a co-op client loading the host's house it's nearly every painted surface, so paint that existed when someone joined was invisible to them.

**Cause:** `SaveLoadHandler.Start` runs `LoadAll`, whose `LoadPaintTargets` (`SaveLoadHandler.cs:443-466`) calls `SetupPaint(picture)` on each painted surface. Unity runs the `Start`s of one frame in no set order, and a `PaintTarget` only finds its renderer in its own `Start` (`CheckValid`, `PaintTarget.cs:545-556`, `:558-565`). Set up before that, `CreateTextures` never gives the material its paint and position textures (`:592-601`), and `RenderTextures` renders an empty position texture (`:606-616`) and clears `transform.hasChanged`, so nothing renders it again. The picture is in `splatTex` but never shows, and a splat's blit (which looks up each texel's position) writes over it. On a co-op client, 0 of 1,242 surfaces had started when the paint loaded.

**Fix:** a prefix on `PaintTarget.SetupPaint` runs `CheckValid` first when the surface has no renderer yet. A surface with `SetupOnStart` (9 in the house) would then set itself up again in its `Start`, with empty textures; a prefix on `Start` only runs `CheckValid` for a surface that's already set up.

**Reproduce:** paint something (blood, a fire), save, load. With `LoadedPaint` off, some painted surfaces have no `_SplatTex` on their material and an empty `worldPosTex` (Co-op's `PaintProbes.Unbound` and `Info` show both), and look clean; in co-op, join a house with paint and look at it. With it on, only surfaces whose material has no paint property are listed, and the paint shows.

## A TV that was on comes back dark after loading

**What happens:** save with the TV on, then load: the TV is on and its channel's sound plays, but the screen is dark and the screen light is off until the remote is used. A co-op client loading the host's house gets the same.

**Cause:** `TV.Load` switches the TV on and shows the saved channel (`TV.cs:81-98`). `SaveLoadHandler.Start` loads the save before the TV's own `Start` runs, and `TV.Start` turns the screen light off and every channel object off (`:119-124`), so the shown channel is hidden again while `isOn` and `displayedScreenModule` still say it's on.

**Fix:** a postfix on `TV.Start` shows the saved channel again (`Control(displayedScreenModule, noAudio: true)`) when the TV is on. A TV that's off isn't touched.

**Reproduce:** turn the TV on, save (sleep), load. With `TvAfterLoad` off the screen is dark with the channel's sound playing; with it on the channel shows.

## A pan stays hot for good after food burned in it

**What happens:** food that burns in a frying pan can leave the pan hot for good. Long after the stove is off the pan keeps its heat (and its flame level: it still cooks, burns whoever touches it and hisses), and it never cools down.

**Cause:** a `HeatHolder` (`HeatHolder.cs`) keeps the flames touching it in `flamesInContact`: added in `OnTriggerEnter`, removed in `OnTriggerExit` by looking up the other collider's `Flame`. Burning food gets a `Flame` of its own (`Flame.AddBurnCollider`, `Flame.cs`) which the game destroys again (`deleteFlameOnExtinguish`, or after its timer), and Unity sends no exit for a component that was destroyed, so the dead `Flame` stays in the set (the later exit finds no `Flame` on the food to remove). `Update` calls `DistributeHeat` for every entry, and `DistributeHeat` ends with `decayDelayTimer = 2f` even when the flame is gone (`HeatHolder.cs:190`). The delay restarts every frame, so the heat never decays.

**Fix:** a prefix on `HeatHolder.Update` drops destroyed flames from `flamesInContact` first.

**Reproduce:** burn a piece of meat in a pan on a lit stove, turn the stove off, destroy the meat and lift the pan away from the stove, then read `HeatHolder._heatIndex` and `flamesInContact`: with `PanCooling` off the set holds destroyed flames and the heat stays put, with it on the set is clean and the heat falls by 1 a second. (A pan left inside the stove's zone doesn't cool either way: the stove's `Stove_Zone` flame stays in the set while the pan touches it, and it resets the delay too. That is how the game is, not part of this fix.)

## Thunder stops for good after the story's lightning

**What happens:** the random thunder (a strike every 30 to 180 seconds) works until the story sets off its own lightning (Ghostboy's first line on Night 1). After that there is no more thunder until the game is restarted. Measured at 5x speed: 8 strikes in 750 game seconds before the story strike, none in 850 after.

**Cause:** `HouseManager.Start` starts the loop without keeping it, and the story's `Thunder` sequence stops `thunderCorout` and starts a single strike in its place (`HouseManager.cs:345-352`). The loop schedules its next round by storing itself in `thunderCorout` at its end (`:521`), so once the first random strike has happened the story's stop kills the loop. The single strike (`instant: true`) never schedules another (`if (!instant)`, `:520`). Before the first random strike the stop does nothing, because the field is still empty and the loop lives on unnoticed.

**Fix:** the loop no longer stores itself in `thunderCorout` (the store in `Thunder`'s state machine is swapped for a call that does nothing). The story's stop then only ever reaches its own earlier single strike, and the one loop started in `Start` keeps running. `Sky.Strike()` in the framework starts the same single strike without touching the field either.

**Reproduce:** turn `ThunderLoop` off, wait for a strike (or `sky.set thunderMin=5 thunderMax=8` on the bridge), run `sky.strike story=true`, and count strikes: none after. With it on, they carry on.

## The moon never comes back after lightning

**What happens:** after the first strike the moonlight is off for the rest of the session, so the yard is darker than in the first minutes and the moon's shadows are gone.

**Cause:** `Thunder` flashes the Moon (`mainDirectionalLight`, intensity 0.2), fades it, and sets its intensity to `0f` (`HouseManager.cs:515`). Nothing sets it back: the scripts that would (`SunRotation`) are disabled in the house.

**Fix:** the `0f` is replaced by the intensity the scene started with, which the framework's `Sky` remembers for each house load (`Sky.MoonBaseIntensity`, 0.2). The moon's shadows come back with it; they cost 12-15% more draw calls (see "Weather & sky" in `world-environment.md`), so Performance Fixes' moon shadow option is the way to keep them off.

**Reproduce:** `sky.strike measure=true` and read `moonNow` (0 with `MoonRestore` off, 0.2 with it on).

## Not bugs

- `SequenceManager`'s "Toggle" sets an object to the state it's already in (`SequenceManager.cs:137`), and "SpawnBrainAt" reads the rotation from the position (`:169`). Toggle is never used, and the one SpawnBrainAt in the story wants -720 degrees (no turn), so neither shows in the demo.
- `Door.Load` ignores the saved `unbreakable` flag (`Door.cs:170`); the scene's value is the one that's wanted anyway.
- `EncounterSpawner` checks visitor conditions (`SpawnIfVisitedKnots`) against its own copy of the story, which never moves (`EncounterSpawner.cs:156,219`). No visitor in the demo has conditions.
- Earned achievement medals only spawn when a goal completes, but they're items, so a save keeps them.
- `KnotList must be instantiated using the ScriptableObject.CreateInstance method` in the log after every load: the save loader rebuilds the spam list with `new`. Unity warns, but the list works.

- A locked card's medal is reset to 0 on load: the `Locked` setter (`AnimeCard.cs:147-166`) does that on purpose; unlocked cards keep their medal.

## A custom shirt looks plain once it's worn

**What happens:** a shirt bought in the custom shop shows its picture while it is in your hand or on the floor, but when you put it on the body wears the plain shirt (the print is missing on the body and the hands). After saving and loading it shows.

**Cause:** `CustomItemShop.CustomizeItem` (`CustomItemShop.cs:114-128`) sets the picture through a `MaterialPropertyBlock` on the item's first renderer. `Player_Abilities.PutOnClothes` (`:1428-1460`) copies `GetComponent<MeshRenderer>().material` onto the body and hands, and a material doesn't contain its renderer's property block. A saved item gets its picture on the material itself (`CustomTextureSaveLoader.Load`, `CustomizeItem(GameObject, string)`), so loading fixes it. Every shirt has a `CustomTextureSaveLoader`, but only `Shirt_White` is sold in the custom shop.

**Fix:** a postfix on `CustomItemShop.CustomizeItem` sets the shop's picture on the material of a customized `Wearable` as well. The property block stays, so the item looks the same as before.

**Reproduce:** unpack a custom shirt (or call `CustomizeItem` on a spawned `Shirt_White` with the shop's preview picture set), put it on, and read `neetRend.materials[2].mainTexture`: none with `CustomShirts` off, the picture with it on.
