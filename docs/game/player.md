# Player

Scope: `Player_Abilities`, `Player_Movement`, `PlayerStats`, `Stat` and its subclasses, `StatusEffect`, `StatusHandler`, camera classes, and the small helper components listed below. All paths are relative to `decompiled/Assembly-CSharp/`. Line numbers refer to those files. Values marked "asset" are set in the Unity inspector or in ScriptableObject assets and are not visible in the decompiled code.

## Overview

### Composition

The player is a prefab instantiated by `HouseManager.CreateAndSetNewPlayer` (`HouseManager.cs:441`) and stored in `HouseManager._.player` (`HouseManager.cs:76`). The components that matter here:

| Component | Role |
|---|---|
| `Brain` / `BrainPlayer` | Owns `intents` (an `IntentHolder`). `BrainPlayer` polls the Input System every frame (`BrainPlayer.cs:111-132`). Not documented here beyond that. |
| `Rigidbody` + `CapsuleCollider` | Physics body. `Player_Movement` requires `Rigidbody` (`Player_Movement.cs:8`). Height is 1.67 standing, 0.55 crouched. |
| `Player_Abilities` | Hands, pickups, clothing, masks, eating/peeing/vomiting, punching/kicking, death. `pickups[0]` is left, `pickups[1]` is right. |
| `Player_Movement` | Walking, sprinting, crouching, sitting/resting flags, slipping, footsteps, camera bob. |
| `PlayerStats` | Subclass of `ActorStats`. Holds all needs stats (health, hunger, bladder, hygiene, energy, and others). |
| `StatusHandler` | Holds active `StatusEffect` instances and ticks them. |
| `Animator` (`neetAC`), `SkinnedMeshRenderer` (`neetRend`) | Body. Blend shapes are used for fat (index 2), body type (3), muscle (4), left hand (0), right hand (1). |
| `FluidGun` x2 (children) | `fluidGunPee` (`clothId` 1) and `fluidGunPuke` (`clothId` 2). Used for peeing and vomiting. |
| Child `Camera` with `CameraControl` | First-person camera. `CameraControl` is a child of the player root. |
| `AudioSource`s | Accessed by index in `Awake`/`Start` (e.g. `[5]` is `dynAudio`, `[2]` is the death/slip source). |

### Input flow

1. `HouseManager` loads an `InputActionAsset` (`HouseManager.cs:20`, `inputAsset.Enable()` at `:298`) and looks up actions by name, for example `"Left_Hand"`, `"Use"`, `"Hunger"` (`HouseManager.cs:298-317`). The fields are `move`, `i_look`, `i_left_hand`, `i_right_hand`, `i_use`, `i_drop`, `i_sprint`, `i_crouch`, `i_pickup_rotation`, `i_zoompickup`, `i_lockview`, `i_eyelid_menu`, `i_exit`, `i_breath`, `i_hunger`, `i_bladder`, `i_hygiene`, `i_energy`.
2. `BrainPlayer.Update` reads each action and writes it into `brain.intents` with `Intent.UpdateState(held, strength)` (`BrainPlayer.cs:111-132`).
3. `Player_Abilities.Update` (`Player_Abilities.cs:542`), `Player_Movement.Update/FixedUpdate` (`Player_Movement.cs:244`, `:372`) and `CameraControl.Update` (`CameraControl.cs:135`) read `mainBrain.intents` and act on it. Nothing in these three classes reads the Input System directly.

Action-to-intent mapping (`BrainPlayer.cs:111-132`):

| Action | Intent | Notes |
|---|---|---|
| `move` (Vector2) | `MoveForward` (.y), `MoveLateral` (.x) | |
| `i_look` (Vector2) | `LookLeftRight` (.x), `LookUpDown` (.y) | Mouse look. |
| `i_crouch` | `Crouch` | Toggle, see Movement. |
| `i_sprint` | `Sprint` | Double-tap triggers a kick. |
| `i_left_hand` / `i_right_hand` | `LeftHand` / `RightHand` (and `Pickup` if either) | |
| `i_use` | `Use` | |
| `i_drop` | `DropPickup` | Short press drops, long press throws. |
| `i_pickup_rotation` | `RotatePickup` | |
| `i_zoompickup` (Vector2) | `ZoomPickup` (.y) | Push/pull held item. |
| `i_lockview` | `Lockview` | Blocks grabbing and camera look. |
| `i_eyelid_menu` | `HUD` | Eyes-closed menu with toilet/hunger/hygiene/sleep buttons. |
| `i_exit` | `Exit` | Toggles `Feedback_Sender` (`CameraControl.cs:137`). |
| `i_breath` | `Breath` | |
| `i_hunger` / `i_bladder` / `i_hygiene` / `i_energy` | `Hunger` / `Bladder` / `Hygiene` / `Energy` | Each triggers a one-shot action on `Started` (see Abilities). |

`IntentHolder` also declares `Pee`, `Poop`, `Puke`, `RemoveClothing` and `LookAt`, but `BrainPlayer` never sets them and `Player_Abilities` never reads them. Treat them as unused for the player.

`Intent` (`IntentHolder.cs:8-75`):
- `Active` is `isHeld`. Reading it also schedules `UpdateState(sustained)` for the next frame via `Commons.CoroutineWrapOneFrame` (`IntentHolder.cs:23`). This side effect is easy to miss if you read `Active` from a patch.
- `Started` is `isHeld && !wasHeld`. `Ended` is `wasHeld && !isHeld`.
- `strength` is only non-zero while held.

### How stats tick

