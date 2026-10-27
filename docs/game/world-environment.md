# World & environment

Scope: the house simulation as implemented in `decompiled/Assembly-CSharp/`. Covers rooms, doors, the fuse-based power grid, fire and heat, the (absent) noise system, breakage and contact effects, and assorted decorative world scripts. Line numbers refer to files under `decompiled/Assembly-CSharp/`. Where a claim is inferred rather than read directly, it is marked as such.

## Overview

**Rooms.** `Location` (`Location.cs`) is a flat enum of 23 values, including `Kitchen`, `Livingroom`, `Attic`, `Basement`, `Backyard`, `FrontYard`, `Shed`, `Basement_Backroom`, `Dream`, and `Other`. Each room has a `Locator` component (`Locator.cs`). `Locator` registers itself in the static dictionary `Locator.rooms` (`Locator.cs:8`, `:39-41`). A room holds its own runtime lists: `electronics`, `usables`, `windows`, and a `fireSpreader` GameObject reference (`Locator.cs:12-26`).

**Electronics list.** `Locator.electronics` is filled at editor time by the `[ContextMenu] FindElectronics` method (`Locator.cs:188-202`). It is not rebuilt at runtime, so an `IElectronic` object spawned or moved into a room later is not on the power grid.

**Power grid.** A physical fuse (`Fuse`) is seated in a `FuseBox_Slot`. A `FuseBox_Button` on the same slot toggles that room's breaker. A room is powered when its slot holds a fuse with charge and its button is on. Power is pushed to every `IElectronic` in the room's `electronics` list (`FuseBox_Slot.cs:114-140`, `FuseBox_Manager.cs:57-66`). The grid is binary per room. The charge on a fuse only matters for the on/off check.

**Fire and heat.** Fire is a per-room `FireSpreader` object made of `FireSpreaderSphere`s, which emit `FireParticle`s onto surfaces. Individual objects carry a `Flame` component and a `HeatHolder` for cooking and heat transfer. Details are in the Fire & heat section.

**Noise.** There is no gameplay noise or hearing system in this assembly. See the Noise section.

**Breakage.** Objects break through `Usable.Damage` (burn/cut accumulation) and through `BreakOnImpact` (collision impulse thresholds). `Gib` is an empty marker script; gib objects are spawned as `BreakOnImpact.breakEffectSpawn`. See Physics & contact effects.

**Saving.** Static world objects (doors, light switches, gas buttons, smoke detectors, and others) implement `ISaveableStatic`. They are listed in `HouseManager` and matched on load by `saveId`. Dynamic objects (fires) save through `SaveLoadHandler.SubscribeSave` (`SaveLoadHandler.cs:61`). Details are in Modding notes.

---

## Doors

### Door (`Door.cs`, 444 lines)

- **Class.** `Door : MonoBehaviour, Player_Abilities.IDropTriggerable, ISaveableStatic, ISaveableObj, ISaveable` (`Door.cs:7`).
- **Locking.** `Locked` is a property (`Door.cs:117-135`). Its setter stores the flag and raises `OnUnlocked` or `OnLocked`. Setting `Locked` from code therefore fires the events. The setter does not change `closed` or kinematic state. `Start()` sets `rigid.isKinematic = true` only when `startLocked` is true (`Door.cs:188-191`).
- **Events.** `OnUnlocked` and `OnLocked` are raised from the `Locked` setter. `OnOpened` and `OnClosed` are declared (`Door.cs:153-155`) but nothing in the assembly invokes them. Treat them as dead.
- **Keys.** `Key.cs` holds a `doorId` string. A `Key` unlocks a door only when the player holds the key in one hand and a GameObject named `"Door"` with a matching `doorId` in the other hand (`Key.cs:17-24`). Unlocking sets `Door.Locked = false` (`Key.cs:29`), plays audio source 4, then drops the key and destroys it.
- **Open/close.** `FixedUpdate` (`Door.cs:236`) drives the state.
  - `CheckOpen` compares the euler angle on `checkAxis` with `openTresholdAngle` (`Door.cs:201-224`). If the threshold is crossed while unlocked and unbroken, the door sets `closed = false` and resets `autoCloseTimer`.
  - When the angle falls back below the threshold, the door sets `closed = true`, zeroes angular velocity, and forces `localRotation = Euler(-90, 0, 0)` (`Door.cs:253-263`). This snap is hard-coded and assumes the closed pose is -90 on X.
  - `preventFrozenShut` suppresses the snap (used during Slam).
- **Auto-close.** While the door is open and `noAutoClose` is false, `autoCloseTimer` counts down by `fixedDeltaTime`. When it reaches zero, the door applies a small torque of `0.19` on Y (`Door.cs:265-280`). This branch does not check `locked`, so a door locked while open still auto-closes. `autoCloseTimerMax` is randomised to 25-160 when left at 0 (`Door.cs:184-187`).
- **Player control.** When `usable.player` is set, the door freezes the camera (`mainCam.isLocked`) and applies torque from the player's look input, scaled by `Feedback_Sender.mouseSensitivity * 50` (`Door.cs:281-325`). If the door is locked, it plays `lockedSoundIndex` instead, on a cooldown. `Player_Movement.blockXMouseLock` is set during the drag and cleared in `DropTrigger` (`Door.cs:397-407`).
- **Sticking.** If `stickOpen != 0` and no player holds the door, the door freezes kinematic at `stickOpen ± 3` once its angle passes `stickOpen` (`Door.cs:369-395`).
- **Slam.** `Slam(bool)` (`Door.cs:226`) applies a torque of `150000000 * sign(reverseInput)` on the rotation axis for three fixed updates. The `slamClosed` parameter is unused. `BrainNeetAI.cs:510-514` uses it: it returns failure if the door is locked, otherwise starts `Slam()` as a coroutine.
- **Breaking.** `FixedUpdate` does nothing when `usable.broken` is true. Doors normally break through a `BreakOnImpact` component, so `Start()` destroys that component when `unbreakable` is set (`Door.cs:180-183`). The cop AI breaks doors by calling `BreakOnImpact.Impact` with a 10000 force (`BrainAgentCop.cs:196`).
- **Keyhole cameras.** When the player enters the trigger, the door activates one of two render-texture cameras (`keyHoleRTCams[0/1]`) depending on which side the player is on (`Door.cs:409-443`).
- **Saving.** `Save()` writes `SaveDataDoor` (`Door.cs:157-160`): `saveId`, `isClosed` (the `closed` field), `isLocked`, the local rotation as a 4-float array, `activeInHierarchy`, and `unbreakable`. `Load()` (`Door.cs:162-171`) calls `Start()`, restores `closed`, `Locked`, the rotation, and active state. **Bug:** `unbreakable = unbreakable;` on line 170 is a self-assignment, so the saved `unbreakable` flag is never restored.
- **Ownership.** Doors are listed in `HouseManager.doors` (`HouseManager.cs:128`). `LoadStatic<Door, ...>` matches them by `SaveID` (`SaveLoadHandler.cs:166`).

