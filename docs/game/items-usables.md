# Items & usables

Scope: the `Usable` component and the item classes that sit on top of it (food, liquids, tools, bathroom, wearables, occult props). All paths are relative to `decompiled/Assembly-CSharp/`. Line numbers refer to those files. Anything not read or confirmed in the decompiled code is marked **(unverified)**.

## Overview

**What makes something an item.** A pickup is a GameObject with:

- a `Usable` MonoBehaviour (`Usable.cs:8`), which is what the player can grab, hold, drop, throw and use;
- a `Rigidbody` (`Usable.Save()` dereferences `GetComponent<Rigidbody>().velocity` at `Usable.cs:241`, so a Usable without one breaks saving);
- a `Collider` (`Usable.Start` and `Player_Abilities.Pickup` call `GetComponents<Collider>()`);
- the `Usable` layer (`Player_Abilities.cs:1123`-ish checks `frontHit.collider.gameObject.layer == LayerMask.NameToLayer("Usable")`; `Usable.SetBaseUsable()` at `Usable.cs:738` sets it);
- a `prefabName` string that must equal the key the prefab is registered under in `PickupBank.pickupsDict` (see the Modding section).

Behaviour is added by **extra MonoBehaviours on the same GameObject**. Most item classes implement `Usable.IUsable` (`Usable.cs:70-73`), a one-method interface, `void Use()`. The `Usable` component itself has no `Use()`.

**How use is dispatched.** The chain for a held item, from Player_Abilities:

1. The per-frame input block in `Player_Abilities` (around `Player_Abilities.cs:633`) runs `ExecuteAction(Use)` when `mainBrain.intents.Use.Active` (`Player_Abilities.cs:633`). `Active` is driven by the input's held state (`BrainPlayer.cs:121`, `intents.Use.UpdateState(i_use != 0)`), so it is a hold state, not a click. **Use is therefore called every frame the button is held, unless something gates it.** Gating used by the item classes: `Usable.PreventOtherUse()` (sets `player_Abilities.cancelledClick`), fire-rate timers (`Gun`), and the `Use()` implementations themselves. Exactly how `cancelledClick` gates the per-frame call was not traced in full **(unverified)**.
2. `ExecuteAction(Use)` calls `Player_Abilities.Use(Hands)` (`Player_Abilities.cs:1317-1326`) for each occupied hand. That method calls **every** component on the held object that implements `Usable.IUsable`, via `GetComponents<Usable.IUsable>()`. Several IUsable components on one object all fire (for example `Egg_Carton.Use` and `PrefabContainer.Use` both run on the egg carton).
3. `Usable.useEvent` (a `UnityEvent`) and `Usable.OnUse` (an `Action`) are **not** fired by `Player_Abilities.Use(Hands)`. They are fired only from:
   - `Player_Abilities.PickupUseOnly()` (`Player_Abilities.cs:1095-1110`), when the object in front has no IUsable, it invokes `useEvent` instead;
   - the button branch of `Player_Abilities.Pickup` (`Player_Abilities.cs:1192-1210`), which calls IUsable.Use, then `useEvent`, then `OnUse`, then `OnPickup`;
   - `Phone_Button.cs:11` and `EventConnector.cs` (other helpers' areas).

   Practical rule: to hook "use" from a held item, implement `IUsable` (or patch `Player_Abilities.Use(Hands)`). `useEvent` is only a path for buttons, levers and world-placed things. `OnUse` is set by `Phone_Interface.cs:63` and fires only in the button branch.

4. `Usable.relegateToUsable` (`Usable.cs:181`) redirects pickup. `Player_Abilities.Pickup` replaces the picked object with `relegateToUsable.gameObject` (`Player_Abilities.cs:1148-1150`-ish). `Food.Stack` uses this to make the stacked food's root the thing held. `GetControlUsable()` (`Usable.cs:203`) returns that target or self.

**Pickup and drop.** `Player_Abilities.Pickup(Hands, GameObject)` (`Player_Abilities.cs:1120`) refuses when the hand is occupied, the Usable has no `player`, or the blacklist/whitelist rejects the picker. Otherwise, for a normal item it:

- sets `pickups[hand]`;
- `Physics.IgnoreCollision` against the player's collider, plus `mimicPhysicsIgnoreObjs`;
- calls every `Player_Abilities.IPickupTriggerable.PickupTrigger(player)` on the object;
- fires `OnPickup` and `pickupEvent` (`pickupEventOnceOnly` / `pickupEventOnceConsumed` gate repeats);
- calls `PickedUp()` and `SetPlayerRef(player)`.

Buttons and levers take a separate branch (`button` / `lever`) and are never held in the normal sense.

**Item lifecycle.**

1. **Spawn.** Either `Object.Instantiate` of a prefab from `PickupBank.pickupsDict` (`PickupBank.cs:71`, `:77`), a dispenser (`PrefabContainer.TakeOne`), a `Spawner`, a `Food` swap (`Food.Cook`, `Spoil`, `SwapWithWateredObj`), or a `Sectionable` cut.
2. **Start.** `Usable.Start()` (`Usable.cs:276-328`) runs once (guarded by `alreadyStarted`). It caches `offset` / `heldRotation`, creates a collision AudioSource, ignores listed colliders, subscribes the save callback (`SubSaveUsable`, unless `dontSave`), adds an achievement hook to `OnBreak`, and schedules `disableOnStart`. Subclass `Start()` methods run separately, so ordering between them is unspecified.
3. **Load path.** `SaveLoadHandler.LoadUsables` (`SaveLoadHandler.cs:232-254`) instantiates the prefab, restores `saveId`, calls `Usable.Start()` **by hand**, restores name / scale / active state, applies sticky joints, calls `DamageRaw(damage)` (`Usable.cs:649`), restores velocity, then calls `Load(JToken)` on every matching `ISaveableObj` sub-save.
4. **Pickup.** See above. `Rotate`, `RaiseLower`, `ResetOffsets` tune the held pose.
5. **Use.** `IUsable.Use()` on each held-item component, every frame while the button is held.
6. **Damage and break.** `Usable.Damage(val, EffectType)` (`Usable.cs:631`) accumulates `damage`. At `damage >= 4` the item is `broken`, and burning additionally calls `Burn()`. `OnBreak` is fired by `Burn()`. Flame heat is the main source (`Flame.cs:416-426`).
7. **Burn.** `Burn()` (`Usable.cs:663-720`) swaps every renderer's materials to `Resources/Materials/Burntfood`, marks `Offering.IsBurnt`, fires `OnBreak`, and destroys the object if `destroyOnBurn`.
8. **Save.** `Usable.Save()` (`Usable.cs:212-247`) builds a `SaveDataUsable` (see below) if `prefabName != ""`, `!lever && !button`, and `!dontSave`. Sub-saves come from child `ISaveableObj` components. The `SaveLoadHandler` writes the list on save.
9. **Destroy.** `OnDestroy` (`Usable.cs:249-260`) unsubscribes the save callback, fires `OnDespawn`, and removes the object from its room's usable list.

---

## Usable (deep dive)

`Usable : MonoBehaviour, ISaveableUsable, ISaveable, Spawner.ISpawnable` (`Usable.cs:8`). `Spawner.ISpawnable` is just `event Action OnDespawn` (`Spawner.cs:30-33`).

### Nested types

| Type | Location | Purpose |
|---|---|---|
| `enum StoreType { Nowhere, Normal, BlackMarket, OccultMarket }` | `Usable.cs:10-16` | Which shop / market lists the item. |
| `class SaveDataUsable : SaveData` | `Usable.cs:18-68` | Serialized item record (see below). |
| `interface IUsable { void Use(); }` | `Usable.cs:70-73` | Per-item use behaviour. |

### Fields, grouped

**Handling**

| Field | Line | Notes |
|---|---|---|
| `twoHanded` | 75 | Not read in my area. |
| `button`, `lever` | 77, 79 | Not held. Never saved. Routed to the button/lever branch in `Pickup`. |
| `currentLocation` (`Location`) | 81 | Set in `OnTriggerEnter` from a `Locator` on the `Location` layer (`Usable.cs:623-629`). Used by `Locator.GetRoom`, `Locator.GetLightSwitch`, `StartRoomFire`. |
| `offset`, `initOffset`, `RaiseLower` | 84, 86, 407 | Hand offset along z, clamped to `initOffset.z ± 0.28`. |
| `heldRotation`, `initRot`, `Rotate`, `ResetOffsets` | 88, 90, 393, 401 | Pose. `invertXRot`, `invertYRot` flip input. |
| `centeringMultiplier`, `throwForceModifier`, `pickupRotationDampener` | 96, 98, 101 | Tuning. `SetBaseUsable` sets `throwForceModifier = 0.2`. |
| `pickedByBlacklist`, `pickedByWhitelist` | 114, 117 | `GameObject[]`, who may not / may pick it up. Enforced in `Player_Abilities.Pickup` (`Player_Abilities.cs:1137-1140`). |
| `player` (`GameObject`), `player_Abilities` | 125, 127 | Set by `SetPlayerRef`, cleared by `ResetPlayerRef`. Null means not held. |
| `lastTouchedByRef` | 183 | Last player to pick it up. Used as the attacker in `Cutter.CutDamage`. |
| `playerRefSetThisFrame` | 172 | Private. Guards `ResetPlayerRefNextFrame`. |
| `relegateToUsable` | 181 | Pickup redirect target. |
| `dontRemoveIgnorePhysics` | 109 | Not read in my area. |
| `ignoreCollisionWith` (`Collider[]`) | 111 | Ignored in `Start`. |
| `mimicPhysicsIgnoreObjs` (`List<GameObject>`) | 123 | Extra objects the player ignores. Food stacking uses it. |

**Physics & sound**

| Field | Line | Notes |
|---|---|---|
| `sticky` | 105 | Creates a `FixedJoint` on collision when `thrown` (`Usable.cs:591-603`). `Soaker.makeStickyOnSoaked` and `Wearable` also flip it. |
| `thrown` | 107 | Set by player throw code. `ThrowHit` clears it after `WaitForFixedUpdate`. |
| `collisionSounds` (`AudioClip[]`), `collisionAudioSource` | 119, 121 | Random clip per collision above speed threshold `sqrMagnitude > 2`. Note: a `UnityEvent useEvent` is created in `Start` **only when `collisionSounds` is non-empty** (`Usable.cs:292-295`). |
| `dontRemoveIgnorePhysics` | 109 | See above. |

**Heat / damage / break**

| Field | Line | Notes |
|---|---|---|
| `heat`, `Heat` property | 141, 189 | **No references outside `Usable.cs`** (grep of all decompiled files for `heat`/`Heat` found only Flame / HeatHolder / HeatLevel members). Burning is driven by `damage`, not `heat`. Treat as unused. |
| `flammability` (`Flammability`) | 144 | Multiplier for `Burn` damage. See formula. Tooltip: it does not affect cook time on food. |
| `damage`, `timeLastDmg` | 146, 148 | Accumulated damage. Rate-limited to one hit per 0.25 s. |
| `broken` | 150 | Set when `damage >= 4`. A broken item cannot be picked up as a button and no longer takes damage. |
| `nonFlammable` | 152 | `Burn()` returns immediately. |
| `destroyOnBurn` | 154 | `Burn()` destroys the GameObject. |
| `OnBreak` (`Action`) | 170 | Fired by `Burn()`. Many classes add to it (LiquidBomb, Lighter_BBQ, FireExtinguisher, LiquidContainer via BreakOnImpact, PrefabContainer, Fuse, SmokeDetector, Cigarette, `Sectionable` cut-burn). |
| `OnDespawn` (`event Action`) | 201 | Fired in `OnDestroy`. Used by Spawner counters and `LiquidContainer` (refrigerator count), `ThreadMarker`. |

**Store (online shop / market)**

| Field | Line | Notes |
|---|---|---|
| `purchaseableAt` (`StoreType`) | 157 | Which shop. `Nowhere` means not sold. See `StoreGenerator.cs:22`, `Computer.cs:245-249`, `DreamRandomizer.cs:105`. |
| `price` (`float`) | 159 | Dollar price. Read by the computer shop, box/market valuation, Piggybank, Dialogue offerings (`Dialogue.cs:658`), PrefabContainer. |
| `description` | 161-162 | `[Multiline]` text. The UI that displays it was not traced **(unverified)**. |
| `pictureRotation`, `pictureScaleMod`, `storePicture` (`Sprite`) | 164-168 | Store thumbnail presentation. Consumer not traced in my area **(unverified)**; `Pickup_UI_Renderer` only declares fields (`Pickup_UI_Renderer.cs`). |

**Saving**

| Field | Line | Notes |
|---|---|---|
| `prefabName` (`string`) | 175 | **Serialized in the prefab; never assigned in code.** It is the key into `PickupBank.pickupsDict`. Empty means not saved. The only writes in the decompiled code are the Brain/Usable constructors. |
| `dontSave` | 177 | Excludes the item from save. `Wig` toggles it. |
| `disableOnStart` | 179 | Deactivates 0.2 s after `Start` (`Usable.cs:323-325`). Saved and restored. |
| `saveId` | 187 | Stable id for cross-references. `GenerateSaveId()` assigns from `SaveLoadHandler.IdSequence` if 0 (`Usable.cs:262-269`). |
| `alreadyStarted` | 185 | Guard for `Start`. |

### Public methods

| Method | Line | What it does |
|---|---|---|
| `GetControlUsable()` | 203 | Returns `relegateToUsable` or self. |
| `Save()` | 212 | See Lifecycle. Returns null when `dontSave`, when `prefabName == ""`, or for `lever` / `button`. |
| `GenerateSaveId()` | 262 | Lazily assigns `saveId`. |
| `GetUsable()` | 271 | Returns self (`ISaveableUsable`). |
| `Start()` | 276 | Public. Called manually by `SaveLoadHandler.cs:255` and by `PrefabContainer.Load`, `Microwave`, etc. Guarded by `alreadyStarted`. |
| `SubSaveUsable()` | 330 | Subscribes `Save` with `SaveLoadHandler._.SubscribeSave`. Also called by `Wig`. |
| `PickedUp()` | 339 | If `sticky`, destroys the FixedJoint, re-enables gravity, unparents. |
| `SetPlayerRef(GameObject)` | 349 | Sets `player`, `player_Abilities`, `lastTouchedByRef`; starts a frame-tracker coroutine. |
| `ResetPlayerRef(bool delay = false)` | 364 | Clears refs now or next frame. Button items keep the ref. |
| `ResetPlayerRefTracker`, `ResetPlayerRefNextFrame` | 377, 383 | Coroutines behind the above. |
| `Rotate(Vector2)` | 393 | Adds to `heldRotation` from look input. |
| `ResetOffsets()` | 401 | Restores `initOffset` and `initRot`. |
| `RaiseLower(float dir)` | 407 | Moves `offset.z` by `dir * 0.1`, clamped to ±0.28 of `initOffset.z`. |
| `IsHandEmpty(GameObject, int hand)` | 412 | Checks `player_Abilities.pickups[hand] == null`. |
| `GetOtherHandItem()` | 427 | The item in the other hand, or null. Used by Food stacking, DustBall merge, GunMagazine, Piggybank. |
| `IsHeld()` | 444 | True when held in the left slot only. Note the check is `pickups[0]` only. |
| `HeldHandId(bool getOtherHand = false)` | 458 | 0 / 1 for the hand it is in, `-1` otherwise. |
| `InsertIntoOtherHand(GameObject)` | 484 | Puts a newly spawned item into the other hand. Used by `PrefabContainer`, `Toilet_Paper_Roll`. |
| `InsertInHands(...)` (2 overloads) | 492, 497 | Places an object in a hand, optionally dropping what is there first. Used by `CatFoodBowl`. |
| `DropHand()` / `DropHand(int hand = 3)` | 534, 543 | Drops the item from its hand. `3` means "both". |
| `GetHandPos(int handId = 3)` | 555 | World position 0.4 m in front of the camera, offset ±0.3 for left/right. |
| `PreventOtherUse()` | 565 | Sets `cancelledClick` so the same press does not trigger other actions. Used by almost every IUsable. |
| `OnCollisionEnter` / `OnJointBreak` / `OnTriggerEnter` | 579, 615, 623 | Sticky joint creation, gravity restore on joint break, location tracking. |
| `Damage(float val, EffectType)` | 631 | See damage formula. Returns `damage`. |
| `DamageRaw(float val)` | 649 | Sets `damage` directly and calls `Burn()` if `>= 4`. Used when loading. |
| `Burn()` | 663 | See Lifecycle. Note that `DamageRaw` also calls `Burn()`, so loading a broken item re-runs the burn visuals. |
| `SetBaseUsable()` | 723 | `[ContextMenu]`, editor-only. Adds BoxCollider, Rigidbody (mass 3, drag 1, angular drag 1, ContinuousDynamic), sets throwForceModifier 0.2, layer `Usable`. |
| `GetSizes()`, `FindSurface(Rigidbody, Vector3)` | 741, 755 | Raycast-based bounding size along local axes. Used by Food stacking. |
| `GoToDialogueFunctions(string)` | 777 | Switches the Ink flow to `Usable` and jumps to a knot. |

### Damage, breaking and burning

Damage formula (`Usable.cs:631-647`):

- Applied only if `!broken` and `Time.time > timeLastDmg + 0.25`.
- `damage += val * multiplier`, where `multiplier = flammability` for `EffectType.Burn` and `Flammability.Resistant` (1) for every other effect.
- `Flammability` (`Flammability.cs`): `Instant = 100`, `Quick = 10`, `Average = 5` (default), `Slow = 2`, `Resistant = 1`, `Unaffected = 0`.
- When `damage >= 4`: if the effect is Burn, `Burn()` runs; then `broken = true`.

`DamageRaw` (`Usable.cs:649`) assigns `damage = val` and breaks at `>= 4`, calling `Burn()` regardless of type.

Callers:

- `Flame.Damage(Usable)` (`Flame.cs:416-426`): burn damage per frame = `Time.deltaTime * HeatLevel * 0.3`. If the object is uncooked food (`timeToCook != 0 && !IsCooked`), heat goes to cooking instead (see Food). If the item is not a Flame and the returned damage is `>= 1`, `MakeFlame` is called.
- `SaveLoadHandler.cs:254`: `DamageRaw(saveDataUsable.damage)` on load.
- `Sectionable.cs:58-121`: calls `Burn()` on cut pieces when the parent was `broken`.

`HeatLevel` (`HeatLevel.cs`): `Cold=0, Low=1, Medium=2, High=3, Lava=4`.

### SaveDataUsable

Fields (`Usable.cs:19-47`): `saveId`, `stickyId`, `prefabName`, `instanceName` (the GameObject name), `position`, `rotation`, `scale`, `velocity` (float arrays), `damage`, `isChild`, `isKinematic`, `isActive`, `pickupEventOnceConsumed`, `disableOnStart`, plus base `type` and `subSaveData` (a `List<SaveData>`).

- `stickyId`: `-1` = no FixedJoint; `0` = joint with no body; `> 0` = joint connected to an `ISaveableUsable`, `Door` or `Brain` id.
- `subSaveData`: produced by `GetComponentsInChildren<ISaveableObj>()`, skipping components whose type is `Usable` or the same type as the Usable (`Usable.cs:241-242`). Each element is one component's `Save()`.

On load (`SaveLoadHandler.cs:216-300`), usables are instantiated from `PickupBank.pickupsDict[prefabName].obj` (line 232, no try/catch: a missing key throws). An empty `prefabName` is only logged (`:228-230`) and then fails the same lookup. All existing non-lever/non-button usables in the scene are destroyed before loading.

---

## Food & consumption

### Food (`Food.cs`)

`Food : MonoBehaviour, Usable.IUsable, ISaveableObj, ISaveable, Player_Abilities.IPunchTriggerable` (`Food.cs:10`). Nested `interface IEatable { void Eat(); }` (`Food.cs:12-15`) lets a food class take over destruction.

**Fields**

| Field | Line | Meaning |
|---|---|---|
| `foodValueRaw`, `foodValueCooked` | 39, 41 | Nutrition when raw / cooked. Passed to `PlayerEat` as hunger gain. Values come from the prefabs **(unverified, not in code)**. |
| `_isCooked`, `_cookedMeter`, `_lastCookedMeter` | 43, 45, 47 | Cooking state. `_lastCookedMeter` starts at -1. |
| `timeToCook` | 61 | Cooking threshold. `0` means the food never cooks. |
| `cookedMeterCooldownDelayed`, `cookCooldownTimer` | 49, 51 | Hold-off before the cooked meter decays. |
| `cookedMats` (`RendMatRef[]`), `cookedMaterial`, `cookedTexture`, `cookedParticle` | 53-59 | Visuals on cook. |
| `keepPaintingCooked` | 63 | If false, `CollisionPainter` is removed on cook. |
| `rawStatusEffects`, `cookedStatusEffects` (`List<StatusEffect>`) | 65, 67 | Applied on eating. |
| `swapOnWaterObj`, `swapOnCookedObj`, `swapOnSpoil` | 69, 71, 73 | Prefab swapped in on water, cook, or spoil. |
| `dontPassEffectsDownOnSwap` | 75 | Stops status effects being copied to cut / broken children. |
| `stackable`, `stackedToObj`, `stackedUsables`, `currentStackedTopPos`, `fakeStackedFoodCollider` | 83-91 | Stacking. Max 10 per stack (`Food.cs:380`). |
| `OnEat` (`Action<Player_Abilities>`), `OnEatEvent` (`UnityEvent`) | 95, 97 | Eat hooks. |
| `spoilable`, `spoiled`, `spoilingPreventers`, `spoiledMeter` | 105-111 | Spoilage. |
| `OnSpoil`, `onCookingChg`, `onCooked` | 103, 113, 115 | Events. `Seafood_Delight` listens to `onCookingChg`. |
| `onFloor` | 117 | Food is on a floor (set on collision with a `FoodDetector`). |
| `readyToEatSteps` | 37 | Eat is refused while `> 0`. See Open questions. |
| `spoilThreshold` | 99 | `const float = 360f`. |

**Properties**

- `SpoilingPreventers` (`Food.cs:121-131`): clamped to `>= 0`.
- `SpoiledMeter` (`Food.cs:133-147`): setter spoils at `>= 360` when `spoilable && !spoiled`.
- `IsCooked` (`Food.cs:149-167`): `timeToCook > 0 && CookedMeter >= timeToCook`. The setter cooks if `_lastCookedMeter < timeToCook`.
- `CookedMeter` (`Food.cs:169-198`): setter writes `_cookedMeter`, sets `cookCooldownTimer = 15`, sets `cookedMeterCooldownDelayed = true`, raises `onCookingChg`, and if the item has just become cooked raises `onCooked` and calls `Cook()`. It also sets the cooking blend shape to `_cookedMeter / timeToCook * 100`.

**Methods**

| Method | Line | Behaviour |
|---|---|---|
| `Spoil()` | 200 | Spawns `Resources/Flies_Particle`, appends `Vomit` and `EnergyDec25Over15s` to both status lists, sets `spoiled`, fires `OnSpoil`, and optionally swaps to `swapOnSpoil`. |
| `Start()` | 232 | Caches usable, creates `stackedUsables`, subscribes to Sectionable / BreakOnImpact to pass effects to pieces, and ignores the fake stacking collider. |
| `Save()` / `Load(JToken)` | 278, 283 | Saves `CookedMeter`, `cookCooldownTimer`, `cookedMeterCooldownDelayed`. Load sets `CookedMeter` (so `Cook()` may run). |
| `Update()` | 291 | See Cooking model. |
| `Cook()` | 316 | Sets `CookedMeter` to `timeToCook` if below, swaps to `swapOnCookedObj` (destroys self after 0.02 s), switches materials/textures, plays particles, removes `CollisionPainter`. |
| `Use()` | 367 | Held food. If the other hand holds a stackable Food, stacks onto it; otherwise `GetEaten(usable.player_Abilities)`. |
| `Stack(...)` | 390, 395 | Parent-constrains this food onto another and rebuilds the stack. Calls `DropHand` first. |
| `GetEaten(Player_Abilities)` | 520 | See Eating below. |
| `OnCollisionEnter` | 607 | Registers floor food in `HouseManager._.foodOnFloor` (cap 30). `BrainRat` only registers 30% of the time. Also force-feeds if `currentlyForceFeedable` and the player is within 0.5 m of the contact. |
| `OnCollisionExit` | 629 | Removes from `foodOnFloor`. |
| `OnTriggerEnter` | 638 | If `swapOnWaterObj` and the trigger is a `Water`, calls `SwapWithWateredObj(paintChannel)`. Destroys `SpraySphere`s. |
| `SwapWithWateredObj(int liquidType)` | 650 | Instantiates `swapOnWaterObj` and adds status effects by liquid. See Liquids. |
| `OnDestroy()` | 685 | Cleans `foodOnFloor`, the flies particle, and stack links. |
| `PunchTrigger()` | 702 | Opens a 0.5 s window in which contact with the player feeds them (`ForceFeedableTimer`). |

### Cooking model

Cooked meter (`CookedMeter`) is a single float that rises from heat and falls back when unheated:

- **Heat from flames** (`Flame.cs:416-426`): each frame a Flame touches an uncooked food with `timeToCook != 0`, `CookedMeter += Time.deltaTime * HeatLevel * 0.6`. (`HeatLevel` 0..4.)
- **Microwave** (`Microwave_Interface.cs:36`): `CookedMeter += 0.04f` on each `OnTriggerStay` call while cooking. This is per physics step, not per second. Rate depends on the physics tick **(unverified)**.
- **Hold-off:** every write to `CookedMeter` sets `cookCooldownTimer = 15` and `cookedMeterCooldownDelayed = true`.
- **Decay:** in `Update`, when not cooked, the cooldown counts down; once it reaches 0, `CookedMeter -= Time.deltaTime`, clamped to `[0, 9999]`. So after heat stops, cooked progress holds for 15 s, then falls at 1 unit per second.
- **Cooked:** `IsCooked` becomes true at `CookedMeter >= timeToCook`. `Cook()` then runs once per transition.
- **Burn after cooked:** heat that reaches a cooked food goes to `Usable.Damage(dt * HeatLevel * 0.3, Burn)`. At `damage >= 4` the food is burnt.

`Seafood_Delight` inflates as `CookedMeter * 1.2 / timeToCook` and explodes once `CookedMeter * 1.2 > timeToCook` (`Seafood_Delight.cs:32-38`).

### Spoilage model

- `SpoiledMeter += Time.deltaTime` in `Update` while `spoilable && SpoiledMeter < 360 && spoilingPreventers == 0`.
- At `>= 360` and not yet spoiled, `Spoil()` runs. Threshold `spoilThreshold = 360` (`Food.cs:99`, `:142`).
- `SpoilingPreventers` is incremented by `RefrigerationZone` while a powered fridge contains the food (`RefrigerationZone.cs:23-25`, `:61-63`). The spoil timer freezes while the counter is above 0.
- `BrainRat` sets `SpoilingPreventers = 0` (`BrainRat.cs:388`).

### Eating

`Food.Use()` (`Food.cs:367`) → `GetEaten(player_Abilities)` (`Food.cs:520-605`):

1. Refused if the player is null or `readyToEatSteps > 0`.
2. `num = IsCooked ? foodValueCooked : foodValueRaw`. For each stacked food, its value is added and its status effects are applied.
3. `Player_Abilities.PlayerEat(num)` (`Player_Abilities.cs:1521-1537`): hunger `+= num`; bladder `-= num * 0.4`. Hunger over 100 raises a blend shape. Also sets `lastIngestionWasFood`.
4. Achievement counter for turds if it is an `Offering` with `isShit`.
5. `OnEat(player)` and `OnEatEvent.Invoke()`.
6. Applies this food's status effects (cooked list if cooked, otherwise raw list). Effects are added through `StatusHandler.AddEffect` on the player.
7. Destroys stacked foods.
8. If the food has no `IEatable`, drops it from the hand and destroys it. Otherwise calls `IEatable.Eat()`. `CerealBox` and `Seafood_Delight` use this route.

Forced feeding: a `Food` colliding with the player within 0.5 m of contact during the punch window calls `GetEaten` (`Food.cs:622-626`).

Stat effects are applied through `StatusHandler.AddEffect(StatusEffect)`. Status effect names referenced from code via `HouseManager._.statusEffectDict[...]`: `Vomit`, `EnergyDec25Over15s`, `DeathDelay10`, `Diarrhea`, `Health5Instant`, `Soul5Instant`. The effect definitions are in data, not code **(unverified)**.

### Food helper components

| Class | File | Role |
|---|---|---|
| `FoodDetector` (`FoodDetector.cs`) | `FloorType { Wood, Carpet, Tile, Grass, Asphalt }` | Marks a floor collider. Food landing on it is registered in `HouseManager.foodOnFloor`. `SlipperOnFloor` uses it to make the player slip. |
| `FoodObjTimeStamp` (`FoodObjTimeStamp.cs`) | struct `(GameObject foodObj, float timeStamp, Location loc)` | Entries in `HouseManager.foodOnFloor` (`Food.cs:612`). |
| `FoodValueContactIncreaser` (`FoodValueContactIncreaser.cs`) | `foodIncreaseValue`, `lerpColor` | On collision with Food: adds `foodIncreaseValue` to `foodValueRaw` and `foodValueCooked`, lerps the food's `_Color` 40% towards `lerpColor`, then destroys itself. |
| `SlipperOnFloor` (`SlipperOnFloor.cs`) | | If the object lands on a `FoodDetector` floor and the other collider is a `Player_Movement`, calls `SlipAccessor()`. |

### Specific foods

| Class | File | Behaviour |
|---|---|---|
| `CerealBox` | `CerealBox.cs` | `[RequireComponent(typeof(Food))]`, implements `IEatable`. `Use()` opens the box once (particles, sound) and decrements `readyToEatSteps`. Particles stop after `distanceTraveled >= 6` (`snackSize`) or `20`. `Eat()` drops the box, moves it to `y = -900`, makes it kinematic and destroys it after 60 s if opened; otherwise destroys it at once. Saves particle positions / velocities / lifetimes. |
| `Egg_Carton` | `Egg_Carton.cs` | Is a `PrefabContainer`-driven dispenser. `Use()` either opens the lid and enables the container after two frames, or hides one egg visual per use. Save stores `pc.enabled` and `pc.objCount`. |
| `Butter` | `Butter.cs` | Extends `FoodValueContactIncreaser`, implements `Soaker.ISoakable`. Randomises its Y rotation on Start. Destroys itself on being soaked unless the soaker is Food. |
| `PizzaBox` | `PizzaBox.cs` | On `PrefabContainer.OnSpawn`, swaps the mesh to `openedBox` and plays audio. |
| `Seafood_Delight` | `Seafood_Delight.cs` | `[RequireComponent(typeof(Food))]`. Inflates with cooking (`onCookingChg`), explodes at `CookedMeter * 1.2 > timeToCook`, spawns `chunkObj` pieces (6-15 raycasts, each piece `IsCooked = true`). `Use()` removes the lid, unsubscribes inflation, and decrements `readyToEatSteps`. |
| `IceCube` | `IceCube.cs` | Frozen water. Tint from `Water.liquidColors`. Shrinks by `dt * 0.001` per second only when `refrigeratorsCount == 0`, shrinks by 0.03 per `WaterVolume` contact, and shrinks by flame contact. At `localScale.x < 0.02` it spawns `SpraySphere_*_Grav` and destroys itself. |

### Refrigerator and RefrigerationZone

- `Refrigerator` (`Refrigerator.cs`) implements `IElectronic`. Its `IsPowered` setter toggles the parent `RefrigerationZone.enabled`, the light on the parent, and `AudioSource[2]`.
- `RefrigerationZone` (`RefrigerationZone.cs`) is a trigger BoxCollider. `requiresPower` is true when a child has `IElectronic`. When powered:
  - entering Food with `spoilable` → `SpoilingPreventers++` (freezes spoilage);
  - entering `IceCube` → `refrigeratorsCount++` and `colPaint.preventPainting = true`;
  - exiting reverses both (`refrigeratorsCount` floored at 0).
- `OnEnable` / `OnDisable` run an `OverlapBox` over the zone to catch items already inside. They are skipped during load (`SaveLoadHandler.timeToLastLoad`).
- `SpraySphere` (`SpraySphere.cs:63-77`) freezes into an `IceCube` on entering a `RefrigerationZone` when `canFreeze`.
- `LiquidContainer` counts nearby fridges and builds `frozenContentsTimer` (see Liquids).

### Microwave_Interface

`Microwave_Interface.cs`: a Usable-less controller on the microwave body.

- Keypad: `CountTimer(int)` builds a timer value in the `M:SS` format on `screenText`. Requires the player within 1 m (`:67`).
- `Cook()` (`:103-110`): requires the player within 1.8 m, `microwave_Door.closed`, and a nonzero timer.
- `StartMicrowave()` (`:112-155`): waits 1 s, then `cookTime` is computed from the timer (`< 100` seconds; `> 960` clamps to 600; otherwise `M*60 + SS`). Counts down 1 s per tick and ends with a 5-beep countdown.
- While cooking, `OnTriggerStay` for each object: Food gets `CookedMeter += 0.04`. An `ActorStats` gets a Burn hit of `-0.03..-0.05` Health (if `canDamageOther`), and `IObliterateable.Obliterate()` at zero health. It also calls `Sparker.SparkUp(other, ...)` (sparks only for water, or metal `Offering`).

---

## Liquids & water

### Liquid enum

`Liquid.cs`: `Dirt = 0, Blood = 1, Burn = 2, Water = 3, Cleaner = 4`. Used as `paintChannel` values across the codebase. `Burn` (2) has a `Water.liquidSpheres` entry but `WaterVolume.SetLiquid` never sets it **(unverified whether any object uses channel 2 for liquid)**.

### Water (base class, `Water.cs`)

- `int paintChannel`.
- Static caches, loaded once from `Resources`: `liquidSpheres` (non-gravity), `liquidSpheresGrav` (gravity), `liquidColors` (`Color[5]`, indexed by channel).
- `SetupSpheres()` is public static and called from `Awake`. Resource names: `SpraySphere_Poop`, `_Blood`, `_Burn`, `_Water`, `_Cleaner` (and `_Grav` variants, `SpraySphere_Water_Grav`, `SpraySphere_Cleaner_Grav`, etc.). Note the Grav array uses `SpraySphere_Burn` for index 2 (no `_Grav` suffix).

### WaterVolume (`WaterVolume.cs`, extends Water)

- `Volume` property, clamped to `[-0.5, 1.1]`. `>= 0` lerps the local position toward `filledOffset` and scales by `scaleFillCurve`, enabling the collider. Below 0 disables the collider.
- `OnTriggerEnter`: a `Soaker` is soaked with `paintChannel`; a `LiquidContainer` that is not filled reduces `Volume` by 0.05 (dipping).
- `SetLiquid(int channel = 3)`: sets colour and `paintChannel` for channels 0, 1, 3, 4 only.

### LiquidContainer (`LiquidContainer.cs`)

`ISaveableObj, ISaveable, Usable.IUsable`.

- Fields: `filled`, `breakable`, `waterVolume` (WaterVolume), `maxDrinkVolume`, `currentDrinkVolume`, `sipVolume`, `frozenContentsTimer`, `nearbyRefridgeratorCount`, `refillCooldown`, `liquidSphere`.
- **Filling:** `OnTriggerEnter` with an unfilled container, not on cooldown (`refillCooldown < 0`), upright, and a `Water` that is not an `IceCube` → `Fill(paintChannel)`. `Fill` sets the liquid, moves the volume up, sets `filled`, and sets `currentDrinkVolume = maxDrinkVolume`.
- **Spilling:** `Update` empties if the container tips (`position.y > (position + forward).y`). `Empty(Collision, bool noSplash)` spawns either an `IceCube` (if `frozenContentsTimer > 20`) or a gravity `SpraySphere`, plays the splash, empties the volume, and sets `refillCooldown = 0.3`.
- **Freezing:** while `nearbyRefridgeratorCount > 0`, `frozenContentsTimer` rises at 1/s to a cap of 30. Otherwise it decays at 0.2/s. `OnTriggerEnter/Exit` with a `RefrigerationZone` maintain the count and subscribe to `OnDespawn` on the fridge.
- **Drinking:** `Use()` → `Drink()` (`LiquidContainer.cs:184-213`). Each sip (`sipVolume`) calls `PlayerDrink(sipVolume * 0.8)` (hunger `+= v`, bladder `-= 2v`). Effects by channel: Dirt → `Vomit`; Blood → `Health5Instant`; Cleaner → `DeathDelay10`. Empties silently once below one sip.
- Save stores `filled` and `refillCooldown`.

### LiquidBomb (`LiquidBomb.cs`)

`Use()` starts `DelayedExplosion()` (unless already triggered). That coroutine: removes the grenade pin, waits `explosionDelay ± randomDelayOffset - 0.75`, lifts the object (30 impulse), then at `+0.75` s spawns `liquidExplosionParticle` tinted by `Water.liquidColors[painterBrush.splatChannel]`, fires 100 random rays of 30 m (painting each hit with `PaintTarget.PaintRaycast`), reduces `FireParticle.Strength`, and destroys itself. `OnBreak` also triggers it.

### SinkFaucet (`SinkFaucet.cs`)

- `Use()` → `Toggle()`. While `pouring`, `waterVolume.Volume += dt * fillSpeed`; otherwise it drains at `drainSpeed`.
- When `Volume >= 1` the sibling `Spawner` is enabled (`:100-103`). What the spawner spawns was not traced **(unverified)**.
- `Save` stores `pouring` and the volume. `Load` flips `pouring` and calls `Toggle(noToggleAudio: true)` (this looks inverted, see Open questions).
- Implements `ISaveableStatic`, `ISaveableObj`. Has `SaveID` property.

### Sprays and fluids

| Class | File | Behaviour |
|---|---|---|
| `SprayBottle` | `SprayBottle.cs` | `Use()` shoots a `SpraySphere` at 45 force along a ray down from the bottle. Non-continuous: one sphere per press and `PreventOtherUse`. Continuous: one sphere per 0.1 s. Paints the surface with `PaintTarget.PaintRaycast` using its `brush`. Implements `IStopUseTriggerable` and `IReleaseTriggerable`. |
| `SpraySphere` | `SpraySphere.cs` | Particle-like ball. Dies 0.7 s after spawn (`killTimer`), or after `overrideLifeTime`, or 0.06 s after hitting something (unless a lit `Flame` is inside). A lit Flame makes it rise and it dies at `killTimer < -5`. `DisablePainting` on ignition, destroys after 2-7 s. Freezes into `IceCube` on contact with a `RefrigerationZone` when `canFreeze`. |
| `FluidGun` | `FluidGun.cs` | Extends `Remove_Clothing`, implements IUsable. Dispenses urine (`clothId == 1`, bladder +18/s, stops at bladder >= 98) or vomit (`clothId == 2`, hunger -42/s, requires shirt removed, stops at hunger < 5). Spawns `fluidSphereGrav` every 0.08 s. Disgust above 50 triggers vomiting via `Disgust.OnChanged`. Uses `player_Abilities.clothings`, `masks`, and `noClothUVs`. |
| `FireExtinguisher` | `FireExtinguisher.cs` | `[RequireComponent(typeof(Usable), typeof(Rigidbody))]`. `Use()` sprays a foam ray 30 m, which reduces `FireParticle.Strength` and calls `Flame.Extinguish()`. On break (`OnBreak`) `Explode()`: fire clean-up in 2.75 m, `Commons.ExplosionDamage(radius 3, damage 40, EffectType.Impact)`, and self-destruct after 10 s. |
| `Soaker` | `Soaker.cs` | Lets an object absorb a liquid. `Soak(int channel)` swaps the material to `soakedPaintedMats[0..2]`, sets the splat channel, and for dirt (0) adds `HygieneDecreaser`. `OnCollisionStay` samples `PaintTarget.RayChannel` every 0.08 s and soaks after 0.12 s of contact. Food with `swapOnWaterObj` swaps. Interface `ISoakable.SoakUp(Soaker)` is used by `Butter`. |
| `Squeezer` | `Squeezer.cs` | Visual wobble (randomised scale oscillation). No game logic. |

---

## Bathroom

| Class | File | Behaviour |
|---|---|---|
| `Toilet_Flush` | `Toilet_Flush.cs` | `flush` is the lever object. When its local X rotation is above 350 and not already flushing, it plays a sound and destroys every `Usable` in `flushableItems` (entries are added on trigger enter, except objects named exactly `Toilet`). If 2 or more were flushed: 4+ calls `Overflow()`, and a second consecutive big flush also calls `Overflow()`. `Overflow()` enables the `Spawner` on the same GameObject. `Use()` is empty. The bowl's own torque keeps the lever returning. |
| `Toilet_Paper_Holder` | `Toilet_Paper_Holder.cs` | On trigger stay with a free `Toilet_Paper_Roll`, parent-constrains it at `hangOffset` and makes it kinematic. When a player holds the roll, it releases it. |
| `Toilet_Paper_Roll` | `Toilet_Paper_Roll.cs` | `[RequireComponent(typeof(Usable))]`. `Use()` → `TakeOne()`: spawns a `tpSheet` at the hand position (soaked to the roll's channel if it has a `Soaker`), puts it in the other hand, and decrements `sheetsCount`. The mesh steps through `smallerMeshes` as the count drops. |
| `Turd` | `Turd.cs` | Empty marker class. `Wearable` checks it on collision to become dirty. |

---

## Tools, weapons & gadgets

### Cutting system

Four cooperating classes.

- **`Cutter`** (`Cutter.cs`) is a damage source. Implements `IDamager`, with `OnDamageRegistered` and `OnDamageDealt` events.
  - `OnCollisionEnter`: if `relativeVelocity.magnitude > 1.8`, it cuts any `Sectionable` (`Sectionable.Cut()`), then calls `CutDamage`.
  - `CutDamage(Collider, Vector3)`: cuts any `Sectionable` and applies `Health.Affect(contact, -U(12,18), EffectType.Cut, null, lastTouchedByRef)` to any `ActorStats` (if `canDamageOther`).
- **`Knife`** (`Knife.cs`) has no `Cutter` of its own. `Use()` sets `isUsed` for 0.04 s, during which `FixedUpdate` applies look-driven sideways force to the blade. `ThrowTrigger` adds 35000 torque. Cutting damage is therefore not in Knife; it comes from a `Cutter` on the same object if present **(unverified which prefabs have Cutter)**.
- **`Scissors`** (`Scissors.cs`) `Use()` toggles `opened` (blade rotates to 30 degrees). On closing, it runs an `OverlapBox` in front (half-size 0.15, `Usable` and `Player` layers), cutting any `Sectionable` and calling `cutter.CutDamage` on each collider. Requires a `Cutter` on the same object (`Scissors.cs:20`). Throw applies -25000 torque.
- **`Sectionable`** (`Sectionable.cs`) is the splittable food/body part.
  - `Cut()`:
    - `justEventOnCut`: only fires `OnCut`.
    - `justDeleteOnCut`: destroys.
    - Otherwise, if `cantSplitAgain` is set it returns. It then spawns `nextSegmentedObj` (one piece, or two pieces when `foodSegment` is set), or `foodSegment` pieces alone. Cooked state propagates to pieces. Broken parents burn their pieces. `OnCut`, the cut sound, and destruction follow.
  - `cantSplitAgain` is set on `Start` and cleared 0.5 s later (`CantSplitDelay`).
  - `CuttingResistance : Stat` (`CuttingResistance.cs`) defaults to 1. Not referenced by any class in my area **(unverified usage)**.

### Fire starters

| Class | File | Behaviour |
|---|---|---|
| `Lighter_BBQ` | `Lighter_BBQ.cs` | `[RequireComponent(typeof(Flame))]`. `Use()` toggles the `Flame`. Ignition enables the collider painter; extinction disables it. On break it spawns `firecracker` and destroys itself. Saves `isOn`. |
| `Cigarette` | `Cigarette.cs` | Implements `MaskFace.MaskEffectable`. Burns down via `depletionRate` on local Y while lit (extinguishes at 0.24). `InhaleNoise` triples depletion. `MaskConstantEffect` adds hunger and energy per frame, scaled by `PlayerStats` constants (sign and values not verified). `OnBreak` extinguishes. |
| `Candle_Small` | `Candle_Small.cs` | Not a Usable. Burns down its child by z-scale (rate `0.002 * random(0.75..1)` per second); extinguishes at scale 0.12 unless `infiniteBurning`. Extinguished by impulse above 200 or a `SpraySphere_Wind(Clone)`. |
| `Sparker` | `Sparker.cs` | `Use()`: 20% chance to shock and slip the holder (25 or 75 Burn damage depending on wetness > 50), then `SparkUp(null)`. `SparkUp(other, pos)` sparks only when `other == null`, water, or metal `Offering`, subject to 0.15-0.3 s cooldown. 6% chance per spark to start a room fire (`Locator.StartRoomFire`). Implements `IElectronic` (`IsPowered` also sets `isOn`). |

### Guns

- **`Gun`** (`Gun.cs`): `Use()` shoots if `fireRateTimer <= 0`. `Shoot()` raycasts 50 m down the barrel (`-up`), applies `-51` Cut Health damage to an `ActorStats`, triggers `BreakOnImpact.Impact` (force 30-120), decrements `mag.bulletCount`, paints a smoke hole, and flashes a light. Empty mag: ejects it and clicks. `fullAuto` controls whether `PreventOtherUse` runs. `InsertMag(GunMagazine)` parents the mag and hides it. Saves the magazine's `saveId` (`Gun.cs:54`). `Load` uses `SaveLoadHandler.loadedUsablesById`, so the magazine must be loaded first **(unverified ordering)**.
- **`GunMagazine`** (`GunMagazine.cs`): `bulletCount`. `Use()` inserts itself into the other-hand `Gun` if the gun has no mag, and drops from the hand.

### Other tools

| Class | File | Behaviour |
|---|---|---|
| `VacuumCleaner` | `VacuumCleaner.cs` | Implements `IBatteryPowered`. Powered by `Fuse` (drains `1 * dt` per second while on). `OnTriggerEnter` absorbs a `DustBall`, adding its `Level` to `containedDust` (cap 80). `PunchTrigger` empties it into dust balls (sizes 7, 4 and 1) when off, or stops it. Punch also removes a `Fuse` when off. `Use()` toggles via `SetUse`. |
| `Dumbbell` | `Dumbbell.cs` | `PunchTrigger` → `playerStats.Exercise()`. |
| `Brush` | `Brush.cs` | Data class, not a component. Paint brush parameters (`splatTexture`, `splatsX/Y`, `splatScale`, `splatChannel` 0=Dirt, 1=Blood, 2=Burn, 3=Water). Used by sprays, guns, and LiquidBomb. |
| `Music_Instrument` | `Music_Instrument.cs` | `Use()` plays the audio. Pitch follows the camera's up/down angle while playing. `StopUseTrigger` stops it. `Player_Abilities.cs:699-732` calls `Music_Instrument.Use()` directly, not through IUsable. |
| `HangNoose` | `HangNoose.cs` | Not a Usable. On trigger with a `BrainNeetAI`, `HangBody` makes the body kinematic and parent-constrains it at `offset`. The body's `Usable.OnPickup` triggers `Unhang`. Saves `heldBrainId`. |
| `BuildDoorKit` | `BuildDoorKit.cs` | Tracks nearby `BuildDoorReceiver`s by trigger. `Use()` activates the `Door` child of the first receiver's parent, sets its rotation to -90 X, zeroes `BreakOnImpact.breakCollisionAmounts`, removes the receiver and destroys itself. `BuildDoorReceiver.cs` is an empty marker class. |
| `Key` | `Key.cs` | `doorId`. `Use()` checks that the other hand holds an object named exactly `Door` whose `Door.doorId` matches. Sets `Door.Locked = false`, plays `AudioSource[4]`, and destroys itself after `DropHand`. Relies on the GameObject name `Door`. |
| `Piggybank` | `Piggybank.cs` | Stores cash (`CashValue`, random 5-15 on Start, pushed to `usable.price`). Accepts coins from the other hand (`Offering.isMoney`) by `Use()` or trigger. On break, `SpawnMoneyValue` splits the value into `PickupBank.pickupsDict` items `MoneyStack10000` ... `MoneyStack100`, `Coin`, `CoinSilver`, `CoinBronze`. Those keys must exist. |
| `Cardboard_Box` | `Cardboard_Box.cs` | `IUsable`, dropable/pickupable triggers. Holds one `content` prefab. `Use()` opens the box and spawns the content (`:175-206`, tape handling in `BreakTape` not traced). `FreezeBoxContents` records overlapping items. `GetBoxContentValue`/`SellBoxContent` sum prices of frozen contents; a black-market or occult item can trigger a cop van and a 10% fine (`:249-256`). `Save` stores `content.GetComponent<Usable>().prefabName` (or `Brain.prefabName`). `Load` looks up `PickupBank.pickupsDict` then `BrainBank.brainsDict`. |
| `RecallObjectButton` | `RecallObjectButton.cs` | `Use()` teleports `objs` to `spawnPosition` plus a random sphere, removes FixedJoints, clears `isTrigger` on colliders, plays audio. |
| `DynamicBoneUsable` | `DynamicBoneUsable.cs` | On drop, zeroes the `DynamicBone` force. |
| `Loot` | `Loot.cs` | Starts scaled to zero and kinematic, then scales in. `PickupTrigger` → `Unloot` (restores scale and gravity, removes itself). `unlootable` auto-unloots after `unlootTimer`. |
| `PrefabContainer` | `PrefabContainer.cs` | Dispenser. Implements `IUsable`. `Use()` → `Take()` (only when enabled) → `TakeOne()`: picks `containedObjs[nextObjId]` (or random if `randomObj`), spawns at the hand (if held and no `spawnPosition`) or 0.5 m above, inserts into the other hand, decrements `objCount` (`-1` means infinite), fires `OnEmpty` at 0. `OnSpawn` fires per take. `OnBreak` calls `Empty()`. **Side effect:** the first take sets `GetComponent<Usable>().price = 0`. Save stores `objCount` and `initObjCount`. |
| `Pickup_UI_Renderer` | `Pickup_UI_Renderer.cs` | Declares `cachedPickups`, `tmpTex`, `cam`. No logic in the decompiled code. |
| `Usable_Hook` | `Usable_Hook.cs` | Stub with a `used` bool. No logic. |
| `Symbol` | `Symbol.cs` | Visual: `SetAlpha`, `SetScale` on scaled sub-objects, slow spin. Callers are outside my area. |
| `Remove_Clothing` | `Remove_Clothing.cs` | `[RequireComponent(typeof(Usable))]`. `clothId`: 0 shoes, 1 pants, 2 shirt. `Use()` calls `player_Abilities.RemoveClothing(id)`. For pants, shoes come off first if worn. Base for `FluidGun`. |
| `ClothRemover` | `ClothRemover.cs` | `Use()` strips the outermost garment (shoes, pants, then shirt) with `noHandInsert`. Its `Start` reads `GetComponent<Player_Abilities>()` and `GetComponent<Usable>()`, so it must sit on an object with both **(unverified where it is attached)**. |

---

## Wearables & occult

| Class | File | Behaviour |
|---|---|---|
| `Wearable` | `Wearable.cs` | `[RequireComponent(typeof(Usable))]`. `pieceId` (0 shoes, 1 pants, 2 shirt). `Use()` → `Wear(player_Abilities)`: `PutOnClothes`, hides the mesh, moves to `y = -500`, makes kinematic, drops from hand. Contact with a `Turd` → `MakeDirty()` (sticky, `Materials/Shitty`, adds a CollisionPainter). Water → `Clean()`. |
| `Wig` | `Wig.cs` | `IPickupTriggerable`, `[RequireComponent(typeof(Usable))]`. First pickup only: sets kinematic off, optionally un-sticks, sets `BrainNeetAI.dontLoadWig`, optionally "gibs" the player (`Obliterate`), sets `dontSave = false`, calls `SubSaveUsable()`, then destroys its own component. `Start` destroys the component if `dontSave` and the parent is not a `BrainNeetAI`. |
| `MaskFace` | `MaskFace.cs` | `[RequireComponent(typeof(Usable))]`. `PutOnMask(player)` (`MaskFace.cs:58-90`) refuses if `canOnlyWearInScene` mismatches, or if the item is not stackable and the player already wears non-stackable masks. Otherwise parent-constrains to the camera at offset/rotation, sets layer `NotPlayer`, adds itself to `Player_Abilities.masks`, and records `maskInitRot`. `RemoveMask` reverses it. `Use()` → `PutOnMask`. `unremoveable` and `MaskEffectable` are declared; enforcement by Player_Abilities was not traced **(unverified)**. |
| `Candy_Bucket` | `Candy_Bucket.cs` | Orbiting bucket for a character (`CharacterStories`). Offerings entering the trigger (not worn on a head mask) call `dialogueManager.ProcessOffering` after a short delay. `ActivateBucket` / `DeactivateBucket` animate it around the owner. |
| `Offering` | `Offering.cs` | Tag component: `isMeat`, `isVeggie`, `isSweet`, `isDrug`, `isShit`, `isCheese`, `isWet`, `isBlood`, `isMoney`, `isMetal`, `isWeapon`, plus `_isBurnt`. `IsBurnt = true` clears meat, veggie and drug. `OfferingStatsToInkList` builds the Ink `OfferingStates` list (`cookedmeat` or `rawmeat` depending on `Food.IsCooked`, `burnt`, and others). Used by Dialogue. |
| `DemonicContraption` | `DemonicContraption.cs` | `Use()` toggles `activated` and rotates tips. When active, every 30 physics steps it casts 4 rays for blood-paint score (`PaintTarget.scores.y`). If the room light is off (or unpowered) and the total exceeds 10, `sacrificePossible` is set and the light flickers. A dead `ActorStats` entering the trigger is consumed over 6.66 s (moved, rotated, destroyed), toggling two `Spawner`s on the object. What those spawn was not traced **(unverified)**. |
| `DemonCore`, `DemonCoreCrystal` | `DemonCore.cs`, `DemonCoreCrystal.cs` | Empty `Start` and `Update`. No behaviour in the decompiled code. |
| `Loot` | `Loot.cs` | See above. |
| `CatFoodBowl` | `CatFoodBowl.cs` | `foodUnits` 0..5. A `Gib` entering the trigger while unheld increments it. `Use()` pulls a gib out and into the hand. `SetHeight` animates the child's Z to `-1.8 + units * 0.392`. |
| `DustBall` | `DustBall.cs` | `Level` (1-2 on Start, scale `0.1 + (level-1)*0.07`). `Use()` and collisions merge with another DustBall when both have `Level < 8` (the merged level can exceed 8). Used by VacuumCleaner. |

---

## Modding notes: making a new item

These steps need no Unity editor. They assume the mod has access to the game's assemblies and the `PickupBank` / `Usable` types.

### 1. Understand the registry

- `PickupBank.pickupsDict` is a `static Dictionary<string, Spawnable>` (`PickupBank.cs:19`). Key = `prefab.obj.name` at the time `PickupBank.Init()` ran (`PickupBank.cs:30-41`). Value = `Spawnable` struct (`Spawner.cs`, primary constructor `Spawnable(GameObject)`, fields `obj`, `locked`).
- `PickupBank.Init()` only populates when the dictionary is empty. It is called by `HouseManager.cs:359`. Adding entries later is therefore not done by Init; write directly with `pickupsDict[key] = new Spawnable(obj)`.
- `Dictionary.Add` in `Init` throws on duplicate names. Two prefabs with the same GameObject name break startup.
- `PickupBank` also has a serialized list `pickups` (`PickupBank.cs:21`), one per store type. The store UI iterates it (`StoreGenerator.cs:42`, `DreamRandomizer.cs:105`, `CustomItemShop.cs:77`). To make an item appear in a shop, add its `Spawnable` to the appropriate `pickupBank.pickups` list and set `purchaseableAt`.

### 2. How `prefabName` is set

`Usable.prefabName` is a serialized public string (`Usable.cs:175`) that no code assigns. The Unity editor sets it per prefab, to the same string as the prefab's name. Consequences:

- `Save()` skips objects where it is empty (`Usable.cs:239`).
- `SaveLoadHandler.LoadUsables` instantiates from `PickupBank.pickupsDict[prefabName].obj` (`SaveLoadHandler.cs:232`). An empty or unregistered key throws.
- `Cardboard_Box` and `Brain` store the same string to re-create contents (`Cardboard_Box.cs:90`).

**For a mod:** set `usable.prefabName = key` yourself, and register the same key in `pickupsDict`. Use the same string for the GameObject name, the dictionary key, and `prefabName`. Do this before any save occurs.

### 3. Recipe: clone an existing item as a template

```csharp
// Sketch, not compiled. Types and members as decompiled.
// 1. Pick a source prefab that already has a working save path.
Spawnable src = PickupBank.pickupsDict["Butter"]; // any simple item
GameObject template = Object.Instantiate(src.obj);   // asset instance, not in scene
template.name = "MyItem";                            // becomes the key
Object.DontDestroyOnLoad(template);
template.SetActive(false);                           // keep inert until spawned

// 2. Tune the Usable.
Usable u = template.GetComponent<Usable>();
u.prefabName = "MyItem";
u.price = 25f;
u.purchaseableAt = Usable.StoreType.Normal;
u.description = "A custom item.";
// u.storePicture = ...; // Sprite, optional

// 3. Add behaviour. A custom IUsable component must be added before SetActive(true).
template.AddComponent<MyItemBehaviour>(); // implements Usable.IUsable

// 4. Register.
PickupBank.pickupsDict["MyItem"] = new Spawnable(template);

// 5. Spawn later.
GameObject clone = Object.Instantiate(PickupBank.pickupsDict["MyItem"].obj, pos, Quaternion.identity);
clone.SetActive(true);   // Usable.Start only runs when active
```

Notes on the recipe:

- Clone the Usable's **sibling components** too. `Object.Instantiate` copies all components on the template, so the cloned `Usable` already has the right Rigidbody, colliders, layer and audio.
- Do not reuse a source's `IUsable` components unless you want their behaviour. Remove them with `Object.Destroy(component)` in the template before registering, if needed.
- `template.name` must not change after registration (the key is captured at registration).
- To make a clone of a source item behave differently, change properties on the clone's `Usable` before `SetActive(true)`.

### 4. Custom behaviour

- **Use:** implement `Usable.IUsable.Use()`. It runs every frame while the Use button is held (see Overview), so guard with `PreventOtherUse()`, timers, or a state flag.
- **Pickup / drop / throw / punch / release / stop-use hooks:** implement the nested interfaces on `Player_Abilities`: `IDropTriggerable` (`DropTrigger`), `IThrowTriggerable` (`ThrowTrigger`), `IPickupTriggerable` (`PickupTrigger(GameObject picker)`), `IReleaseTriggerable`, `IStopUseTriggerable`, `IPunchTriggerable` (`Player_Abilities.cs:76-101`). The methods are called via `GetComponents`, so any number may be present.
- **Save state:** implement `ISaveableObj` (`Save()` returning a `SaveData` subclass with `type` set to `GetType().ToString()`, and `Load(JToken)`). The matching sub-save is loaded only when `item["type"] == GetType().ToString()` (`SaveLoadHandler.cs:263-275`). Use a `[Serializable]` class for the record, like the existing ones.
- **Save:** a custom Usable with extra state must also keep `dontSave` false. `Usable.Save()` returns null for `dontSave` items.

### 5. Survive save and load

Checklist for an item to round-trip through the save file:

1. `prefabName` set and equal to a `pickupsDict` key.
2. `Rigidbody` present (`Usable.Save` reads `velocity`).
3. `saveId` assigned: `GenerateSaveId()` is called from `SubSaveUsable()` in `Usable.Start`.
4. `Start()` runs on the loaded object. `SaveLoadHandler` calls `Usable.Start()` itself, so any subclass `Start` must cope with being called before the scene is ready.
5. Any state the item needs on load comes from `ISaveableObj.Load`, which runs after `Start`.
6. Do not rely on static state to carry per-item data.

### 6. Harmony patch targets

| Target | Why |
|---|---|
| `Player_Abilities.Use(Hands)` (`Player_Abilities.cs:1317`) | Single place where IUsable.Use runs for held items. A prefix can filter by object. |
| `Player_Abilities.Pickup(Hands, GameObject)` (`:1120`) | Pickup gating (blacklist, whitelist, hand). |
| `Player_Abilities.PickupUseOnly()` (`:1095`) | Non-held use path. |
| `Player_Abilities.PlayerEat(float)` (`:1521`), `PlayerDrink(float)` (`:1539`) | All consumption stat changes. |
| `Food.GetEaten(Player_Abilities)` (`Food.cs:520`) | Per-food eating. |
| `Food.Use()` (`Food.cs:367`) | Eat / stack entry point. |
| `Usable.Damage(float, EffectType)` (`Usable.cs:631`) and `Usable.DamageRaw` (`:649`) | Damage and break. |
| `Usable.Burn()` (`:663`) | Burn event. |
| `Usable.Start()` (`:276`) | Setup. Called manually on load, so a postfix will run on load as well as spawn. |
| `SaveLoadHandler` load for usables (`SaveLoadHandler.cs:216`) | Modify records before instantiation. |
| `PickupBank.Init()` (`PickupBank.cs:30`) | Add registrations at startup. |
| `Flame.Damage(Usable)` (`Flame.cs:416`) | Heat model. |
| `LiquidContainer.Drink()` (`:184`) | Liquid effects. |

### 7. Gotchas

- **`alreadyStarted` guard.** `Usable.Start` returns early once `alreadyStarted` is true, and sets it at the very end. Anything that depends on `Start` must be idempotent. Subclass `Start` methods are not guarded. Several item classes cache `GetComponent<Usable>()` in their own `Start`, so calling their code before Start throws NullReferenceException. `Food.Use` guards this by calling `Start()` when `usable == null`.
- **Manual `Start()` on load.** `SaveLoadHandler` calls `Usable.Start()` (`:255`), `PrefabContainer.Load` calls `Start()` (`:61`), `Cardboard_Box.Load` calls `Start()` (`:127`), `Lighter_BBQ.Load` calls `Start()` (`:64`), `Toilet_Paper_Roll.Load` calls `Start()` (`:38`). A `Start` that spawns or subscribes will double up.
- **Static state.** `PickupBank.pickupsDict` persists across scenes (static). `Water.liquidSpheres`, `liquidSpheresGrav`, `liquidColors` are cached on first use and never reset. Anything you register survives scene loads until the process ends.
- **`Usable.Save()` nulls.** Items with `button` or `lever` set, `dontSave`, or an empty `prefabName` are silently not saved. An item that disappears on reload usually has one of these set.
- **`SetActive(false)` templates.** Instantiating an inactive template gives an inactive clone. `Usable.Start` does not run until the clone is activated.
- **`DamageRaw` triggers `Burn()`.** Loading a broken item re-runs burn visuals and `OnBreak` handlers.
- **`OnBreak` is not idempotent.** Several classes append a delegate on every `Start`. Restarting an item adds duplicates.
- **`readyToEatSteps` decrement.** `CerealBox.Use` and `Seafood_Delight.Use` decrement it (it becomes negative). `GetEaten` refuses only when it is positive, and nothing increments it in the decompiled code. Effectively a no-op guard.
- **Copy-paste bugs (apparent).** In `Food.Start` (`:250-251`, `:262-263`) and in the `Sectionable` / `Food.Stack` delegates, the second assignment overwrites the first (`rawStatusEffects` is assigned `cookedStatusEffects` in the piece). In `Sectionable.Cut` (`:97-99`) `OnCutNextSegmented` is called under an `OnCutNextSegment` check. In `Sectionable.Cut` (`:118-121`), `GetComponent<Usable>().broken` is dereferenced without a null check. Verify before relying on these.
- **`Usable.Heat` is unused.** Do not key a mod on it.
- **`Usable.OnUse` / `useEvent` only fire on buttons and world things.** A held item will not fire them through the normal Use path.
- **Sprays and liquids use channel numbers, not enums.** `paintChannel` must be 0, 1, 3 or 4 for `WaterVolume.SetLiquid`. `Food.SwapWithWateredObj` and `Soaker.Soak` log and ignore other values.

---

## Open questions

1. **Use frequency.** Is `intents.Use.Active` a level (held) or a one-frame edge for the purposes of `ExecuteAction(Use)` at `Player_Abilities.cs:633`? I assumed held. The full `cancelledClick` gating path was not traced. This decides whether one-shot items (Food eat, Burn, PrefabContainer take) are safe without guards.
2. **`prefabName` source.** It is assumed to be set by the Unity editor (serialized default). Confirm in the prefab assets, which are not in the decompiled code.
3. **`Food` nutrition values** and status effect definitions live in data (prefabs, `HouseManager.statusEffectDict`). Not in the decompiled code.
4. **`SinkFaucet.Load`** calls `pouring = !saved.pouring` and then `Toggle`, which looks inverted. Confirm.
5. **`ClothRemover`** attach point: `Start` reads `GetComponent<Player_Abilities>()` and `GetComponent<Usable>()` on the same GameObject.
6. **`Sectionable` / `Food.Start` delegate bugs** listed under Gotchas. Confirm against behaviour before patching.
7. **`Microwave` rate.** `CookedMeter += 0.04` per `OnTriggerStay`. The effective per-second rate depends on the physics timestep, which I did not check.
8. **`Spawner` contents** on `SinkFaucet`, `Toilet_Flush` and `DemonicContraption` are not in my area.
9. **`Cigarette` / `MaskFace` `MaskConstantEffect`** sign and magnitude depend on `PlayerStats` constants not read here. Also who calls `MaskConstantEffect` (presumably Player_Abilities).
10. **`MaskFace.unremoveable`** is declared but its enforcement was not traced.
11. **`Gun.Load` ordering** relative to the magazine's load.
12. **`DemonicContraption` spawners** (`GetComponents<Spawner>()[0]` and `[1]`): what they spawn.
13. **`Pickup` button branch** resets: in the button branch `pickups[hand]` is set but not cleared in the code I read (`Player_Abilities.cs:1190-1210`). Whether it is cleared later was not traced.