| Where | What |
|---|---|
| `PlayerStats.FixedUpdate` (`PlayerStats.cs:258`) | Passive decay of hygiene, bladder, hunger, energy, wetness, knockout and disgust. Returns early if `pausedNeeds`, so all of these pause together. |
| `StatusHandler.Update` (`StatusHandler.cs:148`) | Re-applies sticky stats (`Speed`) to `initValue`, then ticks status effects. |
| `Player_Abilities.Update` | One-shot needs actions (burp, shit, sniff, yawn), breath, exercise-like effects, the kill timer. |
| `Player_Movement.Update` | Energy gain while resting or sitting with the eyelid menu open. |
| `Stat.SetVal` | Clamps to [0, 100] and fires events (see Stats system). |

## Stats system

### Classes

- `Stat` (`Stat.cs:5`): base class. Fields `value` (internal), `initValue` (internal), `lastValue` (internal), `sticky`, `statType`. Events `OnChanged`, `OnSubZero`, `OnNotZero`, `OnMaxed`. Delegate hook `manageEffect` (`Stat.EffectManager`) used by `Affect`.
- `StatType` (`StatType.cs`): enum `Health, Hunger, Bladder, Hygiene, Energy, Speed, Wetness, Knockout, Disgust, ImpactResistance, CuttingResistance, BodyType, Soul`. `Stat.GetStatTypeFromEnum` (`Stat.cs:118`) maps it to the class.
- Subclasses (one file each, all thin): `Health`, `Hunger`, `Bladder`, `Hygiene`, `Energy`, `Speed` (sets `sticky = true`, `Speed.cs:9`), `Wetness`, `Knockout`, `Disgust`, `BodyType`, `Soul`. `ImpactResistance` and `CuttingResistance` are referenced but live in their own files that are not part of this area.
- `ActorStats` (`ActorStats.cs:5`): `MonoBehaviour` with `Dictionary<Type, Stat> stats` keyed by the concrete class (`typeof(Hunger)`, `typeof(Health)`, ...). `RegisterStats(params Stat[])` fills it. `ActorStats.canDamageOther` checks `Health > 0`.
- `PlayerStats : ActorStats` (`PlayerStats.cs:8`): creates and registers the stats in `Awake` (`:170-201`), and wires up the behaviour in `Start` (`:203-256`).
- `HealthOnly : ActorStats` (`HealthOnly.cs`) is a non-player actor with only Health. Not the player.

### Core `Stat` mechanics (`Stat.cs`)

- `SetVal(v)` (`:72-106`): sets `lastValue = value`, then `value = Mathf.Clamp(v, 0, 100)`. Then, in order: `OnChanged()` (always, even if the value did not change), `OnSubZero()` if the value went from >0 to <=0, `OnNotZero()` if it went from <=0 to >0, `OnMaxed()` if it went from <100 to >=100, the thought-bubble refresh when crossing 35 or 65 (only if `vertTxt` set), and `symbol.SetScale(value)` if a UI symbol is attached.
- `AddVal(v)` = `SetVal(GetVal() + v)`.
- `ForceSetVal(v)` (`:62`) writes `value` directly. No clamp, no events.
- `ForceSetLastVal(v)` (`:67`) writes `lastValue` directly.
- `Affect(pos, potency, EffectType, force?, source?)` (`:108`): if `manageEffect` is set, `val = manageEffect(pos, potency, initValue, effTyp, force, sourceObj)`, else `val = potency`. Then `AddVal(val)`. This is the path used for damage. Negative potency means damage, but see the sign note under Gotchas.
- `GetVal()` returns `value`. It is `virtual`.
- `initValue` is what `Affect` passes to `manageEffect` and what `StatusHandler` resets sticky stats to.

`EffectType` (`EffectType.cs`): `Impact`, `Cut`, `Burn`.

### Stats on the player

Constructors default to 100 unless noted. `PlayerStats.Awake` (`:172-185`) re-creates every stat using the inspector-serialized `initValue` of the old object, so the actual starting values come from the prefab (asset, not in code).

| Stat | Field (on `PlayerStats`) | Range | Passive decay / regen (per second unless noted) | Consequence at thresholds |
|---|---|---|---|---|
| Health | `health` | 0-100 | None. Damage only through `Affect`. | Crosses to <=0: `OnSubZero` calls `Player_Abilities.DieAccessor` (`PlayerStats.cs:211`). See Death. |
| Hunger | `hunger` | 0-100 | `hungerDecreaseRate = -0.12`. If fat blend shape (index 2) > 0 and hunger < 60, rate is x0.65 (`:269-281`). | No direct death. Below 60 with fat > 0 fat slowly drops; below 75 muscle slowly drops (`:230-240`). Above 100 from food adds to fat (`Player_Abilities.cs:1526`). `Exercise()` costs -1. |
| Bladder | `bladder` | 0-100 | `bladderDecreaseRate = -0.06`. Food adds -0.4 x food value, drink adds -2 x value. | `OnSubZero` (`:216-227`): if the last ingestion was food, `Shit()` (sets bladder +40 if it was below 75, and drops a turd or dirties pants). Otherwise `fluidGunPee.SetFluid(true)`. |
| Hygiene | `hygiene` | 0-100 | `hygieneDecreaseRate = -0.04`. -0.3 while holding a dirty item (`handlingDirtyItemLeft/Right`, set by `HygieneDecreaser`). +16 while in `WaterVolume` (`touchingWater`). `Exercise()` -1. Shitting in pants -50 (`Player_Abilities.cs:1570`). | No consequence in this area. `Sniff()` plays a sound only. |
| Energy | `energy` | 0-100 | `energyDecreaseRate = -0.06`. Sitting +0.3, resting +3, but only while the eyelid HUD is active (`Player_Movement.cs:328-335`). `Exercise()` -1. | Below 30 (or slipped): vignette and blink (`CameraControl.cs:172`). No forced sleep in this area. |
| Speed | `speed` | 0-100 | `sticky`. Reset to `initValue` every `StatusHandler.Update`. | Scales walking force (see Movement). Changes via `SetVal` are overwritten next frame. |
| Wetness | `wetness` | 0-100 | -1.5 while >0 (`:295-298`). Set to 100 on entering water, +5..12 from spray. | >60 and not in water: drip particles (`:283-294`). Burn damage halved when >50 (`:337-343`). |
| Knockout | `knockout` | starts -0.01 | -1 while >0 (`:299-302`). Breath hold >6 s adds 4-6.2 (`Player_Abilities.cs:1647`). `SkeletonParasite.cs:148` sets 10. `Dialogue.cs:313` adds. | `OnChanged` forces eyes shut (`CameraControl.cs:110`). `OnNotZero` triggers `SlipAccessorNoRecov` (`Player_Movement.cs:209`). `OnSubZero` triggers `SlipRecoverAccessor` (`:211`). |
| Disgust | `disgust` | starts -0.01 | -2 while >0 (`:303-306`). | `FluidGun.DisgustVomitCheck` vomits when >50 (`FluidGun.cs:152`). |
| ImpactResistance | `impactRes` | asset | none here | Multiplies Impact damage (`DamagePlayer`, `:333-336`). |
| CuttingResistance | `cuttingRes` | asset | none here | Multiplies Cut damage (`:322-332`). |
| BodyType | `bodyType` | 0-100 | none | Drives blend shape 3 via `OnChanged` (`:193-197`). Starts from `PlayerPrefs "DesiredBodyType"` for objects named `Player` (`:244-247`). |
| Soul | `soul` | 0-100, default 66 | none | When the player's damage kills someone, the damaging actor's Soul loses 25 (`:366-372`). |