### DoorTalking (`DoorTalking.cs`, 14 lines)

Subscribes `StopSound` to `Door.OnUnlocked` in `Start()` (`DoorTalking.cs:7`). `StopSound` stops `GetComponents<AudioSource>()[5]`. This is a hard-coded index, so the audio source order on the prefab matters.

### Frontyard (`Frontyard.cs`, 35 lines)

A trigger on the front yard spider scene. When a `BrainPlayer` enters, it sets the spider `Animator` bool `"Unburrowed"` to true and takes over `Player_Movement.externalSpeedMult` (`Frontyard.cs:22`). The multiplier is `max(1 - (-x - 3.5) / 6, 0)`, using `transform.position.x`. It is 1.0 at x = -3.5, reaches 0 by x = -9.5, and exceeds 1 for x > -3.5. On exit, the multiplier resets to 1 and the spider goes back under. The spider script itself (`Spider_Frontyard.cs`) is outside this area.

### Doorbell (`Doorbell.cs`, 16 lines)

`Doorbell.Awake()` sets `HouseManager._.doorbell = this` (`Doorbell.cs:14`). `Use()` plays `doorBellAudio` (`Doorbell.cs:7`). The trick-or-treater AI calls `HouseManager._.doorbell.Use()` (`BrainTrickOrTreater.cs:120`). There is no knocking mechanic in this assembly. Searches for "knock" only hit the Dialogue knockout functions.

---

## Fire & heat

### Quick reference

- **Ignite or extinguish one flame.** `Flame.Ignite(bool force = false)` (`Flame.cs:147`) and `Flame.Extinguish()` (`Flame.cs:183`).
- **Start a room fire.** `Locator.StartRoomFire(Location loc, Vector3 origin, bool byUsableCol = false, bool noStarterSphere = false)` (`Locator.cs:105`).
- **Stop a room fire.** `FireSpreader.Clear()` (`FireSpreader.cs:240`), reached through `Locator.GetRoom(loc).fireSpreader`.

### Flame (`Flame.cs`, 470 lines)

A `Flame` is a burnable/heat source. Key state:

- `lit`, `inert` (hot but does not ignite others; e.g. a frying pan), `sterile` (only ignites through its own `Ignite` calls), `preventIgnition`, `preventExtinguish`.
- `HeatLevel` (`Cold`, `Low`, `Medium`, `High`, `Lava`). Setting it clamps to `[0, maxHeatLevel]` and sets `lit = true` if the level is above Cold (`Flame.cs:94-108`).
- Events `OnIgnited` and `OnExtinguished` (`Flame.cs:67-69`).
- Child particle systems `flameParticle` and `smokeParticle`, and `Light[] lights`.

Behaviour:

- `Ignite` returns early if `(sterile && !force) || lit || preventIgnition` (`Flame.cs:149`). Otherwise it plays particles, enables lights, sets `lit`, and sets `HeatLevel = maxHeatLevel` (`Flame.cs:147-181`).
- `Extinguish` returns early if `!lit || preventExtinguish`. It stops particles, plays smoke, disables lights, sets `HeatLevel = Cold`, and if `deleteFlameOnExtinguish` is set, destroys the component and its `CollisionPainter` (`Flame.cs:183-243`).
- `Toggle(bool, force)` maps to `Ignite` or `Extinguish` (`Flame.cs:245-255`). `LaserPointer` uses this on its dot flame (`LaserPointer.cs:28`).
- `FlameTriggerZone(Collider)` (`Flame.cs:348-379`) is the proximity logic. A lit flame in a trigger zone ignites any non-sterile, non-lit flame there, unless it is inert and below High. A `Water` collider with `paintChannel != 4` extinguishes it. A `Flame` in the zone that is lit and not inert ignites this flame when this flame is not sterile. `ActorStats` in the zone takes burn damage of `-0.03..-0.05` (`Flame.cs:369`) if `canDamageOther` is true. `Usable` objects go through `Damage(Usable)`.
- `ParticleHandle` (`Flame.cs:453-469`) extinguishes on water particles, particles from a `FireExtinguisher`, or water. Paint channel 4 particles spawn a `SpraySphere_Cleaner` instead.

**Spreading to objects.**

- `MakeFlame(GameObject, point)` (`Flame.cs:265-293`) acts only if this flame is lit and the target has no `Flame` child.
  - If the target has a `Usable`, `AddBurnCollider` attaches a `CollisionPainter`, a child `Flame` that will delete itself, a `FireParticle_Small`, and a random impulse (`Flame.cs:295-322`). All of these are destroyed after `max(60 - 5 * flammability, 2)` seconds.
  - Otherwise the target must be on the `Furniture` or `Construction` layer. If the point is more than 0.7 m from the last custom sphere position, the method instantiates a `FireParticle` and calls `StartRoomFire` in the room of the usable (or `Locator.LocatePoint` if `Location.Other`) (`Flame.cs:282-291`).
- `Damage(Usable)` (`Flame.cs:416-429`) cooks food when `timeToCook` is set. Otherwise it damages the usable by `deltaTime * HeatLevel * 0.3` with `EffectType.Burn`. If the usable's damage reaches 1 or more, the flame ignites it.
- `canDamageOther(Usable)` requires `flammability != Unaffected`, `!broken`, `lit`, and `HeatLevel > Cold` (`Flame.cs:399-406`).

### Usable burn rules (`Usable.cs`)

- `Flammability` is an enum with values `Instant=100`, `Quick=10`, `Average=5`, `Slow=2`, `Resistant=1`, `Unaffected=0` (`Flammability.cs`). `Usable.flammability` defaults to `Average` (`Usable.cs:144`).
- `Usable.Damage(val, effType)` (`Usable.cs:631-644`) multiplies `val` by `flammability` for `Burn` and by `Flammability.Resistant` (1) for any other effect. When cumulative damage reaches 4 the object is broken. A burn-break also calls `Burn()` (`Usable.cs:663`). The non-burn multiplier looks odd but is what the code does.

### HeatHolder (`HeatHolder.cs`, 192 lines)

`HeatHolder` requires `Flame` and `AudioSource` on the same object. It tracks `_heatIndex` in `[0, 100]` (`HeatHolder.cs:47-78`).

