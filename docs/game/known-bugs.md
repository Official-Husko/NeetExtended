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

## Not bugs

- `KnotList must be instantiated using the ScriptableObject.CreateInstance method` in the log after every load: the save loader rebuilds the spam list with `new`. Unity warns, but the list works.

- A locked card's medal is reset to 0 on load: the `Locked` setter (`AnimeCard.cs:147-166`) does that on purpose; unlocked cards keep their medal.