Muscle and fat are not `Stat` objects. They are blend shapes on `neetRend`: index 4 is muscle, index 2 is fat. `GetMuscleTier()` (`PlayerStats.cs:397`) is `floor(muscle / 30)`, used for extra punch and throw damage.

`PlayerStats` flags:
- `pausedNeeds`: when true, `FixedUpdate` returns immediately, so none of the passive decays run. `Player_Abilities.Breath` also skips knockout while it is true (`:1647`). `Player_Abilities` sets it false in `SetMainPlayer` (`:1966`).
- `invincible`: in `DamagePlayer` (`:315`), returns 0 for any `Affect` call, so `Affect`-based damage does nothing. Direct `SetVal`/`AddVal` still works.
- `handlingDirtyItemLeft` / `handlingDirtyItemRight`: set by `HygieneDecreaser`. See Gotchas.
- `touchingWater`, `wetWithLiquidType`, `dripTimer`: water logic.
- `faceTextureName`, `startFat`, `startGender`, `startMuscle`: appearance, applied in `Start` and `SetPlayerFace`.
- Static decay rates: `PlayerStats.hungerDecreaseRate`, `bladderDecreaseRate`, `hygieneDecreaseRate`, `energyDecreaseRate` (`:125-131`). These are `public static float` and can be changed at runtime. Changes apply to every instance.

### Reading and writing stats

The player's stats are fields on `PlayerStats`, and also in `stats[typeof(T)]`:

```csharp
PlayerStats ps = HouseManager._.player.GetComponent<PlayerStats>();
float hunger = ps.hunger.GetVal();          // read
ps.hunger.SetVal(100f);                     // set, clamped, fires events
ps.hunger.AddVal(-10f);                     // relative change, fires events
ps.hunger.ForceSetVal(100f);                // set without clamp or events
ps.stats[typeof(Hunger)].GetVal();          // same object, dictionary form
```

Notes:
- `SetVal` and `AddVal` clamp to [0, 100] and fire `OnChanged` and threshold events. Use these for game-like changes.
- `ForceSetVal` skips events. `OnSubZero` will not fire, so death will not be triggered. Use it only for loading saves or silent resets.
- `value`, `initValue` and `lastValue` are `internal` on `Stat`. A separate mod assembly cannot access them directly. Use `GetVal()`, `ForceSetVal()` or reflection (`AccessTools.Field`).
- `PlayerStats.Awake` replaces the stat objects. Cache references only after `Awake`, or re-fetch them.

## Status effects

### `StatusEffect` (`StatusEffect.cs`)

A `ScriptableObject` (`CreateAssetMenu` at `:5`). Each application creates an instance via `Object.Instantiate(st)` (`StatusHandler.cs:214`). Key fields:

| Field | Meaning |
|---|---|
| `statusType` (`StatusEffectType`) | Tick behaviour, see below. |
| `operationType` (`overwriter`, `multiplier`, `linearDelta`) | Combined into `oper` in `OnApplied` (`:63-69`). `overwriter` sets, `multiplier` multiplies, `linearDelta` adds. |
| `statType` (`StatType[]`) | Stats to apply to. Tooltip says the order must match the `StatType` enum. An effect is applied only if the player has at least one matching stat, unless the array is empty (`StatusHandler.cs:212`). |
| `effectSize` | Operand per tick. For `ticker` it is multiplied by `Time.deltaTime` (`:83`). |
| `duration`, `durationTimer` | Lifetime. `durationTimer` is set from `duration` on add. |
| `startDelay`, `startDelayRandomOffset` | Delay before applying. Random offset is applied (`StatusHandler.cs:274`). |
| `tickerFreq` | Seconds between ticks for `ticker`. |
| `cannotStack` | Refuses the add if an effect with the same name is already queued or active. |
| `dontSave` | Excluded from `Save()`. |
| `noStatEffect` | Skips the operation. |
| `onAppliedClip` | Sound on apply (not for delayed or reloaded effects). |
| `handlerRef` | Set to the owning `StatusHandler`. |

Virtual hooks: `OnApplied`, `OnRemoved`, `OnTick`, `OnStatModified`. `OnTick` (`:75-85`) applies `oper(stat.GetVal(), delta)` to every stat in `statRef` via `SetVal`.