- The setter also drives `flame.HeatLevel`: below 20 is Cold, 20 to 40 is Low, 40 to 75 is Medium, 75 and above is High (`HeatHolder.cs:57-72`). Because the flame setter raises `lit` above Cold, a hot object can become lit without `Ignite` being called. `OnIgnited` is not fired in that case.
- Heat in from flames in contact, per second: Low 0.2, Medium 0.5, High 1.0, Lava 1.2 (`HeatHolder.cs:179-186`). Heat from another `HeatHolder` moves the index by `(other - self) / 2 * dt * 0.2` (`HeatHolder.cs:175`).
- Each frame, when a flame is in contact, `decayDelayTimer` is reset to 2 seconds (`HeatHolder.cs:190`). Otherwise the index decays at 1 per second once the timer runs out (`HeatHolder.cs:120-127`).
- `WaterContact` (`HeatHolder.cs:155-167`) makes steam. Above 10 it loses 200 per second when the source is `Water` or a `FireExtinguisher`. Above 30 with an enabled `WaterVolume` (or a forced small steam) it multiplies the index by 0.2.
- **Save bug.** The `SaveDataHeatHolder` constructor is `(type, heatIndex, decayDelayTimer)`, but `Save()` passes `(decayDelayTimer, _heatIndex)` (`HeatHolder.cs:100`). The saved `heatIndex` field therefore holds the decay timer. `Load()` then sets `HeatIndex` from that field (`HeatHolder.cs:107`), so heat is restored to the wrong value.

### Fire spread (`FireSpreader.cs`, `FireSpreaderSphere.cs`, `FireParticle.cs`)

Each room has at most one `FireSpreader` (a GameObject with a `FireSpreader` component), stored in `Locator.fireSpreader`.

**Starting.** `Locator.StartRoomFire` (`Locator.cs:105-123`):

- If the room has no fire, it instantiates the `FireSpreader` prefab at the room centre, sets `currentLocation`, calls `Init(origin)`, fires `room.OnFireStart`, and chains `room.OnFireStop` onto `OnClear`.
- If a fire already exists, it calls `SpawnFireSphere(origin, byUsableCol)` on it.

`FireSpreader.Init` (`FireSpreader.cs:108`) spawns a starter sphere unless `noStarterSphere` is set. It places and plays the room smoke particles, scaled to the room's `BoxCollider`, and starts the fire audio.

**Gotcha on `origin`.** `SpawnFireSphere` uses `origin - spreader.position` as a direction for its first raycast, and as a random direction only when `origin == Vector3.zero` (`FireSpreader.cs:201-223`). If you pass exactly the room centre, the direction is zero. Pass an offset point instead, for example `Locator.GetRoomCenter(loc) + Vector3.up * 0.3f`. `Sparker.cs:89-93` passes a point offset up from the spark.

**Sphere lifecycle** (`FireSpreader.cs:121-190`, `FireSpreaderSphere.cs:64-79`).

- Each sphere paints a burn mark every physics step (`FireSpreader.cs:130`, `PaintBurnMark` at `FireSpreader.cs:270`).
- Each sphere also has a per-step chance to emit a `FireParticle` onto a surface. The chance is `base * lifeTimeMultiplier`, where `base` is 0.003 with fewer than 8 spheres, 0.001 with 8 to 22, and 0.00016 with more than 22 (`FireSpreader.cs:131`). The raycast is 25 m against the `Construction` and `Location` layers, and never hits the `Location` layer itself. If the hit is closer than 2 m, only a 5% roll succeeds. Each emitted particle reduces `lifeTimeMultiplier` by 0.002 (`FireSpreader.cs:144`).
- A sphere grows at a rate that depends on sphere count, until its scale passes 0.1. After that, if the total is below 60 and `dupeTimer > dupeCount * 10`, it duplicates itself and increments `dupeCount` (`FireSpreader.cs:147-160`). When there are more than 35 spheres, each duplication also lowers `lifeTimeMultiplier` by `0.001 * count / 2`.
- Spheres die in two ways. A sphere with no particles after 0.04 s despawns (`FireSpreaderSphere.cs:75`). Otherwise, when its last particle is removed, `DespawnFireParticle` calls `DespawnFireSphere` (`FireSpreaderSphere.cs:102-111`). `DespawnFireSphere` (`FireSpreader.cs:254`) destroys the sphere after 10 to 15 s and calls `Clear()` when none are left.

**Ending.** `FireSpreader.Clear()` (`FireSpreader.cs:240-252`) kills every particle, clears `Locator.fireSpreader` for the room, destroys the spreader, and calls `OnClear`. The chain from `StartRoomFire` then fires `OnFireStop`, which stops smoke alarms. Natural burnout comes from particle lifetime, which is set on the `FireParticle` prefab's `ParticleSystem` (not inspected). `FireParticle.OnParticleSystemStopped` (`FireParticle.cs:66`) returns the particle to its sphere.

**Particle kill.** `FireParticle.Start()` sets `Strength` to `Random.Range(0.01f, 0.01f)`, which is always 0.01 (`FireParticle.cs:61`). It also subscribes `Kill` to its `Flame.OnExtinguished`. `Kill()` sets strength to -1 (`FireParticle.cs:78`), and a negative strength removes the particle through its sphere.

**Ways a fire starts.**

- A lit `Flame` with `canStartFires` gets `MakeFlame` on long collisions (`Flame.cs:117`).
- `GasButton.GasIgnite` starts a number of room fires equal to `round(gasScale / max * 5)`, placed randomly within 4 m (`GasButton.cs:183`), when the gas zone is large enough.
- `Sparker.SparkUp` has a 6% chance per spark to start a room fire (`Sparker.cs:85-89`). The sparker must be powered.
- Any direct `Locator.StartRoomFire` call, including dev-tool calls.

**Ways a fire is put out.**

- `Flame.Extinguish()` on a single flame, which kills its particle.
- Water in a flame trigger zone or `ParticleHandle`.
- `FireSpreader.Clear()` for the whole room, the cleanest dev-tool option.
- `HeatHolder.WaterContact` reduces heat on objects but does not extinguish room fires directly.

**Saving.** `FireSpreader` is `ISaveableObj` and registers `Save` with `SubscribeSave` (`FireSpreader.cs:100-106`). It saves `currentLocation`, and every sphere's position and scale with its particle positions. On load, `SaveLoadHandler.LoadFireSpreaders` (`SaveLoadHandler.cs:469`) clears every existing room fire, then recreates each saved fire with `StartRoomFire(..., noStarterSphere: true)` and calls `Load`. `FireParticle.Load` throws `NotImplementedException` (`FireParticle.cs:56`), but nothing calls it: `FireSpreaderSphere.Load` only reads positions.

**Other fire-related classes.**

- `GasButton` is a gas-stove style knob (inference from its gas zone and ignite logic). Its flame has `preventIgnition` set whenever gas is off. Turning it on sets `preventIgnition = false` and starts a gas zone that grows until the flame ignites (`GasButton.cs:117-190`).
- `SmokeDetector` is covered under Electricity & lights, but its alarm is driven by room fire events.

