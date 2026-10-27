# What the demo holds back

Everything found in the demo's code, story and scene data that the player doesn't get to, or only barely. Paths are in the local decompile (`decompiled/Assembly-CSharp/`). The scene and prefab facts come from reading `NEET_Data/level*` and `sharedassets*`; ones not yet seen in play say so.

## Locks

| What | How it's locked | Opened by |
| --- | --- | --- |
| Pantry, attic, roof | `DEMO_BLOCKER_*` walls, two for the pantry (see `world-environment.md`) | Open House |
| Black and occult markets | `HouseManager.demoMode` in `Computer.OrderItem` (`Computer.cs:249`); it also keeps NPCs out of the pantry (`HouseManager.cs:484`) | Open House, "Lift demo limits" |
| Dad's yard | Locked door, `Key_DadYard` in the living room | the key, or Open House |
| Attic stairs | Locked door, `Key_Of_Anger` in the shed (`Key_Attic` is an unused copy) | the key, or Open House |
| Basement backroom | Locked, unbreakable black door; `Key_Basement` is in the bad thief's loot (100%), dropped when he's skeletonized at the end of his dialogue (`Player_Abilities.cs:1671`). Not yet seen in play. | the key, or Open House |

`demoMode` is the only demo flag in the code: no build, debug, DLC or version checks, and no PlayerPrefs key gates anything. The demo's own messages are the `Note_Demo` note on the wall and the red "THIS IS THE END OF THE ENCOUNTERS FOR THE DEMO" lines at the end of GhostBoy's second visit (`GhostboyNight1.N1SecondEncounter`, which also starts the thief intro at 1800 s).

## Nights

`EncounterSpawner.nightId` is set to 1 in `Awake` and only otherwise from a save (`EncounterSpawner.cs:129,140`); nothing moves it on. Night 1's visitors are GhostBoy, Emo, DeathBlue, Biker, MushroomBoy, Poopman, then GhostBoy again. `Night2Encounters` to `Night5Encounters` are empty in the scene, and the story only has three Night 2 stubs ("NO YES MAYBE" lines that go back to Night 1), two of them spelled differently from the character names (`PoopManNight2`, `DeathBlueNight2`). So there's nothing to unlock past night 1.

## Characters and story

- **Behind the backroom door:** `Player_Original` with the `OriginalNEET` story (you killed yourself; the plan is to die, wake up in the `Nightmare1` level and trade 20 soul for a clone). Getting there takes the basement key (above).
- **In the attic:** the Queen of Dust (`QueenOfDust`: a "Candelabrian" living in the walls who pays for dust balls), placed at about (8.1, 7.0, -3.0) behind the attic wall.
- **Poopman's reactions** (`PoopManHarm`, `PoopManDisgust`) never played because of the spelling; fixed in Unofficial NEET Patches (`KnotNames`).
- **Loose ends written into the story:** Emo asks you to break up with her boyfriend and nothing follows; MushroomBoy's story says outright that he won't move in yet; the Succubus says to come back in a few months.
- **Never spawned:** `SkeletonParasite` (maybe inside `Cardboard_Box_Parasite`), `Player_NPC_Corpse`, `SkeletonCorpse`. The BrainBank's `Nightmare1_Desk_Jockey` has Poopman's `characterName`.
- **Story functions the game binds but never calls from Ink:** `AddMoney`, `KillPlayer`, `KillSpeaker`, `DisableLootSpeaker`, `SetOverrideKnot`, `SceneSwitchKnockout`, `SceneSwitchInstant` (`Dialogue.cs:155-236`). Free for modded dialogue.

## Items and hidden spots

- **Backroom:** `Knife_Sacrifice Skull`, `DemonCoreCrystal` (an empty class), bones.
- **Outside every room volume**, so probably sealed in walls (not checked in play):
  - `Agent Gun` with two `MagazinePistol`, `Note_BlackMarkets_Ad` and a silver star medal at about (3.4, 4.5, -0.5);
  - `MoneyStack10000` with a "Ship me out" note at about (14.8, 3.7, -7.9);
  - the `Sacrificer` (`DemonicContraption`: in the dark, on blood, it eats a corpse and spawns things) with its note at about (3.9, 0.3, 5.1); its reward looks like a placeholder (a cheese wheel and blood);
  - `Note_PhoneClueNaked` (427-9683) up at attic or roof height. Calling the number turns on the dining room window trigger.
- `NightmareGlasses` (occult market, or the `Nightmare1` reward) make the Reaper collect corpses, which drops `Soul_Sphere`s (`Player_Abilities.cs:1693`, `HouseManager.cs:227`).

## Emails and the computer

- `HateYou`, `LoveYou` and `TaxReminder` are in the email story but never sent; Email Expansion sends them. `EmailHandler.scheduledEmails` and `conditionalEmails` are never read and have no lists.
- All 11 sites open when their address is typed; only ordering on the black and occult markets is blocked.
- `PigBuddy` under the computer canvas is inactive and its class is empty.

## Leftovers

- `Teleporter` (inactive, in the basement), `Button_CloneTest` (inactive, in the backroom), `SceneSwitch.cs` (a scene dropdown, in no scene), the `[ConsoleMethod]` commands in `PickupBank`/`BrainBank` (no console in the shipped scenes), `HouseManager.Church_Bell` (waits, never rings), `Succubus.Anim` (no caller), `FuseBox_Manager.UpdateHousePowerState` (no caller).