`StatusEffectType` (`StatusEffectType.cs`):

| Type | Behaviour in `StatusHandler.Update` (`StatusHandler.cs:148-189`) |
|---|---|
| `ticker` | `OnTick` every `tickerFreq` seconds (time-based). The operand is `effectSize * Time.deltaTime`. |
| `persistent` | `OnTick` every frame. The operand is `effectSize` with no delta-time scaling. Frame-rate dependent. |
| `oneTime` | `OnTick` once, then removed. |
| `amplifier` | Not ticked in `Update`. Intended to hook `OnTick` into each stat's `OnChanged`, but see Gotchas. |

### `StatusHandler` (`StatusHandler.cs`)

- `AddEffect(st, forceDelayed, remainingDelay, durationTimerOverride, startDelayTimerOverride, timeToLasTickOverride)` (`:196`) is the public entry point. It returns the instance, or `null` if the player lacks the stat or `cannotStack` blocks it.
- `RemoveEffect(effect)` (`:292`) calls `OnRemoved` and removes it from `effects`.
- Effects with `startDelay > 0` are held in `effectsDelayed` (`StartDelayed`, `:268`) until their timer runs out. Effects are staged in `effectsToAdd` and moved into `effects` at the end of `Update` (`:185-188`).
- `hasStats` is populated from every `ActorStats` on the same GameObject (`:127-138`). `stickyStats` collects those with `sticky`.
- `effectsToAddOnStart` (asset list) is applied once in `Start`.
- Save and load: `Save()` (`:52`) stores remaining timers. `Load` (`:74`) looks effects up by name in `HouseManager._.statusEffectDict`.
- Instances are keyed by name in `HouseManager._.statusEffectDict` (built from `HouseManager.statusEffects`, `HouseManager.cs:327-331`).

### Effects referenced in code

The effect assets themselves are not in the decompiled code. These references are in code:

| Effect (field or call site) | Applied by | Notes |
|---|---|---|
| `speedEffectCrouch` | `Player_Movement.Update` while crouched (`:346-349`) | Removed when standing (`:364-368`). |
| `speedEffectFat` | `Player_Movement.CheckApplyFatSpeedDebuff` (`:214-242`) | Two instances, added when fat blend shape is above 33 and 66. |
| Food `cookedStatusEffect` / `rawStatusEffect` (and `...2`) | `Food.cs:540-588` | Chosen by the food's cooked state. |
| `LiquidContainer` effect (by name) | `LiquidContainer.cs:205` | Drinks. |
| `TransferStatusEffectOnContact` effects | `TransferStatusEffectOnContact.cs:27-32` | Contact transfer. |

Durations and sizes for these are asset data. Look them up in the project's ScriptableObject assets if needed.

## Abilities and interaction

All of this is in `Player_Abilities.cs`. Hands are `Hands` (`Hands.cs`): `left = 0`, `right = 1`, `both = 2`, `none = -1`. `pickups` is `GameObject[2]`, indexed by hand.

### Hand selection and dispatch (`Update`, `:542-785`)

- `selectedHands` is derived each frame from `LeftHand.Active` / `RightHand.Active` (`:595-610`): both, left, right or none.
- Actions run through `ExecuteAction` (`:982`), which uses `selectedHands` and only acts on hands that hold something. `ExecuteActionHand` (`:950`) takes an explicit hand.
- Releasing a hand calls `DropOnClickUp` (buttons and levers) and `OnReleaseHand` (`IReleaseTriggerable`).
- The core grab logic is in `Update` (`:611-655`), gated by `!cancelledClick`:
  - Use held: `Use(hand)` on the held item.
  - Drop released quickly (`throwTimer < 0.18`): `Drop(hand)`.
  - Drop released after holding `>= 0.18`: `Throw(hand)`.
  - Otherwise, when not in `Lockview`: `Pickup(selectedHands)`, or both hands.
- `throwTimer` accumulates while `DropPickup` is held.
- `cancelledClick` is set after drop or throw, and cleared on `Use` ended, so one click cannot both drop and grab.

### Abilities table