### Smoke detector (`SmokeDetector.cs`, 292 lines)

- Watches `detectFireInLocations`. Each listed room's `OnFireStart` starts `RingAlarm` (3 to 6 s delay, then 30 s of alarm), and `OnFireStop` calls `StopAlarm` (`SmokeDetector.cs:122-132`).
- Alarm only rings if `Powered` (fuse charge above 0). The battery is a `Fuse`.
- Drains `batteryDrainAmt` per second. Chirps every 33 s once charge is 40 or below (`SmokeDetector.cs:167-177`).
- A lit cigarette held in its trigger for more than 5 s rings the alarm (`SmokeDetector.cs:177-192`).
- `Start()` sets `Fuse.BatteryPower` to a random value in 45 to 100 (`SmokeDetector.cs:136`). This runs unconditionally after a null check that only covers the Usable, so a detector with no fuse seated may throw here. Unverified.
- Removing the battery (`RemoveBattery`, `SmokeDetector.cs:224`) happens on `Usable.OnBreak` and on player `Use()`.

---

## Electricity & lights

### Fuse (`Fuse.cs`, 141 lines)

- `BatteryPower` (`Fuse.cs:37-84`) ranges from 0 to `maxPowerCapacity` (100). The setter clamps at 0 and paints the fuse: green above 66, yellow from 33 to 66, red between 0 and 33, black at 0. Each colour change fires `OnFuseLevelChg`, which slots use to revalidate power (`FuseBox_Slot.cs:83-86`).
- `HasPower` is `BatteryPower > 0`.
- `EmptyPower()` and `FillPower()` set the charge to 0 or `maxPowerCapacity` (`Fuse.cs:132-140`).
- `Use()` transfers the fuse into the other hand's `IBatteryPowered` item, if one is held (`Fuse.cs:111-120`). Implementers are `Fuse`, `Radio`, `SmokeDetector`, `VacuumCleaner`, and each one's `ModifyPower` drains the fuse.
- `Explode()` (`Fuse.cs:122`) runs on `Usable.OnBreak`.

### FuseBox_Slot / FuseBox_Button / FuseBox_Manager

**FuseBox_Slot** (`FuseBox_Slot.cs`).

- Accepts a fuse on trigger entry, if the slot is empty, the fuse is not socketed, and either `Time.time < 1.5` or `fuseBox_Manager.snapTimer <= 0` (`FuseBox_Slot.cs:42-48`).
- `InsertFuse` kinematic-izes the fuse, disables its colliders and light, and poses it in the slot (`FuseBox_Slot.cs:68-88`).
- `ExpellFuse()` releases the fuse and sets `snapTimer` to 1 s on the manager (`FuseBox_Slot.cs:90-112`).
- `ValidatePowerState()` (`FuseBox_Slot.cs:114-140`) is the core power decision. It returns early for `Attic`, `Backyard`, `FrontYard`, `Shed`, `Basement_Backroom`, `Dream`, and `Other`, so those rooms are never switched through a slot. Otherwise the room is powered when `insertedFuse != null && insertedFuse.HasPower && fuseButton.IsOn`. It calls `OnPowerStateChg(room, bool)`.

**FuseBox_Button** (`FuseBox_Button.cs`).

- `IsOn` setter (`FuseBox_Button.cs:14-40`) fires `OnToggle` (which the slot listens to) and rotates the lever to ±45 degrees on Z.
- `Use()` flips `IsOn` (`FuseBox_Button.cs:56`).
- Each button has a `room` field (`Location`) that the slot uses.

**FuseBox_Manager** (`FuseBox_Manager.cs`).

- `fuseSlots` and `fuseButtons` are serialised arrays. `SetButtonAndSlotReferences` is a context menu helper.
- `Start()` wires each slot's `OnPowerStateChg` to `UpdateRoomPowerState` (`FuseBox_Manager.cs:189-196`).
- `UpdateRoomPowerState(Location, bool)` (`FuseBox_Manager.cs:57-67`) sets `IsPowered` on each `IElectronic` in `Locator.GetRoom(room).electronics`, using `GetComponent<IElectronic>()`. The game already uses `GetComponent<T>` with interface types in `Commons.cs:32`, so it presumably works, but this has not been confirmed at runtime.
- `UpdateHousePowerState(bool)` (`FuseBox_Manager.cs:69-75`) applies the state to every room in `Locator.rooms`. It does **not** apply the exclusion list from `ValidatePowerState`, so it can power the attic, shed and similar rooms.
- `ExpelAllFuses()` (`FuseBox_Manager.cs:77`) is called from the Ink story through `Dialogue.cs:217-219`.
- `LoadAllFuseSlots(List<JToken>)` sorts slot save data by the number in each slot's name (`FuseBox_Manager.cs:157-174`).

### IElectronic / IBatteryPowered (`IElectronic.cs`, `IBatteryPowered.cs`)

- `IElectronic` has one property, `bool IsPowered { get; set; }`. Implementers found: `LightSwitch`, `Computer_PowerButton`, `TV`, `Refrigerator`, `RefrigerationZone`, `Sparker`.
- `IBatteryPowered` has `Fuse Fuse { get; set; }` and `ModifyPower(float)`. Implementers found: `Fuse`, `Radio`, `SmokeDetector`, `VacuumCleaner`.

### LightSwitch (`LightSwitch.cs`, 181 lines)

