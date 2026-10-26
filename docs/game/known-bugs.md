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

## Not bugs

- A locked card's medal is reset to 0 on load: the `Locked` setter (`AnimeCard.cs:147-166`) does that on purpose; unlocked cards keep their medal.