| Ability | Input | Method | Notable numbers and behaviour |
|---|---|---|---|
| Grab from front | Hand intent (`LeftHand` / `RightHand`) | `Pickup(Hands hand)` (`:1115`) and `Pickup(Hands, GameObject)` (`:1120`) | Raycast 2.1 m from `mainCamera` on `usablesMask` (`:1126`). Refuses if the hand is full, the `Usable` already has a `player`, or the blacklist/whitelist blocks the player. Ignores colliders with the picker. Calls `IPickupTriggerable.PickupTrigger`, `pickupEvent`, `OnPickup`, `PickedUp`, `SetPlayerRef`. Buttons and levers are handled separately (`:1189-1216`). |
| Hold position | Automatic | `CenterPickup(hand, rotMatchSpeed, isLever)` (`:1028`) | Pulls the item toward a point 0.32 to 0.42 m in front of the camera with force 400 (or 630 on a double click, or during punch cooldown). Also rotates it toward `heldRotation`. |
| Rotate held item | `RotatePickup` + look | `Rotate` (`:1329`) calls `Usable.Rotate(look)`. | |
| Reset rotation | Quick tap of `RotatePickup` | `ResetOffsets` (`:1341`) | Triggered if `RotatePickup` ended in under 0.15 s. |
| Push/pull | `ZoomPickup` | `RaiseLower` (`:1353`) calls `Usable.RaiseLower(strength)` | |
| Drop | Short `DropPickup` | `Drop(Hands)` (`:1219`) | Calls `IDropTriggerable.DropTrigger`, `ResetPlayerRef`, re-enables collisions after 0.15 s. Adds -50 down impulse. Sets `cancelledClick = true`. |
| Throw | `DropPickup` held >= 0.18 s, then released | `Throw(Hands)` (`:1258`) | `Rigidbody.AddForce(camera.forward * 10000 * throwForceModifier)`. Sets `Usable.thrown = true`. Adds up 50 and forward `50 * (1 + 0.05 * muscleTier)` to the hand. Calls `IThrowTriggerable.ThrowTrigger`. |
| Use held item | `Use` held | `Use(Hands)` (`:1317`) | Calls `Usable.IUsable.Use()` on every `IUsable` component, unless broken, button or lever. |
| Press button/lever | `Use` or grab | `PickupUseOnly()` (`:1095`) and `Pickup` button branch | Fires `Usable.IUsable.Use` or `useEvent`, `OnUse`, and `RaisePhysicsHand(oneShot)`. |
| Punch | Double-tap `LeftHand` or `RightHand` within 0.25 s, or punch cooldown | `Punch(Hands, GameObject forced = null)` (`:804`) | Cooldown 0.1 s. Raycast 0.85 m. Damage `-(1.5..3.5) - muscleTier` to `Health` via `DamageOther` (`:834-841`). Calls `IPunchTriggerable.PunchTrigger`. |
| Kick | Double-tap `Sprint` within 0.25 s | `Kick()` (`:851`) | Animator trigger `"Kick"`. Raycast 2.1 m. After 0.2 s `ApplyKickForce` adds 1500 force to a `Usable` target and calls `DamageOther` (`:862-874`). |
| Eat | Via food objects | `PlayerEat(float foodVal)` (`:1521`) | Called from `Food.cs:556`. Hunger +food (and fat blend shape if over 100), bladder -0.4 x food, sets `lastIngestionWasFood = true`. |
| Drink | Via `LiquidContainer` | `PlayerDrink(float)` (`:1539`) | Called from `LiquidContainer.cs:189` with `sipVolume * 0.8`. Hunger +value, bladder -2 x value, sets `lastIngestionWasFood = false`. |
| Burp | `Hunger` intent `Started` | `Burp()` (`:1549`) | Sound only. |
| Pee | Bladder at 0, or vomit/pee guns | `playerStats` bladder `OnSubZero`, `fluidGunPee.SetFluid(true)` | Pee gun, `clothId` 1. |
| Poop | `Bladder` intent `Started` | `Shit()` (`:1556`) | Only if bladder < 75: plays fart, bladder +40. If pants are on, dirties them and `hygiene -50`. Otherwise spawns a turd. Otherwise plays a sound only. |
| Sniff | `Hygiene` intent `Started` | `Sniff()` (`:1585`) | Sound only, depends on hygiene >= 65. |
| Yawn | `Energy` intent `Started` | `Yawn()` (`:1601`) | Sound only, depends on energy >= 65. |
| Breath | `Breath` intent | `Breath()` (`:1617`) | Vignette while held. After 6 s held, knockout +4..6.2 (unless `pausedNeeds`). Release after 0.6 s: spray wind, or with cigarette masks, exhales smoke and `hunger +2` per frame. |
| Vomit | Disgust > 50 | `FluidGun` `clothId` 2 | Via `FluidGun.DisgustVomitCheck`. |
| Wear clothing | Via `Wearable` | `PutOnClothes(GameObject, int pieceId, bool updateMatsOnly)` (`:1428`) | `pieceId`: 0 shoes, 1 pants, 2 top. Updates attire and blend shapes. |
| Remove clothing | `RemoveClothing` (not bound to an input) | `RemoveClothing(int pieceId, bool noHandInsert)` (`:1462`) | Refuses top removal while unremovable masks are on. Otherwise inserts the item into the selected hand unless `noHandInsert`. |
| Masks | Via `MaskFace` | `masks` list, `MaskFace.PutOnMask` / `RemoveMask`, `GetClothingIds` (`:1508`) | |
| Attire | Derived | `DefineCurrentAttire()` (`:1377`) into `Attire` enum; `GetValidClothes()` (`:1423`) | |
| Skeletonize | `KillMethod.Skeletonize` or `Contact_Skeletonizer` | `SkeletonizeAccessor(bool noSkeleton)` (`:1861`) | Drops both hands, spawns a skeleton prefab, drips blood, pushes the body to y=500, then dies. |
| Die | Health <= 0, or kill timer | `DieAccessor(bool noEffects)` (`:1704`) | See Death. |
| Obliterate | `KillMethod.Obliterate` | `Obliterate()` (`:1655`) | Spawns gibs, then `HouseManager.CreateAndSetNewPlayer` if BrainPlayer. |
| Possession switch | Jaunt, or respawn | `SetMainPlayer(GameObject)` (`:1934`), `SetNotMainPlayer` (`:1980`) | Swaps the `Brain` to `BrainPlayer`, camera, canvases, `HouseManager.mainCamera`, `Feedback_Sender.postVolume`. |

### Public members a mod can call

Most of these are safe. Call them on the player's `Player_Abilities` component.

- Give an object in hand: `Pickup(Hands hand, GameObject newPickup)` (`:1120`). Requires `newPickup` to have a `Usable` with no current `player`, and the hand must be empty. It does the blacklist check and the physics setup. Does not move the object; the object should be near the player. `CenterPickup` pulls the held object toward the hand each `FixedUpdate`.
- Drop or throw: `Drop(Hands)`, `Throw(Hands)`, `DropOnClickUp(Hands)`.
- Use the held item: `Use(Hands)`, `PickupUseOnly()`.
- Wear and remove clothes: `PutOnClothes(GameObject, int)`, `RemoveClothing(int, bool)`.
- Needs actions: `PlayerEat(float)`, `PlayerDrink(float)`, `Burp()`, `Shit()`, `Sniff()`, `Yawn()`, `Breath()`.
- Damage: `Punch(Hands, GameObject)`, `DamageOther(GameObject, Vector3)`, `Kick()`.
- Death: `DieAccessor()`, `DieAccessor(bool noEffects)`, `SkeletonizeAccessor(bool)`, `Obliterate()`.
- State: `dead`, `pickups`, `selectedHands`, `lastUsedHand`, `currentAttire`, `masks`, `clothings`, `lastIngestionWasFood`, `killtimerCurrent`, `killDelay`, `killMethod`.
- Events: `OnPreDeath` (`Action`), `OnDelayedKill` (`UnityEvent`), `pickupAction` (`Action<Hands>`, cleared after each `ExecuteAction`).