- Implements `IElectronic`. `IsPowered` (default `true`) is a property whose setter calls `SetLampState()` (`LightSwitch.cs:46-61`). `SetLampState` (`LightSwitch.cs:121`) turns `linkedLights` on when `switchedOn && IsPowered`, plays `litUpSound`, and sets emission on lamp renderers when `lightHasRenderer` says so.
- `Use()` (`LightSwitch.cs:93-110`) toggles `switchedOn`, copies the value to `threewaySwitch.switchedOn` (without refreshing that switch's lamps), calls `SetLampState`, plays the flip animation, and raises `OnSwitched(bool)`.
- `Computer.RegisterLightSwitch` subscribes the computer's glow to `OnSwitched` (`Computer.cs:154`). It finds the switch through `Locator.GetLightSwitch`.
- `Flicker()` (`LightSwitch.cs:155`) is subscribed to `HouseManager.OnLightningStrike` (`LightSwitch.cs:86`). It dims the linked lights and recovers.
- `Locator.GetLightSwitch(Location)` (`Locator.cs:84-103`) returns the first `LightSwitch` in the room's electronics list, or null.

### Flashlight (`Flashlight.cs`, 85 lines)

Not on the grid and not an `IElectronic`. `Use()` toggles the spot light and its lens flare (`Flashlight.cs:65-69`). `SetLight(bool isOn, bool noAudio)` is public (`Flashlight.cs:76`). There is no battery drain in this file.

### LaserPointer (`LaserPointer.cs`, 58 lines)

`Toggled` (`LaserPointer.cs:102-115`) shows the line renderer and the dot, and calls `laserDot.GetComponent<Flame>().Toggle(toggled)`. The dot's flame can therefore ignite (its `canStartFires` is prefab-defined). Each frame, a raycast from the pointer places the dot on the hit surface (`LaserPointer.cs:131-141`).

### LightFlicker / MaterialColorFlicker

Purely cosmetic. `LightFlicker` (`LightFlicker.cs`) lerps a `Light` intensity, or the object's X/Y scale if there is no light, towards a random target. Retargets when `freqTimer` runs out. `MaterialColorFlicker` lerps the alpha of a material colour to a random value each frame.

---

## Physics & contact effects

- **BreakOnImpact** (`BreakOnImpact.cs`, 199 lines). The component on breakable objects.
  - `OnCollisionEnter` (`BreakOnImpact.cs:96`) calls `Impact` when the impulse per fixed step exceeds `collisionForceTreshold`, at least 0.2 s after spawn, and the object is not broken. With `impactMode` set, the other collider also needs an `Impacter`.
  - `Impact(dir, col, extraForce)` (`BreakOnImpact.cs:115`) adds a count, rolls `ignoreBreakCollChance`, and plays collision sounds. At `breakCollisionAmountsMax` (`BreakOnImpact.cs:129`) it runs `OnBreakAction`, spawns `breakEffectSpawn` (`amtToSpawn` plus or minus `randomAddedAmt`), pushes the spawned rigidbodies, plays break sounds, and destroys or disables itself and `objToAlsoDestroy` with `destroyChanceOnBreak`. Then it sets `broken = true` (`BreakOnImpact.cs:192`). `OnCollAction` fires on every impact.
  - `ForceBreak()` (`BreakOnImpact.cs:104`) sets the count to max minus one and calls `Impact()`. It is used by `Commons.Obliterate` (`Commons.cs:205`) and `ThiefHandler`.
  - Break effects are spawned by `Commons`, and gibs come from `Commons.GetGibs` (`Commons.cs:209`).
- **Impacter** (`Impacter.cs`, 36 lines). On collision with relative velocity above 4 it calls `ImpactDamage` (`Impacter.cs:17`). That applies `Health.Affect(-12..-18, EffectType.Impact)` to the other collider's `ActorStats` (`Impacter.cs:28-36`).
- **ImpactResistance** (`ImpactResistance.cs`, 10 lines). A `Stat` subclass defaulting to 1. It is referenced by `PlayerStats` and `StatType`, not by `BreakOnImpact`.
- **Chunk** (`Chunk.cs`, 36 lines). A counter, `colCount`, starting at -100 (`Chunk.cs:5`). Each collision enter increments it, and each exit decrements it. When it reaches 0 or below on exit, the rigidbody becomes non-kinematic and gravity is turned on (`Chunk.cs:145-153`). Nothing in this file sets kinematic, so the initial state is set on the prefab. The purpose of the -100 start is unclear.
- **Gib** (`Gib.cs`, 12 lines). Empty `Start` and `Update`. A marker for gib prefabs.
- **IDamager** (`IDamager.cs`). Interface exposing `OnDamageRegistered` and `OnDamageDealt`. Implemented by `Cutter` (`Cutter.cs:4`). `Contact_JauntMarker` subscribes to `Cutter`'s events.
- **IObliterateable** (`IObliterateable.cs`). Interface with `Obliterate()`. Implemented by `BrainCat`, `BrainRat`, `Microwave_Interface`, and `Player_Abilities`. `Commons.Obliterate` (`Commons.cs:177`) and the explosion helper in `Commons.cs:140-175` call it for objects within `obliterateRadius`.
- **EffectType** (`EffectType.cs`). `Impact`, `Cut`, `Burn`. Passed to `Health.Affect`, `Usable.Damage`, and similar.
- **Contact_JauntMarker** (`Contact_JauntMarker.cs`, 50 lines). Types are `Contact`, `Damage`, and `Food`. Only `Contact` (on collision) and `Damage` (via `Cutter.OnDamageRegistered`) do anything in this file. `Food` is unused here. In both cases it sets `lastWielder.JauntBody` to a `BrainNeetAI`-owned target, and clears it when that target's health reaches zero (`Contact_JauntMarker.cs:34`, `:38-49`). The meaning of `JauntBody` lives in `Player_Abilities` and is not covered here.
- **Contact_Skeletonizer** (`Contact_Skeletonizer.cs`, 25 lines). On collision with an enabled `Player_Abilities` that is not `unSkeletonizeable`, calls `SkeletonizeAccessor()` (`Contact_Skeletonizer.cs:13`), optionally swaps materials, then destroys itself. One-shot.
- **MakeStickyOnContact** (`MakeStickyOnContact.cs`, 12 lines). Sets `Usable.sticky = true` on any `Usable` it collides with (`MakeStickyOnContact.cs:9`). Nothing here clears it.
- **TransferStatusEffectOnContact** (`TransferStatusEffectOnContact.cs`, 43 lines). Adds `StatusEffect`s from `effects` to a `StatusHandler` on collision. With `allAtOnce` it adds all of them. Otherwise it picks `effects[Random.Range(0, effects.Length - 1)]` (`TransferStatusEffectOnContact.cs:32`). **Bug:** `Random.Range` on ints excludes the upper bound, so the last effect in the array can never be chosen by the random path. `deleteThisOnTransfer` and `deleteObjOnTransfer` self-destruct.
- **KnotList** (`KnotList.cs`, 53 lines). Not physics. A `ScriptableObject` list of Ink story knots with `accessible`, `read`, and `timeStamp` flags, with helpers to draw or lock them.

---

## Noise

**There is no gameplay noise or hearing system in `Assembly-CSharp`.**

- `NoiseGenerator.cs` (325 lines) is a static class that builds procedural textures. `Generate2D` and `Generate3DAtlas` (`NoiseGenerator.cs:7`, `:66`) stamp recursive quads or circles into a float grid, normalise the result, and return a `Texture2D`. The `noiseType` static field selects quad, circ, or pixel (`NoiseType.cs`). The class logs iteration counts with `Debug.Log`.
- A search for `NoiseGenerator`, `noise`, `hearing`, `listen`, and `audible` across `Assembly-CSharp` finds no callers outside `NoiseGenerator.cs` itself. The NoiseGenerator is not used by AI, by sound, or by any gameplay system in this assembly. It may be used by shaders or by assets outside this assembly (unverified).
- AI "detection" is entirely spatial queries. `BrainRat.DetectPlayer` and `BrainCat.DetectPlayer` use `Physics.OverlapBox` in front of the agent on the `Player` layer (`BrainRat.cs:276`, `BrainCat.cs:298`). `BrainAgentCop.DetectNearbyDoor` uses a short raycast on the `Usable` layer (`BrainAgentCop.cs:178`). None of them listens for sound.
- The word "noise" otherwise appears only in audio-related names, such as `Cigarette.InhaleNoise` (`Cigarette.cs:104`, not inspected in full) and `Player_Movement.GetFloorBasedStepNoise` (`Player_Movement.cs:456`), which picks a footstep clip. Nothing listens to them.

If the modding framework needs an AI-hearing system, it has to be built from scratch. There is no event, bus, or listener to hook.

---

## Weather & sky

The house has no weather system: it is one fixed scene, a rainy night with thunder. No clock, no time of day, no wind, clouds, snow or seasons. Measured in a running game (64-bit, 2026-10-27) and read from the code:

- **Light.** "Directional light Moon" (blue, intensity 0.2, soft shadows) is the only directional light on and is `RenderSettings.sun`. "Directional light Sun" (orange, 2.8) exists but is disabled. Ambient is flat black, fog is black ExponentialSquared at 0.06, the skybox is `Skybox_CubeTest`. Six `Star (n)` sprites with `LightFlicker` sit about 100 units west.
- **Sun and moon don't move in the house.** Both `SunRotation` components in `NEET_House` are disabled with `cycleSpeed` (0,0,0). Only the main menu's sun turns (0.1 degrees per frame, a full turn in about 25 s at 143 fps). Tutorial has one static light; DreamZone randomises its lights and sky tint (`DreamRandomizer.cs:61-72`).
- **Rain.** Four particle systems (`Particle_Rain_Frontyard` about 120 particles, `_Backyard`, `_DinnerRoom`, `_LivingRoom` about 30 each). Three carry a `Water` component, and `Flame.ParticleHandle` (`Flame.cs:443-466`) puts out a flame hit by a `Water` particle, so rain puts out open flames in the yard. Rain paints nothing and doesn't make the player wet (`PlayerStats.wetness` only comes from water colliders).
- **Storm sound.** `HouseManager`'s first AudioSource is the rain-and-wind loop (`thunderStormAudio`), the next three the claps (`HouseManager.cs:337-338`, played at `:518-519`; the indices are fixed, so another AudioSource on that object breaks them). `BrainPlayer.cs:62-82` sets the loop to 1.0 in FrontYard/Backyard and 0.28 elsewhere (`Commons.ExteriorLocations`, which leaves out the Shed). Outdoors the player also gets rain in the eyes (`BrainPlayer.cs:105-111`) and breath fog (`Player_Abilities.cs:445-456`).
- **Thunder** (`HouseManager.Thunder`, `:488-524`): waits 30-180 s, pushes the Moon's intensity to 14-22 for the flash (per-frame lerps, so the flash is shorter at high frame rates), raises `OnLightningStrike` (only `LightSwitch.Flicker` listens: lamps dip, `LightSwitch.cs:86,155-175`), fades the Moon and sets it to **0**, plays a clap after 0.1-2.2 s and starts the next wait. `noThunder` stops the loop from starting. The story forces a strike through `SequenceDict["Thunder"]` (`:345-352`; Ghostboy's first Night 1 line).
- **Nothing is saved**: a load restarts the scene and the thunder timer. `church_Bell`/`Church_Bell` (`:526-529`) only waits and plays nothing.