### `Usable` interaction

`Player_Abilities` depends on several `Usable` members (defined in `Usable.cs`, not in this area): `player`, `broken`, `button`, `lever`, `throwForceModifier`, `pickedByBlacklist`, `pickedByWhitelist`, `relegateToUsable`, `mimicPhysicsIgnoreObjs`, `dontRemoveIgnorePhysics`, `pickupRotationDampener`, `offset`, `heldRotation`, `saveId`, `OnPickup`, `OnUse`, `pickupEvent`, `useEvent`. Methods used: `SetPlayerRef`, `ResetPlayerRef`, `PickedUp`, `DropHand(int)`, `InsertInHands`, `Damage`, `Rotate`, `RaiseLower`, `ResetOffsets`.

## Movement and camera

### `Player_Movement` (`Player_Movement.cs`)

- Walking force: in `FixedUpdate` (`:398-402`), `AddRelativeForce(normalized(lat*0.02, 0, fwd*0.02) * max(|lat|, |fwd|) * speed.GetVal() * externalSpeedMult, Impulse)`. `externalSpeedMult` (default 1) is a public multiplier.
- Damping: horizontal velocity multiplied by 0.8 when sprinting, 0.5 otherwise (`:408`, `:413`, `:420`).
- Sprint: `Sprint` intent. Animator speed 1.5 while held, 1.0 otherwise (`:263-272`). Step timer advances faster.
- Crouch: `Crouch` intent toggles `isCrouching` (`:283-298`). Fires `OnCrouch` / `OnStand`. Capsule is center (0,-0.6,0), height 0.55 when crouched, center (0,0,0), height 1.67 when standing (`:343-344`, `:361-362`).
- Forced crouch: if a head-room raycast hits `GroundCeilingMask`, the body is held in crouch (`:299`).
- Sitting and resting: `isSitting` and `isResting` are set by `Sit_Enabler` and `Rest_Enabler` through `OnCrouch`. Resting sets `FreezeAll` constraints (`Rest_Enabler.cs:147`).
- Gravity: when a downward ray 0.1 m below the feet hits ground, `useGravity` is off and the Y position is lerped to the ground (`:384-391`). Otherwise gravity is on. A teleport in the air will fall.
- Slip: `Slip` (`:528`) sets `slipped`, adds torque and force, may drop each hand with 20% chance, waits 0.8 s, then applies `playerStats.health.Affect(...)` (`:560`). `SlipRecover` (`:577`) stands the body back up over about 1 s.
- Fat speed debuff: `CheckApplyFatSpeedDebuff` (`:214`) adds the `speedEffectFat` status effect above fat 33 and 66.
- Dream and sleep: while sitting, resting or crouched with the eyelid HUD active, `dreamTimer` counts. After 5 s it saves once. After 20 s it saves and calls `SceneManager.LoadScene("DreamZone")` (`:310-326`), unless energy is at 100, which resets the timer.

### `CameraControl` (`CameraControl.cs`)

- Attached to the child camera. Reads `mainBrain.intents`.
- `initCamPos = (0, 0.5345, 0)` (`:105`).
- Pitch: `yRotate` clamped to +/-89.8 degrees from `LookUpDown` times `Feedback_Sender.mouseSensitivity` (`:216`). Yaw is applied to the body in `Player_Movement.Update` (`:256-261`).
- Cursor: locked and hidden during play, released when HUD, `Lockview` or the `Feedback_Sender` menu is active (`:206-218`).
- `Start` sets `Application.targetFrameRate = 120` and `vSyncCount = 0` (`:102-103`). Side effect for any mod that depends on frame timing.
- Eyes: `eyesForcedShut` closes the eyelids. `ForceEyesShut()` sets it when knockout > 0 (`:130`), and `OnChanged` on knockout calls it (`:110`).
- Eyelid HUD: when `HUD` is active, the UI shows the eyelids and enables the four need buttons (`:141-163`).
- `SetSymbolRenderers(bool)` (`:221`) toggles the UI symbol renderers.
- `mainBrain` is a property with a setter (`:84-94`).

### `MaxCamera`, `Orbitcam`, `ZoomCam`, `CamAnim`

These are generic camera helpers. None of them is referenced from other `.cs` files in the decompiled code, so they are presumably placed on objects in scenes or prefabs.

- `MaxCamera` (`MaxCamera.cs`): 3ds-Max-style orbit camera. Right mouse rotates, mouse wheel and `PickupRotation` axis zoom, left click drags the target. `desiredDistance` is clamped to [`minDistance` 0.6, `maxDistance` 20]. Used by `CamAnim`.
- `Orbitcam` (`Orbitcam.cs`): orbits `go` at `speed` (default 2), height offset `height` (default 0.5), always looking at `go`.
- `ZoomCam` (`ZoomCam.cs`): sine-wave wobble of the camera around `go`.
- `CamAnim` (`CamAnim.cs`): on enable, finds a `MaxCamera`, waits `t` seconds, multiplies `desiredDistance` by `mult` (default 2) for `t` seconds, then restores it.

### Teleporting the player

The player is a `Rigidbody` with a `CapsuleCollider`. There is no `CharacterController` in this area. Existing code writes `transform.position` directly:

- `KillPlane.cs:15,20`: moves any `Usable` or `BrainPlayer` object to `HouseManager._.packageDropOffPoint.position + Vector3.up * 2`.
- `Teleporter.cs:15-22`: on trigger enter with tag `Player`, adds `offset` to the player's transform, and to `pickups[0]` and `pickups[1]`. Moving held objects too is worth copying.
- `Rest_Enabler.cs:156-158` and `ResetStanding` (`:190`): same approach, bed-related.

Suggested teleport for a mod:

```csharp
Player_Abilities pa = HouseManager._.player.GetComponent<Player_Abilities>();
Rigidbody rb = HouseManager._.player.GetComponent<Rigidbody>();
Vector3 target = ...; // a point slightly above the floor, e.g. groundPoint + Vector3.up * 0.9f
Vector3 delta = target - pa.transform.position;
rb.velocity = Vector3.zero;
rb.angularVelocity = Vector3.zero;
rb.position = target;                 // or transform.position = target, as the game does
for (int i = 0; i < 2; i++) if (pa.pickups[i] != null) pa.pickups[i].transform.position += delta; // optional, held items
```

Caveats:
- `Player_Movement` snaps Y to the ground below the feet (`:384-391`), so a point well above the floor will fall and a point inside the floor will be pushed up.
- If `Player_Movement.slipped` is true, teleporting may fight `SlipRecover`. Check `slipped` first.
- The safe respawn point the game uses is `HouseManager._.packageDropOffPoint` plus 2 m up.

## Other components

| Class | File | Purpose |
|---|---|---|
| `ActorMovement` | `ActorMovement.cs` | Empty `Start` and `Update`. |
| `Hands` | `Hands.cs` | Enum: `left=0, right=1, both=2, none=-1`. |
| `KillPlane` | `KillPlane.cs` | On collision: destroys `DustBall` usables, moves other usables and `BrainPlayer` objects to `packageDropOffPoint` + 2 m up. |
| `Teleporter` | `Teleporter.cs` | Trigger that moves the tagged `Player` by `offset` (and held items). |
| `HygieneDecreaser` | `HygieneDecreaser.cs` | `IPickupTriggerable` / `IDropTriggerable` on a usable. Sets the picker's `handlingDirtyItemLeft/Right` flag. See Gotchas. |
| `ClosedEyeTrigger` | `ClosedEyeTrigger.cs` | Trigger that fires its `EventConnector` after the player's HUD (eyelid menu) is held inside it for `timeToTrigger` seconds, up to `timesCanExecute` times. |
| `NakedTrigger` | `NakedTrigger.cs` | Trigger that spawns a `Pumpkin` pickup after the naked player stays for 3 s. Tracks `isNaked` and `consumed`. |
| `Rest_Enabler` | `Rest_Enabler.cs` | Bed trigger. On player crouch inside the trigger, starts resting (freezes body). `RestExternal(GameObject)` forces it. Persisted via `ISaveableObj`. |
| `Sit_Enabler` | `Sit_Enabler.cs` | Chair trigger, same pattern as `Rest_Enabler` with `SitInitiate` and `SitExternal`. |
| `IntentHolder` | `IntentHolder.cs` | Per-brain input state, described under Input flow. |
| `Bladder`, `Energy`, `Hunger`, `Hygiene`, `Health`, `Speed`, `Wetness`, `Disgust`, `Knockout`, `BodyType`, `Soul` | respective files | Thin `Stat` subclasses. `Bladder` / `Energy` / `Hunger` / `Hygiene` / `Soul` take a `Symbol` and `VertexShakeA` for the UI. `Speed` sets `sticky`. `Disgust` and `Knockout` start at -0.01. |
| `HealthOnly` | `HealthOnly.cs` | Non-player `ActorStats` with a single `Health` that ignores all effects. |

## Modding notes

### Reading and setting needs

- Hunger, bladder, hygiene, energy: `ps.hunger.SetVal(100f)`, `ps.bladder.SetVal(100f)`, `ps.hygiene.SetVal(100f)`, `ps.energy.SetVal(100f)`, where `ps = HouseManager._.player.GetComponent<PlayerStats>()`. Use `SetVal`, not `ForceSetVal`, so `OnSubZero` and `OnMaxed` handlers still run. Setting bladder to 0 will trigger the pee or poop handler.
- Health: `ps.health.SetVal(v)`. Setting it to 0 kills the player (see Death).
- Knockout: `ps.knockout.SetVal(v)`, with `v > 0` to knock out and `v <= 0` to wake. Going from >0 to 0 fires `OnSubZero`, which calls `SlipRecoverAccessor`.
- Disgust: `ps.disgust.SetVal(v)`. Above 50 triggers vomiting.
- Freeze all needs: `ps.pausedNeeds = true`. This stops every passive decay in `PlayerStats.FixedUpdate`.
- Money: lives on the `Computer`, not on the player. Not covered here; see the Computer documentation.

### God-mode style patch targets

- Simplest: `ps.invincible = true`. Blocks damage applied through `Stat.Affect`. Does not block direct `SetVal`/`AddVal` calls, such as the Knockout and Hunger changes.
- Death: `Player_Abilities.DieAccessor(bool noEffects)` (`Player_Abilities.cs:1710`). The parameterless `DieAccessor()` (`:1706`) calls it, and so does `PlayerStats.Start`'s delegate (`PlayerStats.cs:211`), which is bound to the parameterless overload. A Harmony prefix on the bool overload that returns false covers all three entry points. Note that the prefix must also restore health, or `Health` stays at 0 and the next `SetVal` to 0 will re-enter. Alternatively, subscribe to `Health.OnSubZero` and reset the value, but the delegate order is not defined, so prefer the prefix.
- Other kill paths: `Player_Abilities.Obliterate()`, `SkeletonizeAccessor(bool)`, `Contact_Skeletonizer`, `Dialogue.cs:303-323`. Patch these too if you want full immunity.
- Knockout and slip: patch `Player_Movement.SlipAccessor*` to no-op to stop the slip reaction. Knockout itself has no separate guard.
- Frozen crouch or energy: patch `Player_Movement.Update` energy lines (`:330-335`).

### Events and delegates to subscribe to

- `PlayerStats.health.OnSubZero`: fired once when health hits 0. `PlayerStats.Start` already subscribes `DieAccessor`, so append with `Delegate.Combine` if you add a handler.
- `PlayerStats.bladder.OnSubZero`: pee or poop trigger.
- `PlayerStats.knockout.OnNotZero` / `OnSubZero`: slip on, recover off.
- `PlayerStats.disgust.OnChanged`: `FluidGun` vomit check.
- Any stat's `OnChanged`, `OnMaxed`, `OnNotZero`: fired on every `SetVal`.
- `Player_Abilities.OnPreDeath`: fired before death, unless `noEffects`.
- `Player_Abilities.OnDelayedKill`: UnityEvent fired when `killtimerCurrent` drops below 0 (requires `killDelay > 0`).
- `Player_Abilities.pickupAction`: `Action<Hands>` set by callers, cleared after `ExecuteAction`.
- `Player_Movement.OnCrouch` / `OnStand`: used by `Rest_Enabler` and `Sit_Enabler`.
- `Brain.OnLocationChange` and `Brain.OnDestroyAction`: used by `Player_Abilities.Start` and `ReapQueue`.

### Gotchas

- Stat clamp: all stats are clamped to [0, 100] by `SetVal`. `Disgust` and `Knockout` start at -0.01 and only clamp after the first `SetVal` or `AddVal`.
- `ForceSetVal` and `ForceSetLastVal` bypass events and clamps. Used by `PlayerStats.Load` (`:156-157`).
- `PlayerStats.Awake` replaces all stat objects. Do not cache `Stat` references before `Awake`.
- `Stat.value` and `initValue` are `internal`. Use `GetVal()`, or reflection if you must.
- `Speed` is sticky, so `StatusHandler.Update` resets it to `initValue` every frame. A direct `SetVal` on `speed` is overwritten next frame. To change speed permanently, change `speed.initValue` via reflection, or use a persistent `StatusEffect` with `linearDelta` or `multiplier`.
- `Stat.Affect` multiplies Impact damage by `impactRes.GetVal()` and Cut by `cuttingRes.GetVal()`. Their values are asset data.
- Sign convention: all damage callers pass negative potency to `Affect` (for example `Cutter.cs:55`, `Impacter.cs:33`). `Player_Movement.Slip` passes a positive value (`Player_Movement.cs:560`), and `DamagePlayer` returns it unchanged. By the normal convention that heals the player. This may be a bug, or it may depend on asset values. Verify in-game before relying on it.
- `HygieneDecreaser.DropTrigger` (`HygieneDecreaser.cs:33`) sets `handlingDirtyItemRight = true` when dropping from the right hand. Its counterpart for the left hand (`:30`) sets it to false. This looks like a bug. The right-hand dirty flag may stay on after a drop, which makes hygiene decay 0.3 per second instead of 0.04.
- `StatusHandler.Update` (`:175-182`): the duration countdown runs only for the last element of `effects` (the test `effects.Count - 1 == num` is true only for the last index). Other effects may not expire until they reach the end of the list. Verify in-game.
- `StatusEffectType.amplifier` appears inert. `AddEffect` clears `st.statRef` on the asset (`StatusHandler.cs:199`), then loops over `st.statRef` (`:251`), which is empty. So the `OnChanged` hook is never attached.
- `persistent` effects apply `effectSize` every frame without `Time.deltaTime`. The total effect depends on frame rate.
- Calling `IntentHolder.Intent.Active` schedules a state update for the next frame (`IntentHolder.cs:23`). Reading `Active` in a patch can change the state seen by later code.
- `Player_Abilities.Breath` adds `hunger` +2 per frame while exhaling with a cigarette mask (`:717-718`). That is per frame, not per second.
- `PlayerStats.Start` can run again through `Load`. It is guarded by `alreadyStarted`. `StatusHandler.Start` and `Player_Abilities.Start` have the same guard.
- Teleport to a point below the ground or inside geometry may cause the body to be pushed by collision.

## Open questions

- Starting values for every stat (`initValue` on each serialized stat) and the movement `Speed` value. These come from the prefab, not the code.
- Contents of the `StatusEffect` assets (durations, sizes, operations). Only the names referenced in code are known.
- Whether `Player_Movement.Slip`'s positive `Health.Affect` (`:560`) is a bug or intended, and what happens to health after death.
- Whether the `StatusHandler.Update` duration logic (`:175-182`) is intended.
- Whether the `amplifier` status effect type is ever used in asset data.
- `FluidGun` pee and puke behaviour (what bladder and disgust do while dispensing). This area only covers `SetFluid`. Not read in detail.
- `ImpactResistance`, `CuttingResistance` values and their `Stat` behaviour: separate files, not read.
- Whether `Player_Movement.Update` (crouch and sit energy) runs before `Player_Abilities.Update` each frame. Script execution order is not set in code.
- `BrainPlayer.Update` calls `base.Update` or not; not verified. The `new` keyword hides `Brain.Update`.
- Whether `Feedback_Sender.mouseSensitivity` is a static field (used as `Feedback_Sender.mouseSensitivity`), and whether it is saved.
- The real uses of `MaxCamera`, `Orbitcam` and `ZoomCam`. They are not referenced from code, so they are presumably scene or prefab components.
- Exact effect of `Usable.Rotate`, `RaiseLower` and `ResetOffsets` (in `Usable.cs`, not read).
- `ClosedEyeTrigger` event wiring: depends on the `EventConnector` setup in the scene.