Bugs (not fixed yet, see TODO.md):

- **A story strike ends the thunder for the session.** The sequence stops `thunderCorout` and starts `Thunder(instant: true)`, and an instant strike never schedules the next one (`if (!instant)` at `:520`). The loop started in `Start` isn't stored, so it only bites once the first random strike has happened. Measured at 5x speed: 8 strikes in 750 game seconds before, none in 850 after.
- **The Moon never comes back after a strike**: `Thunder` ends with `intensity = 0` and nothing restores 0.2 (a running `SunRotation` would, but it's off in the house). Hard to see; it does make the moon's shadows free by accident.
- **Moon shadows are costly**: with the Moon at 0.2 and soft shadows, the living room had 12% more draw calls (5,203 vs 4,586) and the backyard 15% more (18,571 vs 15,814) than with them off, for a light that is almost invisible. `PerformanceFixes` `ShadowControl` leaves directional lights alone.

---

## Misc world objects

| Class | File | What it does |
|---|---|---|
| SunRotation | `SunRotation.cs` | Rotates the sun light by `cycleSpeed` each `Update` (per frame, not time-scaled). Sets intensity from the height of the light's forward vector, lerped from 0 to the cached intensity. Only runs in the main menu; in `NEET_House` both are disabled (see Weather & sky). |
| Billboard | `Billboard.cs` | Starts `Commons.Billboard`, a coroutine that turns the object to face `HouseManager._.mainCamera` at the end of every frame (`Commons.cs:40-48`). |
| ActiveWhileInside | `ActiveWhileInside.cs` | Disables `activatedObject` on `Start`. Enables it on each `OnTriggerStay` from an object with `checkForTag`, and disables it again on `OnTriggerExit`. |
| PickupPackageArea | `PickupPackageArea.cs` | The package delivery area. Collects `Cardboard_Box` objects that enter. A box not yet opened and not locked gains 2x `Time.deltaTime` to `soldFreezeTimer`. After more than 10 s it is locked (`PickupPackageArea.cs:87`). A `DeliveryGuy` entering with locked boxes takes the package and calls `SellBoxes` (`PickupPackageArea.cs:122`). That adds `SellBoxContent()` to `HouseManager._.mainComputer.Money` and destroys the boxes. Save stores locked box `saveId`s. |
| LooseChangeVolume | `LooseChangeVolume.cs` | Empty marker class. Referenced by `Spawner.cs`. |
| FerrisWheel | `FerrisWheel.cs` | At `Start`, instantiates 13 cabins from `cabin` and disables the original. Each frame it orbits cabins at `i * 30 + time * 5` degrees around `core`, and spins the core by 0.12 degrees per frame. |
| Rotater | `Rotater.cs` | While `rotating`, rotates each frame by `worldRotate` (world space) and `localRotate` (local space), and orbits around `rotateAroundAnchor` by `rotateAroundRot` if that vector is non-zero. Not time-scaled. |
| RandomRotate | `RandomRotate.cs` | Each frame adds a random rotation within `±range` degrees around up and right. Jitter. |
| Hoverer | `Hoverer.cs` | Each frame lerps the position between `initPos` and `initPos + downPos` using `sin(time * freq) / 2 + 0.5`. |
| MirrorScript | `MirrorScript.cs` | Configuration only: texture size, clip offset, layers, recursion, and so on. No logic. |
| MirrorCameraScript | `MirrorCameraScript.cs` | Renders the reflection camera (`RenderMirror`, `CreateRenderTexture`, `CalculateReflectionMatrix`). Not read in full. |
| MirrorReflectionScript | `MirrorReflectionScript.cs` | Finds a `MirrorCameraScript` in its parent's children at `Start`, and calls `RenderMirror()` from `OnWillRenderObject`. Logs an error if none is found. |

---

## Modding notes

### Dev-tool actions

These are the public entry points. Check each for `null` before calling, since most assume the room exists in `Locator.rooms`. `Locator.StartRoomFire`, `GetRoomCenter`, and `GetRoom` do not guard against missing keys except where noted.

- **Start a fire in a room.** `Locator.StartRoomFire(loc, origin)`, where `origin` is offset from the centre (see the gotcha in Fire & heat). Example: `Locator.StartRoomFire(Location.Kitchen, Locator.GetRoomCenter(Location.Kitchen) + Vector3.up * 0.3f);`
- **Stop a room's fire.** `var r = Locator.GetRoom(loc); if (r != null && r.fireSpreader != null) r.fireSpreader.GetComponent<FireSpreader>().Clear();`. `GetRoom` returns null for a missing room.
- **Ignite or extinguish one object's flame.** `flame.Ignite(force: true)` and `flame.Extinguish()`. Use `force` to bypass `sterile`. `preventIgnition` and `preventExtinguish` are still respected.
- **Trigger the room smoke alarm.** `Locator.GetRoom(loc).OnFireStart?.Invoke();` starts `RingAlarm`. The alarm checks `Powered` and does nothing if the detector has no charge. `OnFireStop?.Invoke()` stops it. These are the same events the fire system raises.
- **Power a room, respecting fuses.** `fuseBox.fuseButtons[i].IsOn = true;` where `fuseBox` is `HouseManager._.fuseBox_Manager` and `fuseButtons` lists the buttons. The setter raises `OnToggle` and revalidates the slot.
- **Power a room directly (bypasses fuses).** `HouseManager._.fuseBox_Manager.UpdateRoomPowerState(loc, bool)` sets `IsPowered` on that room's electronics. The next `ValidatePowerState` on a slot in that room overrides it.
- **Power the whole house.** `HouseManager._.fuseBox_Manager.UpdateHousePowerState(bool)`. This includes rooms that the slot logic normally excludes.
- **Fill or empty a fuse.** `fuse.FillPower()` and `fuse.EmptyPower()`. Emptying a fuse in a slot turns off that room on the next revalidation, which happens through the colour-change event.
- **Eject all fuses.** `HouseManager._.fuseBox_Manager.ExpelAllFuses()`.
- **Toggle a light.** `lightSwitch.Use()`, which also raises `OnSwitched`. To set the state without toggling, use `lightSwitch.switchedOn = x; lightSwitch.IsPowered = lightSwitch.IsPowered;`. The `IsPowered` setter is public and refreshes lamps through `SetLampState`. This does not raise `OnSwitched` and does not run the flip animation.
- **Lock or unlock a door.** `door.Locked = true;` or `door.Locked = false;`. This fires the events. It does not move the door.
- **Slam a door open or shut.** `StartCoroutine(door.Slam());`. `Slam` is a coroutine, so a bare call does nothing. Its `slamClosed` argument is unused.
- **Break a door.** `door.GetComponent<BreakOnImpact>()?.ForceBreak();`. `Door` itself has no break method, and `unbreakable` doors have had their `BreakOnImpact` removed.
- **Ring the doorbell.** `HouseManager._.doorbell.Use();`.
- **Break any object.** `GetComponent<BreakOnImpact>()?.ForceBreak();`. For `Usable`-only objects, `usable.Damage(val, EffectType.Burn)` multiplies `val` by the object's `flammability` and breaks it once accumulated damage reaches 4 (`Usable.cs:631-644`).

### Harmony patch targets

- `Door.set_Locked` (the property setter, `Door.cs:117`), for lock/unlock logging.
- `Door.FixedUpdate` (`Door.cs:236`), for custom auto-close or open logic.
- `Locator.StartRoomFire` (`Locator.cs:105`), to observe or redirect room fires. This is the single entry point for every room fire, including Sparker, gas, and Flame.
- `FireSpreader.Clear` (`FireSpreader.cs:240`), to observe fire end.
- `Flame.Ignite` and `Flame.Extinguish` (`Flame.cs:147`, `:183`), for any flame.
- `FuseBox_Slot.ValidatePowerState` (`FuseBox_Slot.cs:114`), to override the grid decision.
- `FuseBox_Manager.UpdateRoomPowerState` and `UpdateHousePowerState` (`FuseBox_Manager.cs:57`, `:69`).
- `LightSwitch.Use` (`LightSwitch.cs:93`) and the `IsPowered` setter (`LightSwitch.cs:46`).
- `HouseManager.Thunder` (`HouseManager.cs:488`). The lightning event is raised inside this coroutine (`HouseManager.cs:507`). To react to lightning, subscribe to `HouseManager._.OnLightningStrike` at runtime rather than patching the coroutine.
- `Usable.Damage` (`Usable.cs:631`) and `Usable.Burn` (`Usable.cs:663`), for all burn and break behaviour.
- `BreakOnImpact.Impact` (`BreakOnImpact.cs:115`) and `ForceBreak` (`BreakOnImpact.cs:104`).

### Static lists to enumerate

- `HouseManager.doors` (`HouseManager.cs:128`).
- `HouseManager.lightSwitches` (`:130`), `gasButtons` (`:136`), `smokeDetectors` (`:152`), `paintTargets` (`:132`), `sinkFaucets` (`:134`), `sitEnablers` and `restEnablers` (`:138-140`), `mouseHoles` (`:142`), `spawners` (`:150`), `eventConnectors` (`:148`).
- `HouseManager.fuseBox_Manager` (`:94`) gives `fuseSlots` and `fuseButtons`.
- `Locator.rooms` (static dictionary, `Locator.cs:8`). Enumerate with `Locator.rooms.Keys`.
- `Locator.GetRoom(loc).electronics` and `.usables` (`Locator.cs:12-14`). The electronics list is editor-generated and may be stale.
- `Locator.GetRoom(loc).windows` (`Locator.cs:26`) is a `List<BreakOnImpact>`, populated by the `FindWindows` context menu.

### Gotchas

- **Save IDs.** Static objects are matched on load by `saveId`. `LoadStatic` builds a `Dictionary<int, O>` from the `HouseManager` list and calls `dictionary.Add(SaveID, ...)` (`SaveLoadHandler.cs:360-370`). Duplicate `saveId`s throw. A door or switch added at runtime is not in those lists and is never saved or loaded. Add it to the list, with a fresh id, if it must persist.
- **Id 0 is special for spawners and smoke detectors.** `GetTokensByType(..., ignoreIdZero: true)` skips id 0 for `Spawner` and `SmokeDetector` (`SaveLoadHandler.cs:173-174`).
- **Fire save order.** Room fires are cleared and rebuilt in `LoadFireSpreaders`. Any fire object you hold a reference to will be destroyed on load.
- **Fire spreader lookup.** `Locator.fireSpreader` is a `GameObject`, not a `FireSpreader`. Call `GetComponent<FireSpreader>()`.
- **Flame component removal.** `Flame.Extinguish` on a flame with `deleteFlameOnExtinguish` destroys the component. A held reference becomes invalid.
- **Unbreakable not restored.** See the `Door.Load` bug above.
- **Electronics list is static.** New electronics must be added to `Locator.electronics` manually, or they will not receive power changes.
- **Power exclusions.** Some rooms are excluded from slot-driven power (see `FuseBox_Slot.ValidatePowerState`). Do not assume a slot controls them.
- **Burn-collider lifetimes.** `AddBurnCollider` destroys its helper objects after `max(60 - 5 * flammability, 2)` seconds. `Instant` objects burn for 2 s, `Average` for 35 s, `Unaffected` for 60 s.

---

## Open questions

- **Natural fire burnout.** The duration of fire particles comes from the `FireParticle` prefab's `ParticleSystem` (`FireParticle.cs:66`). The prefab is in Resources, not in code. Check whether fires end on their own in practice.
- **Asset-defined values.** The `FireSpreader`, `FireSpreaderSphere`, `FireParticle`, and `FireParticle_Small` prefabs, plus the `Resources/Textures/Smoke15Frames` atlas, are loaded by name at runtime. Their settings have not been inspected.
- **`GetComponent<IElectronic>()` on interface types.** `FuseBox_Manager.cs:65` uses this. The game relies on the same pattern in `Commons.cs:32`, so it probably works. Confirm in-game.
- **Door `OnOpened` and `OnClosed`.** Declared and never raised. Possibly a leftover, or raised from the prefab's Unity events. Check the scene.
- **Door `unbreakable` on load.** Confirmed as a self-assignment bug (`Door.cs:170`). Whether the saved value is meant to be restored, or the prefab value is authoritative, is unclear.
- **Heat holder save order.** `SaveDataHeatHolder` may swap `heatIndex` and `decayDelayTimer` on save (`HeatHolder.cs:100`). Confirm before relying on heat being saved correctly.
- **SmokeDetector `Start`.** `Fuse.BatteryPower` is set without a null guard (`SmokeDetector.cs:136`). Check whether any detector can start without a seated fuse.
- **Chunk.** The `-100` start for `colCount` and what sets kinematic. Unclear.
- **TransferStatusEffectOnContact random range.** The last effect is never picked by the random path. Confirm this is a bug and not a deliberate choice.
- **Usable non-burn damage.** `Usable.Damage` uses `Flammability.Resistant` (1) for non-burn damage (`Usable.cs:631-644`). It is probably intentional but does not match the name.
- **Contact_JauntMarker `JauntBody`.** Its meaning is in `Player_Abilities`, which is outside this area.
- **MirrorCameraScript.** Not read in full.
- **Who listens to `NoiseGenerator`.** No caller found. It may be used by shaders or assets. Unverified.
- **Initial power state.** Neither `FuseBox_Slot.Start` nor `FuseBox_Manager.Start` calls `ValidatePowerState`; they only subscribe (`FuseBox_Slot.cs:35-40`, `FuseBox_Manager.cs:189-196`). Power is pushed on insert, button toggle, fuse colour change, or save load. A room with no fuse inserted at load keeps each device's own default `IsPowered` (for example, `LightSwitch` defaults to true). Confirm in-game.

## Demo restrictions

The demo closes parts of the house in two ways. Both are scene data, not code (found by inspecting the running game).

**Demo walls.** Root objects named `DEMO_BLOCKER_*`: a mesh with a scrolling "demo" texture (`TextureScroll`) and a solid `BoxCollider` across a doorway.

| Object | Where | Collider |
|---|---|---|
| `DEMO_BLOCKER_PANTRY` | Pantry doorway by the kitchen, around (8.5, 1.2, -4.3) | yes |
| `DEMO_BLOCKER_PANTRY (1)` | Outside, around (-12.2, 0.1, 0.2) | no (visual only) |
| `DEMO_BLOCKER_ATTIC` | Top of the attic stairs, around (9.8, 8.5, -6.7) | yes |
| `DEMO_BLOCKER_ROOF` | Roof access, around (2.8, 11.5, -4.1) | yes |

The pantry behind its wall is fully stocked with normal items (cans, cheese, candy, flour, candles, knives including `Knife_Jaunter`, mouse traps, knockout pills, a flashlight, a fire extinguisher...).

**Locked doors.** `Door.Locked` with a `doorId`; a `Key` whose `doorId` matches unlocks it when the player holds the key in one hand and the door in the other and uses it (`Key.cs`).

| doorId | Door | Key in the demo |
|---|---|---|
| `BASEMENT` | `HOUSE/Door_single (23)`, the black door behind boxes in the basement, also `unbreakable` | `Key_Basement`, dropped by the bad thief (see `demo-content.md`) |
| `DADYARD` | `HOUSE/Door_single (16)`, dad's yard door in the living room | `Key_DadYard`, in the living room |
| `Attic` | `HOUSE/Door_single (17)`, on the attic stairs | `Key_Of_Anger`, in the shed |

Behind the `BASEMENT` door is the `Basement_Backroom` location: a hanged corpse (`Player_NPC_Corpse_Hanged`), `Player_Original`, a `DemonCoreCrystal`, `Knife_Sacrifice Skull`, bones and a `Cardboard_Box_Parasite`. According to the game's data the demo does let the player in: the bad thief carries `Key_Basement` (see `demo-content.md`); not yet seen in play.

`HouseManager.demoMode` is the only demo flag in code: it blocks black/occult market orders (`Computer.OrderItem`) and keeps wandering NPCs out of the pantry (`HouseManager.cs:484`).

**Main menu.** The main menu scene (`level0`) has three doors (`Door_single`, `(1)`, `(2)`) but only two scene switches (`SceneSwitcherHouse`, `SceneSwitcherTutorial`) and two doormat labels (PLAY, TUTORIAL), and no teleporter. The third door leads nowhere in the demo. (From the scene file; not yet checked in the running main menu.)
