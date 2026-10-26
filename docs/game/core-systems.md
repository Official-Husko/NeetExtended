# Core systems & managers

Source: `decompiled/Assembly-CSharp/*.cs` (global namespace, Unity 2022.3.17f1, Mono). Paths below are relative to the project root. Line numbers are given where they help; they refer to the decompiled files, not the original project.

## Overview

The game is driven by a small set of singletons and static registries that all hang off the `NEET_House` scene.

1. **Boot.** The `MainMenu` scene is the default target in `SceneLoadingData.sceneNameToLoad` (`decompiled/Assembly-CSharp/SceneLoadingData.cs`). Scene changes go through the `Loading` scene: `SceneSwitcher.SwitchScene()` writes the target name into `SceneLoadingData.sceneNameToLoad` and loads `Loading`, and `SceneLoader` (`decompiled/Assembly-CSharp/SceneLoader.cs`) then async-loads that target. Death can instead call `SceneManager.LoadScene` directly (`LoadSceneOnDeath`).
2. **Scene setup.** `HouseManager` (`decompiled/Assembly-CSharp/HouseManager.cs`) is the central singleton, reached as `HouseManager._`. Its `Awake` looks up input actions and registers `_instance`. Its `Start` does the house-specific setup, but only when the active scene is `NEET_House`: it starts thunder and church-bell coroutines, adds the `"Thunder"` entry to `SequenceManager`, calls `Init()` on `PickupBank` and `BrainBank`, and subscribes the loose-change and dust spawners to saving.
3. **Registries are filled.** `PickupBank.pickupsDict` and `BrainBank.brainsDict` (static, string to `Spawnable`) are the item and NPC registries. `Locator.rooms` (static, `Location` to room GameObject) is filled by each `Locator.Awake`. `SequenceManager.SequenceObjDict` is built from the serialized `SequenceObjs` list in `SequenceManager.Awake`, while `SequenceDict` and `timedSequences` are filled by scene scripts (`HouseManager`, `Nightmare1_Sequences`, `SeqSpawnThiefIntro`).
4. **Spawners and sequences run.** `Spawner` components spawn prefabs on timers or on demand. `EncounterSpawner` runs the nightly visitor schedule from an Ink story. `SequenceManager.TriggerSequence` is the verb dispatcher that Ink scripts call through an external function binding. `EventConnector` wires physical events (collisions, triggers, fire) to enable/disable/destroy effects. `TimerSequence` subclasses fire after a countdown.
5. **Saving.** Objects register a `Func<SaveData>` with `SaveLoadHandler._.SubscribeSave(...)`. `SaveLoadHandler` (`decompiled/Assembly-CSharp/SaveLoadHandler.cs`, outside this area) writes all of them to `neetSave_<datetime>.json` in `Application.persistentDataPath`, with an autosave every 300 s while the player is in `Bedroom_Neet`. On load, objects with a `saveId` are re-linked through `SaveLoadHandler.loadedUsablesById` / `loadedBrainsById`. Several classes here (`SequenceManager`, `Spawner`, `EventConnector`, `TimerSequence`, `EncounterSpawner`) implement this pattern.

Two conventions show up everywhere in this area and matter for mods:

- Anything that is looked up by name goes through a static dictionary that is only populated by a specific code path (see the Modding notes section on registration order).
- `Location` enum values are stored as integers in saves, so the enum order is part of the save format.

---

## HouseManager

Source: `decompiled/Assembly-CSharp/HouseManager.cs`. Central singleton. `public class HouseManager : MonoBehaviour`.

**Purpose.** Holds references to almost every major house-level object (player, camera, pickup/brain banks, spawners, door/light/sink lists, input actions) and runs house-wide loops: stat-driven NPC spawning, wet-paint drying, thunder, corpse reaping, player respawn, and the brain spawn-point picker.

**Singleton.** `private static HouseManager _instance;` exposed as `public static HouseManager _ => _instance;`. `Awake` assigns `_instance` only if it is null. A duplicate instance calls `UnityEngine.Object.Destroy(this)` (destroys the component, not the GameObject). `_` is null until the first `Awake` runs.

### Key fields

Serialized (set in the scene/prefab, not in code):

| Field | Type | Meaning |
|---|---|---|
| `demoMode` | `bool` | Changes spawn-room exclusions (adds `Location.Pantry`) in `GetValidSpawnPointForBrain`. |
| `inputAsset` | `InputActionAsset` | Asset used in `Awake` (`Enable()`, then `FindAction`). See `Input_Asset` below. |
| `i_*` | `InputAction` (`move`, `i_look`, `i_left_hand`, `i_right_hand`, `i_pickup_rotation`, `i_sprint`, `i_crouch`, `i_zoompickup`, `i_use`, `i_drop`, `i_eyelid_menu`, `i_lockview`, `i_hunger`, `i_bladder`, `i_hygiene`, `i_energy`, `i_breath`, `i_exit`, `i_submit`) | Cached actions, found by name in `Awake`. Public so brains and UI can read them. |
| `noThunder`, `noChurchBell`, `noWaterDrying` | `bool` | Disable the corresponding loops (thunder and bell in `Start`, water in `Update`). |
| `thunderStormAudio`, `thunderAudio` (private `AudioSource[]`), `church_Bell` | `AudioSource` | Thunder audio. `thunderAudio` is `GetComponents<AudioSource>()`. |
| `mainDirectionalLight` | `Light` | Lightning flashes this light. |
| `player`, `playerPrefab`, `skeletonPrefab`, `mainCamera` | `GameObject` | Current player body, prefab for respawn, camera. `CreateAndSetNewPlayer` reassigns `player` and `mainCamera`. |
| `mainComputer`, `tv`, `doorbell`, `catFoodBowl`, `catLitterBox`, `copVan`, `dialogueManager`, `fuseBox_Manager` | various | Scene singletons referenced by other scripts. |
| `packageDropOffPoint`, `pickupPackageArea` | `Transform`, `PickupPackageArea` | Delivery drop-off. |
| `pickupBank` | `PickupBank` | Item registry SO. `Init()` is called from `Start` (NEET_House only). |
| `brainBank` | `BrainBank` | NPC registry SO. `Init()` is called from `Start` (NEET_House only). |
| `looseChangeSpawner`, `dustSpawner` | `Spawner` | Subscribed to save in `Start`. |
| `spawners` | `List<Spawner>` | Used by `Spawner.Start`: a spawner is saved only if it is in this list. Must be filled in the scene. |
| `eventConnectors` | `List<EventConnector>` | Declared; no code in this file reads it. |
| `doors`, `lightSwitches`, `paintTargets`, `sinkFaucets`, `gasButtons`, `sitEnablers`, `restEnablers`, `mouseHoles`, `smokeDetectors` | `List<...>` | Scene-object lists. `paintTargets` is used by the water-drying loop. The rest are read elsewhere. |
| `statusEffects` | `List<StatusEffect>` | Serialized. Copied to `statusEffectDict` (keyed by `StatusEffect.name`) in `Awake`. |
| `characterStoriesDict` | `Dictionary<string, CharacterStories>` | Created empty in `Awake`; filled by other code (`CharacterStories`). |
| `foodOnFloor` | `List<FoodObjTimeStamp>` | Created in `Awake`. |
| `reaper` | `GameObject` | Prefab for the Reaper that collects corpses. |
| `queuedCorpsesToReap` | `List<GameObject>` | Created only in the NEET_House branch of `Start`. |
| `spawnedSkeletonCount`, `spawnedSpiderCount` | `int` | Live counts of stat-driven spawns (cap 4 each, see `StatCheck`). |
| `statCheckTimer`, `waterDryCheckTimer` | `float` | Set to 120 and 600 in `Start` (NEET_House only). |
| `performStatChecks` | `bool` | Gates the hunger/energy stat checks in `Update`. Default in code is `false`; the scene must enable it. |
| `failedHungerChecks`, `failedEnergyChecks` | `int` | Failure counters, clamped to 0..15. |
| `functionOnlyStory` | `Ink.Parsed.Story` | Unused in this file. Note the `using Ink.Parsed;` import: it is the parsed type, not `Ink.Runtime.Story`. |

Static:

| Field | Value | Meaning |
|---|---|---|
| `roadSideSpawnPoint` | `(-12.53, 2, -17.96)` | Spawn point for visitors and for the `CopVan`-adjacent area. Also default in `EncounterSpawner`. |
| `roadSideSpawnPointFloor` | `roadSideSpawnPoint - up*2` | Same, lowered by 2 m. |
| `frontDoorStandPoint` | `(-4.04, 2, -4.46)` | Where visitors stand at the door (`EncounterSpawner` passes it as door destination with y = 0.05). |
| `fadingTransitionInstance` | `FadingTransition` | Static reference, not assigned in this file. |

### Events

- `public delegate void LightningStrike();` and `public event LightningStrike OnLightningStrike;` Raised by `Thunder()` at the peak of each flash, just before the light fades. Current subscriber: `LightSwitch` (`LightSwitch.cs:86`, `HouseManager._.OnLightningStrike += Flicker`).

### Key methods

- **`Awake()`** Enables `inputAsset`, looks up the 19 actions by name (`"Move"`, `"Look"`, `"Left_Hand"`, ..., `"Submit"`), sets `_instance` (or destroys a duplicate), builds `statusEffectDict`, and creates empty `foodOnFloor` and `characterStoriesDict`. Nothing else is initialised here (see the Modding notes about `queuedCorpsesToReap`).
- **`Start()`** Always: `thunderAudio`, `thunderStormAudio`, `Water.SetupSpheres()`. In `NEET_House` only:
  - starts `Thunder()` unless `noThunder`, and adds the `"Thunder"` entry to `SequenceManager._.SequenceDict` (calling it restarts a thunder coroutine with `instant: true`);
  - starts `Church_Bell()` unless `noChurchBell` (see note below);
  - calls `pickupBank.Init()` and `brainBank.Init()`;
  - subscribes `looseChangeSpawner.Save` and `dustSpawner.Save` to `SaveLoadHandler._`;
  - sets `statCheckTimer = 120`, `waterDryCheckTimer = 600`, and creates `queuedCorpsesToReap`.
- **`Update()`** Two loops. (1) If `performStatChecks`, count down `statCheckTimer`; every 10 s (reset value) it runs `StatCheck` on the player's `PlayerStats.hunger` (spawns `"SkeletonNeetNPC"`) and `PlayerStats.energy` (spawns `"Spider_House"`). (2) Every 120 s after the first 600 s, unless `noWaterDrying`, it walks `paintTargets` with `surfaceWet` (via `Commons.ExecuteMethodOverFrames`, 2 frames per item) and calls `ClearWaterChannel()` on any target whose last water splat is older than a random 600–900 s.
- **`StatCheck<S>(S stat, ref int failedChecks, Func<int> getSpawnCount, Action<int> addSpawnCount, string spawnName)`** (private, generic). If `stat.GetVal() < 20`, the failure counter goes up by 1, otherwise down by 2, clamped to `[0, 15]`. At 15 or more failures, if fewer than 4 of that type exist and a 10% roll passes, it spawns `BrainBank.brainsDict[spawnName].obj` at a point outside the player's room (`GetValidSpawnPointForBrain(excludeRoomPLayerIsIn: true)`), increments the count, and subtracts 4–6 from the failure counter. The spawned brain's `OnDestroyAction` decrements the count.
- **`CreateAndSetNewPlayer(GameObject prevPlayer, float fat = 0f, float bodyType = 0f)`** Respawn. Picks a point with `GetValidSpawnPointForBrain()`, moves the old body up 600 units, instantiates `playerPrefab`, sets blend-shape 2 to `fat`, sets the `BodyType` stat, reassigns `mainCamera`, rewires `Feedback_Sender._` post-processing volume/grading/bloom, and increments the `deathRespawns` ink variable in `Dialogue._.masterStory`. Callers: `Player_Abilities` (`.cs:1666, 1855, 1927`).
- **`GetValidSpawnPointForBrain(bool excludeRoomPLayerIsIn = false)`** Builds an exclusion array of `Location` values, picks a random non-excluded room via `Locator.GetRandomRoom(obj)`, and returns a navmesh-valid random point in it at 0.75 feather, plus 0.89 m up. Default exclusions: `Attic`, `Basement`, `Backyard`, `FrontYard`, `Shed`, `Basement_Backroom`, `Dream`, plus slot 7 (the player's current room if `excludeRoomPLayerIsIn`, else `Other`) and slot 8 (`Pantry` in demo mode, else `Other`). The array literal lists `Diningroom` in both slots and the two slots are then overwritten, so `Diningroom` is not excluded unless it is the player's room in the exclude case. Callers: `CreateAndSetNewPlayer`, `StatCheck`.
- **`Thunder(bool instant = false)`** Coroutine. Waits 30–180 s (or one frame if `instant`), ramps `mainDirectionalLight.intensity` up to a random 14–22 over up to 2 s, raises `OnLightningStrike`, fades out, waits 0.1–2.2 s, then plays one of `thunderAudio[1..3]` at pitch 0.85–1.15. Reschedules itself unless `instant`.
- **`Church_Bell(bool instant = false)`** Coroutine. Waits 300–420 s and then does nothing. The bell audio is never played in this method.
- **`SpawnCopVan()`** Instantiates `Resources.Load<GameObject>("CopVan")` at `(-13.02, -0.13, 6.3)` if `copVan` is null, and returns the new object (null otherwise). Callers: `Computer.cs:247`, `TaxBill.cs:60`, `Cardboard_Box.cs:251`.
- **`QueueCorpseReaping(GameObject)` / `DeQueueCorpseReaping(GameObject)`** Add or remove a corpse from `queuedCorpsesToReap` and start the reaper coroutine if none is running. Callers: `Player_Abilities.cs:1695, 1699`.
- **`SpawnReaper(GameObject corpse)`** Coroutine. Spawns the `"DarkFogParticle"` effect, waits 0.3 s, instantiates `reaper`, waits 4 s, turns the corpse into a glowing soul (`SoulifyCorpse`), rises it over about 4 s, instantiates `PickupBank.pickupsDict["Soul_Sphere"]`, flings the sphere toward the reaper, and destroys the reaper after roughly 10 s in total (approximate, from the wait times). Destroys the soul sphere if the player did not grab it. Processes the queue one corpse at a time.
- **`SoulifyCorpse(GameObject corpse)`** Clones the corpse, removes `Spawner`, `Player_Movement`, `Player_Abilities`, `CollisionPainter` components, swaps all materials to `Resources.Load<Material>("WaterRainbow")`, disables colliders, and adds a black-light child (`"CorpseSoulLight"`, intensity 10, range 5).

### Static state

- `_instance` (private static). The only static singleton state. It is set once and does not reset on scene load, but its `Awake` is what sets it, so a new scene's `HouseManager` re-takes it.

### Used by

`HouseManager._` is referenced from 42 files. The most common accesses are `player` (42 files), `mainComputer`, `foodOnFloor`, `statusEffectDict`, `mainCamera`, `i_look`, `move`, `mouseHoles`, `characterStoriesDict`, `tv`. Treat it as the global service locator for the house scene.

### Numbers

- Stat check: threshold 20, interval 10 s, failure cap 15, spawn threshold 15, 10% spawn chance, max 4 per type, reset 4–6.
- Water dry: initial 600 s, then every 120 s, wet time 600–900 s.
- Thunder: first strike 30–180 s after start, peak 14–22 intensity.
- Church bell: 300–420 s (no-op).
- Reaper: 0.3 s, 4 s, soul float, 1.5 s, 0.5 s, 0.5 s.
- Respawn: player lifted 600 units before teardown.

---

## Commons

Source: `decompiled/Assembly-CSharp/Commons.cs`. `public class Commons : MonoBehaviour` (never added to a GameObject in code; used as a static helper class).

**Purpose.** A grab bag of static helpers: coroutine wrappers, scene scanning, audio play-at-point, explosion damage, the gib/organ/turd helpers used by death and cleanup, paint spray, and persistence across scenes.

### Static fields and data

- `CyanDark` (`Color`). Only colour constant.
- `ExteriorLocations` (property, `List<Location>`): `[FrontYard, Backyard]`.

### Key methods

- `OrbitAround(Vector3 point, Vector3 centerOrb, Vector3 angles)`: rotates a point about a centre.
- `FindInterfaceObjectsOnUsables<T>()`: all `Usable` objects in the scene that have a component of type `T`. Used by `Locator.FindElectronics` with `IElectronic`.
- `Billboard(Transform t)`: coroutine that makes `t` look at `HouseManager._.mainCamera` every frame.
- `CrawlSceneForType<T>()`: all components of type `T` (including inactive) under the active scene's root objects. Used by `Spawner.GetLooseChangeVolumes`.
- `CoroutineWrapOneFrame` / `CoroutineWrapSeconds`: overloads taking `Func<T>` or `Action`. Note `CoroutineWrapSeconds<T>(Action f, float delay)` has an unused type parameter.
- `PlayClipAt(AudioClip, Vector3, pitch, volume)`: spawns a temporary 3D `AudioSource` GameObject named `"TempAudio"`, destroyed after the clip length.
- `GetAudioType(string extension)`: maps `.mp3`, `.wav`, `.ogg`, `.aiff`/`.aif`, `.aac` to `AudioType`.
- `ExecuteMethodOverFrames<T>(List<T>, int framesPerElement, Action<T>)`: coroutine, one item per `framesPerElement * deltaTime` seconds.
- `SetActiveDelayed(GameObject, bool, float delay)`: coroutine.
- `ExplosionDamage(pos, radius, damage, EffectType, LayerMask, obliterateRadius, canImpact)`: overlap sphere; applies an explosion force (100, impulse) to rigidbodies, damages `ActorStats` `Health` (if `ActorStats.canDamageOther`), optionally obliterates `IObliterateable` within `obliterateRadius`, and triggers `BreakOnImpact`. It applies `Health` damage twice for `ActorStats` (once in the first block, once after the obliterate check). This is a real duplication in the code, not a decompilation artifact.
- `Obliterate(GameObject gO, Liquid bloodColor, int bloodSpawnCount, GameObject[] gibObjs, int gibSpawnCount, int randomGibSpawn, bool includeOrgans)`: disables the camera (if it is not the player), colliders, spawns `"Particle_BloodBurst"` tinted by `Water.liquidColors[(int)bloodColor]`, spawns a paint spray, adds a `BreakOnImpact` (explosion mode), optionally spawns organs on break, then destroys the object 0.1 s later. Callers: 3 (`Commons.Obliterate` is used in gore paths).
- `GetGibs()`: `PickupBank.pickupsDict["Gib1"|"Gib2"|"Gib3"].obj`.
- `GetOrgansWithOffsets()`: `Heart`, `Brain`, `Eyeball` (x2), `Liver`, `Guts`, each with a local offset.
- `GetTurds()`: `Turd1`, `Turd2`, `Turd3`.
- `SpawnPaintSpray(GameObject gO, int paintColor, int paintSpawnCount, float randForce = 15f)`: adds a `Spawner` to `gO` that sprays `Water.liquidSpheres[paintColor]`, with `availableSpawnCount = paintSpawnCount`, `forceOffset = down*8`, `randomRadius = 1`.
- `SetPersistOnObj(GameObject obj, string destScene)`: attaches a `SceneObjSwitcher` so the object survives the scene change to `destScene`. After the switch the object is moved to the player and pushed onto `SaveLoadHandler._.spawnAfterLoad`. Callers: `DreamRandomizer` (`NEET_House` for held pickups), `Nightmare1_Sequences` (`glasses`).

### Used by

`Commons.Billboard` (2), `PlayClipAt` (5), `OrbitAround` (5), `CrawlSceneForType` (3), `SetPersistOnObj` (3 including the definition's callers), `GetGibs` (3), `GetOrgansWithOffsets` (2), `Obliterate` (3).

---

## ExtensionMethods

Source: `decompiled/Assembly-CSharp/ExtensionMethods.cs`. `public static class ExtensionMethods`.

**Purpose.** Extension helpers for arrays, lists, transforms, vectors, colliders and constraints.

| Method | What it does |
|---|---|
| `GameObject[].RandomGO()` | Random element. **Bug:** uses `Random.Range(0, arr.Length - 1)`, so the last element is never picked. |
| `T[].RandomVal()` / `RandomVal(out int)` | Random element (uniform). Logs an error on an empty array, then throws `IndexOutOfRange`. |
| `List<T>.RandomVal()` / `RandomVal(out int)` | Same for lists. |
| `BoxCollider.GetRandomPointInsideCollider()` | Random world point inside a box collider. |
| `GameObject.ToggleAllColliders(bool? force)` | Set or flip all child colliders. Used by `HouseManager.SoulifyCorpse`. |
| `GameObject.AddParentConstraint(...)` (2 overloads) | Adds a `ParentConstraint` to a source transform, if none exists. **Oddity:** the position offset is set twice; the second `SetTranslationOffset(0, posOffset)` overwrites the first. Same for rotation. |
| `GameObject.AddBreakOnImpactObjExplosion(...)` | Adds `BreakOnImpact` with explosion settings and `Resources.Load<AudioClip>("Audio/CorpseExplosion")` and `"Audio/Meat_Squish"`. Used by `Commons.Obliterate` and `Spider_House`. |
| `Transform.ResetLocal()` / `ResetWorld()` | Reset transform (`ResetWorld` also sets `localScale` to one). |
| `RendMatRef[].ToMats()` | Flattens renderer material indices to a `List<Material>`. |
| `Vector3.NormalizedMaxAxis()` / `NormalizedMinAxis()` | Returns the axis unit vector of the largest or smallest component. |
| `Vector3.RoundVector3ElementsBy(float)` | Round each axis to a multiple. |
| `Vector3.ToArray()`, `Quaternion.ToArray()` | To `float[]`. |
| `Quaternion.RotAxisEuler(Transform, x, y, z)` (and a `Vector3` overload) | Rotates about the transform's local axes. |

**Used by.** Commonly used across the codebase (`RandomVal`, `ToggleAllColliders`, `AddBreakOnImpactObjExplosion`). `DreamRandomizer` uses `RandomVal`. For modding, these are safe helpers to call from patches.

---

## Locator

Source: `decompiled/Assembly-CSharp/Locator.cs`. `public class Locator : MonoBehaviour`, plus the `Location` enum in the same file (see the next section).

**Purpose.** Each room in the house has a `Locator` component with a `Location` ID and a `BoxCollider`. The class registers rooms in a static dictionary, lets code ask "which room is this point in", picks random points inside rooms, and tracks which usable items and electronics are in each room. It also starts and tracks room fires.

### Static registry

- `public static Dictionary<Location, GameObject> rooms` is filled in `Awake`: `rooms.Add(roomId, base.gameObject)`. Static constructor creates the empty dictionary.
- `Awake` first clears the dictionary if its first entry's `GameObject` is destroyed (a crude scene-change check). It does not clear it otherwise, so stale entries from a previous scene can remain if the first one is alive. Duplicate `roomId` values are ignored (first one wins).

### Instance fields

| Field | Type | Meaning |
|---|---|---|
| `roomId` | `Location` | The room's ID (key in `rooms`). |
| `electronics` | `List<GameObject>` | Usables with `IElectronic` inside this room's collider. Built by the editor context menu `FindElectronics`. |
| `usables` | `List<GameObject>` | Usables currently inside the trigger (maintained by `OnTriggerEnter`/`OnTriggerExit`). Players are excluded. |
| `sitRestEnablers` | `List<GameObject>` | Sit/rest enablers added on enter. Entries are never removed (no exit handler). |
| `fireSpreader` | `GameObject` | The active room fire, if any. Cleared by `FireSpreader`. |
| `adjacentLocators` | `List<Locator>` | Neighbour rooms. Used by `BrainAgentCop`. |
| `OnFireStart`, `OnFireStop` | `Action` | Raised when a room fire starts or ends. `OnFireStop` is chained onto the spreader's `OnClear`. |
| `windows` | `List<BreakOnImpact>` | Filled by the context menu `FindWindows` (raycast sphere of 20×20 rays, 20 m, on `"Construction"` layer, names containing `"Window"`). Used by `ThiefHandler`. |

### Static methods (the room API)

- `GetRoom(Location)`: the room `Locator`, or null if the key is missing or destroyed. Safe.
- `GetRoomCenter(Location)`: `rooms[loc].transform.position`. **Throws** if the key is missing.
- `GetElectronics(Location)`: `electronics` for that room. Throws if missing.
- `GetLightSwitch(Location)`: first `LightSwitch` among the room's electronics (direct, then child). Logs and returns null if none. Used by `Computer`, `DemonicContraption`.
- `StartRoomFire(Location loc, Vector3 origin, bool byUsableCol, bool noStarterSphere)`: instantiates `"FireSpreader"` at the room centre if none is active, else spawns a fire sphere on the existing one. Callers: `Flame`, `GasButton`, `Sparker`, `SaveLoadHandler` (load path).
- `LocatePoint(Vector3 point)`: the room whose collider `Bounds` contain the point; otherwise the nearest room by bounds distance. The fallback loop is approximate (it only updates `result` when the running minimum changes), so treat its result as a best guess near room boundaries.
- `ParseLocationName(string)`: `Enum.Parse<Location>`. Throws on an unknown name. Used by `PickupBank.SpawnPickupInRoom`, `BrainBank.SpawnbrainInRoom` (console `spawnAt`).
- `GetRandomRoom()`: **Bug.** Returns `(Location)Random.Range(0, rooms.Count)`. That casts a random index to the enum, which does not match the actual keys when they are not contiguous. Not used by the rest of the codebase (only `GetRandomRoomCenter` calls it).
- `GetRandomRoom(Location[] exclusions)`: a random key from `rooms` not in `exclusions`. Used by `HouseManager.GetValidSpawnPointForBrain`. Throws if every room is excluded.
- `GetRandomRoomObj()`: rooms except `Other`, returning the `GameObject`. Used by `GetRandomPositionInRoom` when `loc == Other`.
- `GetRandomRoomCenter()`: centre of a random room (via the buggy `GetRandomRoom()`).
- `GetFlooredPositionInRoom(Location, Vector3)`: `point` with y set to the room collider's floor.
- `GetRandomPositionInRoom(Location loc = Location.Other, float featherBoundariesMult = 1f)`: random point inside the room's `BoxCollider`, shrunk by `featherBoundariesMult`. Callers include `BrainNeetAI`, `BrainCat`, `BrainRat`, `BrainAgentCop`, `ThiefHandler`, `HouseManager`.

### Editor-only context menus

`FindElectronics`, `FindWindows`, `TallyPaintScore` (empty). These are `[ContextMenu]` methods, so they only run in the editor. They are not a runtime API.

### Gizmos

`OnDrawGizmos` draws a yellow sphere on each electronic.

---

## Location

Source: `decompiled/Assembly-CSharp/Locator.cs` (lines 244-269). `public enum Location` (default `int` backing, declaration order).

**Purpose.** The set of rooms and "areas" in the game. Used as the key for `Locator.rooms`, as `Usable.currentLocation`, `Brain.currentLocation`, and in many spawn exclusions.

**Important for mods and saves.** Values are the declaration order (zero-based). `SaveLoadHandler` reads `currentLocation` from JSON as an integer (`(Location)token["currentLocation"].Value<int>()`, `SaveLoadHandler.cs:481`), so the save format depends on these numbers. Reordering the enum breaks old saves. **Add new values only at the end.**

| Value | Name |
|---|---|
| 0 | `Diningroom` |
| 1 | `Kitchen` |
| 2 | `Entrance` |
| 3 | `Livingroom` |
| 4 | `BathroomFloor` |
| 5 | `Pantry` |
| 6 | `Bedroom_Neet` |
| 7 | `Bedroom_Parents` |
| 8 | `Lobby` |
| 9 | `Bathroom_1F` |
| 10 | `Attic` |
| 11 | `Lounge` |
| 12 | `Basement` |
| 13 | `Stairs_Main` |
| 14 | `Corridor_Main` |
| 15 | `Stairs_Basement` |
| 16 | `Stairs_Attic` |
| 17 | `Backyard` |
| 18 | `FrontYard` |
| 19 | `Other` |
| 20 | `Shed` |
| 21 | `Basement_Backroom` |
| 22 | `Dream` |

Notes:

- `Other` is a sentinel and is not necessarily registered as a room. Code uses it as "no room" and in exclusions. `GetRandomPositionInRoom(Other)` means "any room".
- Only rooms that have a `Locator` in the loaded scene exist in `rooms`. Which of these are present in `NEET_House` vs `Dream` is a scene question (see Open questions).
- `Location.ToString()` and `Enum.Parse` use these names, so the console `spawnAt` command also takes these exact strings.

---

## SequenceManager

Source: `decompiled/Assembly-CSharp/SequenceManager.cs`. `public class SequenceManager : MonoBehaviour`.

**Purpose.** Scripted-event hub. Ink (dialogue) scripts call `TriggerSequence(funcName, arg)` through an external function binding, and the manager dispatches to enable/disable/destroy scene objects, spawn pickups or brains, call named delegates, and drive `TimerSequence` objects. It also persists which scene objects are sequence targets.

**Singleton.** `public static SequenceManager _;` set in `Awake` only if null. Not destroyed if a duplicate appears (the duplicate still runs its `Awake`).

### Fields

| Field | Type | Meaning |
|---|---|---|
| `SequenceObjs` | `List<GameObject>` (serialized) | Scene objects that scripts can address by name. |
| `SequenceObjDict` | `Dictionary<string, GameObject>` | Built from `SequenceObjs` by `GameObject.name` in `Awake`. Rebuilt in `Load`. |
| `SequenceDict` | `Dictionary<string, Action<string>>` | Named callbacks invoked by the `Execute` verb. Filled by scene scripts (`Nightmare1_Sequences`, `HouseManager`). |
| `timedSequences` | `Dictionary<string, TimerSequence>` | Created in `Awake`. Filled by `TimerSequence` subclasses (`SeqSpawnThiefIntro`). |

### Methods

- **`Awake()`** Sets `_` (if null), creates `timedSequences`, and adds every `SequenceObjs` entry to `SequenceObjDict` by name. Duplicate names throw.
- **`Start()`** Binds the external function `"TriggerSequence"` on `Dialogue._.masterStory` (so Ink can call it), and subscribes `Save` to `SaveLoadHandler._`.
- **`TriggerSequence(string funcName, string arg)`** The verb dispatcher. Verbs:

| `funcName` | `arg` format | Effect |
|---|---|---|
| `Enable` | object name | `SetActive(true)`. Clears `Usable.disableOnStart` if the object has `Usable`. |
| `Disable` | object name | `SetActive(false)`. |
| `Toggle` | object name | Calls `SetActive(activeInHierarchy)`, which re-applies the current state. As decompiled this is effectively a no-op (it never negates), so it does not flip the object. It clears `Usable.disableOnStart` only if the object is already active. |
| `Destroy` | object name | `Object.Destroy`. |
| `SpawnPickupAt` | `Name@x,y,z@rx,ry,rz` | Instantiates `PickupBank.pickupsDict[Name].obj` at the position with the rotation. |
| `SpawnBrainAt` | `Name@x,y,z@rx,ry,rz` | Instantiates `BrainBank.brainsDict[Name].obj`. **Bug:** the rotation is parsed from the position segment (`array3[1]`), not the rotation segment (`array3[2]`). |
| `Execute` | `SequenceDict` key | Calls `SequenceDict[arg]("")`. |
| `TimedSeqToggle` | sequence name | Flips `TimerSequence.active`. |
| `TimedSeqActivate` / `TimedSeqDeactivate` | sequence name | Sets `active`. |
| `TimedSeqSetTimer` | `name@seconds` | Sets timer only if it is currently running (`timer > 0`). |
| `TimedSeqSetTimerMinOf` | `name@seconds` | Sets timer to `seconds` only if that is smaller than the current timer. |

Unknown verbs log `"Could not find trigger sequence method being called: ..."`. Missing object names throw (`KeyNotFoundException`) because there is no guard.

- **`Load(JToken)`** Rebuilds `SequenceObjs[i]` for entries typed `"Usable"` or `"Brain"` from `SaveLoadHandler.loadedUsablesById` / `loadedBrainsById`, then rebuilds `SequenceObjDict` by name. There is no null check on entries in that rebuild loop.
- **`Save()`** Returns a `SaveDataSequenceManager` with per-slot save ids and type tags (`"Usable"`, `"Brain"`, `"Other"`, `"Missing"`).

### Events and delegates

- `SequenceDict` is the callback registry. The entry `"Thunder"` is added by `HouseManager.Start` (NEET_House only). `"DisappearAndSpawnWeapon"` is added by `Nightmare1_Sequences.Start`.

### Used by

- `SequenceManager._` is used in `HouseManager`, `SeqSpawnThiefIntro`, `Nightmare1_Sequences`, and `SaveLoadHandler` (`.cs:487, 494`).
- The Ink dialogue reaches it through `Dialogue._.masterStory.BindExternalFunction("TriggerSequence", ...)`. The `.ink` source is not in the decompiled code.

---

## TimerSequence

Source: `decompiled/Assembly-CSharp/TimerSequence.cs`. `public class TimerSequence : MonoBehaviour`.

**Purpose.** A base class for one-shot countdowns that fire `RunSequence()` when they reach zero.

| Member | Meaning |
|---|---|
| `sequenceName` (string) | Key in `SequenceManager._.timedSequences`. Set by the subclass. |
| `active` (bool) | When true, the timer ticks down. |
| `timer` (float) | Seconds remaining. |
| `virtual void RunSequence()` | Override to define the event. Called once, then `active` is set to false. |
| `protected void Update()` | Ticks down `timer` with `Time.deltaTime`. |
| `Save()` / `Load(JToken)` | Persist `sequenceName`, `active`, `timer`. Type tag `"TimerSequence"`. Restored via `SaveLoadHandler.cs:494`. |

Subclass: `SeqSpawnThiefIntro` (see its section below).

---

## EventConnector

Source: `decompiled/Assembly-CSharp/EventConnector.cs`. `public class EventConnector : MonoBehaviour, ISaveableStatic, ISaveableObj, ISaveable`.

**Purpose.** A data-driven "when X happens, do Y" component. Each entry (`EventCon`) has an event kind, an optional trigger filter, a list of effects to apply to target objects, and a `UnityEvent` for custom wiring. Used for things like "when this flame is extinguished, enable that object".

### Serialized data

- `evtCons` (private `EventCon[]`). Each `EventCon` has:
  - `eventKind` (`EventKind`);
  - `objToCheckColTrigger` (GameObject) or `typeToCheckColTrigger` (string component type name). If the string is non-empty it is used, otherwise the GameObject;
  - `deleteObjOnExecute` (bool): destroys the colliding object after firing;
  - `unityEventFirst` (bool): whether `useEvent` runs before or after the effects;
  - `effects` (`EffectToTarget[]`): each has an `EffectKind` and an array of target GameObjects;
  - `useEvent` (`UnityEvent`): custom wiring in the inspector.
- `saveId` (int), `executed` (bool, tooltip "Only works on enable/disable"), `shouldExecuteAgainOnLoad` (bool).

### Enums

- `EventKind`: `OnIgnite`, `OnExtinguish` (both listen to `Flame.OnIgnited` / `Flame.OnExtinguished` on the same GameObject), `OnCollisionEnterObj`, `OnTriggerEnterObj`, `OnCollisionExitObj`, `OnTriggerExitObj` (these fire from the matching Unity physics callbacks on this object), and `External` (only fires via `ExternalExecute`).
- `EffectKind`: `Enable`, `Disable`, `Toggle`, `Destroy`, `SetKinematic`, `RemoveKinematic`, `ToggleKinematic`.

**Gotcha.** `SetKinematic` sets `isKinematic = false`, and `RemoveKinematic` sets it to `true`. The names are the reverse of what the code does, so do not rely on the names.

### Events and delegates

Public `Action` fields: `colEnterDeleg`, `triggerEnterDeleg`, `colExitDeleg`, `triggerExitDeleg`. These are populated in `Start` by `EventCon.SetListeners`, which adds each `EventCon.Execute` to the matching field, and are invoked from the Unity physics callbacks. Other scripts can subscribe to them. `Flame`'s `OnIgnited` and `OnExtinguished` are subscribed the same way.

### Methods

- `Start()` calls `SetListeners` for each entry, and subscribes `Save` to `SaveLoadHandler._`.
- `OnCollisionEnter`, `OnTriggerEnter`, `OnCollisionExit`, `OnTriggerExit`: for each matching `EventCon`, invoke the delegate, set `executed = true`, and destroy the other object if `deleteObjOnExecute`. Note the delegate is invoked without a null check.
- `ExternalExecute(bool force = false, bool skipUnityEven = false)` / `ExternalExecute()`: run every `External` entry (or all, if `force`). Caller: `ClosedEyeTrigger.cs:42`.
- `ExternalExecuteInverse(...)`: runs the inverse effects.
- `EventCon.Execute(bool skipUnityEvent)`: runs `useEvent` first or last, depending on `unityEventFirst`, then applies effects. Wrapped in try/catch that just logs `"Could not execute event connector"`. `ExecuteInverse` has no try/catch.
- `Save()` / `Load(JToken)`: persist `saveId` and `executed`. On load, if `shouldExecuteAgainOnLoad` the effects re-run (`ExternalExecute(force: true)` or the inverse), otherwise a previously `executed` object is deactivated.

### Used by

Attached to scene objects. The only code call is `ClosedEyeTrigger`. Inspector wiring is not visible in code.

---

## SceneSwitcher

Source: `decompiled/Assembly-CSharp/SceneSwitcher.cs`. `public class SceneSwitcher : MonoBehaviour`.

**Purpose.** A trigger volume that changes scenes through the `Loading` screen when the player enters it.

| Member | Meaning |
|---|---|
| `sceneNameToLoad` (string) | Target scene. Written to `SceneLoadingData.sceneNameToLoad`. |
| `sceneSpawnPoint` (Vector3) | Declared; not read in this file. |
| `OnUnload` (Action) | Invoked before the scene change. `DreamRandomizer` subscribes `PersistHeldDreamPickups`. |

- `OnTriggerEnter(Collider)`: if the collider's GameObject is `HouseManager._.player`, calls `SwitchScene()`.
- `SwitchScene()` invokes `OnUnload`, sets `SceneLoadingData.sceneNameToLoad`, and loads `"Loading"`. `Loading` then loads the target asynchronously (see `SceneLoader`).

**Used by.** `DreamRandomizer.sceneSwitcher` (`SwitchScene` after 15 s in the dream, `DreamRandomizer.cs:199`). Scene-side trigger placement is not in code.

---

## SceneLoader and SceneLoadingData

Sources: `decompiled/Assembly-CSharp/SceneLoader.cs`, `decompiled/Assembly-CSharp/SceneLoadingData.cs`.

- `SceneLoadingData` is a static class with one field: `public static string sceneNameToLoad = "MainMenu";`. It is the handoff from whichever code starts a scene change to the `Loading` scene.
- `SceneLoader : MonoBehaviour` lives on the `Loading` scene. In `Start` it sets a face texture obtained from `PlayerStats.GetFaceTexture` on its `MeshRenderer`, then in a coroutine waits one frame and calls `SceneManager.LoadSceneAsync(SceneLoadingData.sceneNameToLoad)` with `allowSceneActivation = false`. It flips activation to true once `progress >= 0.9`.

**Modding use.** Setting `SceneLoadingData.sceneNameToLoad` to any scene name and then loading `"Loading"` is the simplest way to route to a new scene (provided the scene is in build settings). Nothing in code validates the name.

---

## SceneObjSwitcher

Source: `decompiled/Assembly-CSharp/SceneObjSwitcher.cs`.

Attached dynamically by `Commons.SetPersistOnObj`. In `Start` it marks the GameObject `DontDestroyOnLoad` and makes its rigidbody kinematic. Each frame it checks whether `destinationScene` is loaded. When it is, it moves the object into that scene, un-kinematics the rigidbody, invokes `OnPostSwitchAction`, and removes itself. Until then it keeps the object alive.

Fields: `destinationScene` (string), `OnPostSwitchAction` (Action). `OnPostSwitchAction` is set by `Commons.SetPersistOnObj` to move the object to the player and push it onto `SaveLoadHandler._.spawnAfterLoad`.

---

## LoadSceneOnDeath

Source: `decompiled/Assembly-CSharp/LoadSceneOnDeath.cs`.

Added to the player by `Dialogue.AddPlayerSceneSwitchOnDeath(string scene)` (`Dialogue.cs:373-379`). In `Awake` it chains `SwitchScene` onto `Player_Abilities.OnPreDeath`. `SwitchScene` calls `SaveLoadHandler._.SaveAll()` and then `SceneManager.LoadScene(sceneNameToLoad)` directly (no Loading screen).

---

## Spawnable

Source: `decompiled/Assembly-CSharp/Spawnable.cs`. `[Serializable] public struct Spawnable(GameObject gameObject)` (C# primary constructor).

| Field | Type | Meaning |
|---|---|---|
| `obj` | `GameObject` | The prefab. |
| `locked` | `bool` | Declared and default `false`. Not read in this area. |

This is the element type of `PickupBank.pickups`, `BrainBank.brains`, and the two static dictionaries.

---

## Spawner

Source: `decompiled/Assembly-CSharp/Spawner.cs`. `public class Spawner : MonoBehaviour, ISaveable, ISaveableStatic, ISaveableObj`.

**Purpose.** Generic spawner: periodically (or on demand) instantiates a random prefab from `spawns` at an anchor or random point, optionally limited by a count, a concurrency cap, and a `spawnCondition`. Saved and restored when registered.

### Fields

| Field | Type | Meaning |
|---|---|---|
| `spawns` | `GameObject[]` | Prefabs to pick from at random. |
| `spawnAnchors` | `Transform[]` | Spawn points, cycled in order, or random if `randomSpawnPoints`. If empty, the spawner's own transform is used. |
| `randomSpawnPoints` | `bool` | Pick anchors randomly. |
| `useAnchorBounds` | `bool` | Spawn at a random point inside the anchor's `BoxCollider`. |
| `randomRadius` | `float` | Random offset from the anchor (unit sphere × radius). |
| `delay`, `randomDelayUpperBand` | `float` | Seconds between spawns, plus up to `randomDelayUpperBand` random. `delay == 0` means spawn as many as allowed each frame. |
| `randomForce`, `forceOffset` | `float`, `Vector3` | Relative force added to the new rigidbody. |
| `randScaleLowerBound`, `randScaleUpperBound` | `float` | Random scale multiplier (if either is non-zero). |
| `availableSpawnCount` | `int` | Total spawns allowed. `Start` adds `Random(0, randAdditionalSpawns)`. |
| `randAdditionalSpawns` | `int` | Random extra spawns, rolled once in `Start`. |
| `canReset` | `bool` | When the count runs out, reset the counter instead of disabling. |
| `maxConcurrentSpawns` | `int` | Max alive spawns at once (0 = unlimited). Tracked via `ISpawnable.OnDespawn`. |
| `currConcurrentSpawns` | `int` | Currently alive. Saved. |
| `disableTimer` | `float` | If nonzero, disables this spawner after the time elapses. |
| `externalCallsOnly` | `bool` | Skip the timer; only `Spawn` / `SpawnNoReturnGO` spawn. |
| `tryToSpawnInHand` | `bool` | If the spawner is under a `Usable`, put the new object in the other hand. |
| `ignoreColliders` | `Collider[]` | Collisions to ignore for each new object. |
| `spawnCondition` | `Func<bool>` | Gate. Set in code by `Player_Abilities.cs:1775` (`() => GetComponent<Usable>().player != null`). |
| `applyToObjOnSpawn` | `Action<GameObject>` | Called on each spawned object. Used by `Commons.SpawnPaintSpray`. |
| `saveId` | `int` | Save key. `SaveID` property wraps it. |
| `alreadyLoaded` | `bool` | Prevents re-initialisation on load. |

Interface: `public interface ISpawnable { event Action OnDespawn; }`. If a spawned object implements `ISpawnable`, the spawner decrements `currConcurrentSpawns` when `OnDespawn` fires.

### Methods

- `Start()`: on first run, rolls extra spawns, sets `spawnCounter`, creates `concurrentSpawnIds`, and subscribes `Save` only if the spawner is in `HouseManager._.spawners`.
- `Update()`: unless `externalCallsOnly`, ticks `timer` and spawns when `CanSpawn()`. If `disableTimer` was set, counts it down and disables the component at zero.
- `CanSpawn()`: true when `timer <= 0`, the condition holds, the concurrency or count allows, and no load happened within the last 2 s (`SaveLoadHandler.timeToLastLoad`).
- `Spawn()`: instantiates a random prefab at the next anchor position, applies scale, `applyToObjOnSpawn`, `ISpawnable` hookup, concurrency tracking (with `GenerateSaveId()` for `Usable` and `Brain` so they can be re-linked), ignores colliders, applies force, decrements `spawnCounter`, and disables the component when the count runs out (or resets if `canReset`).
- `SpawnNoReturnGO()`: calls `Spawn()` for external triggers. `Spawn()` returns the object, but `SpawnNoReturnGO` is the entry point for UnityEvents (no callers found in code).
- `GetLooseChangeVolumes()`: `[ContextMenu]` that fills `spawnAnchors` from `LooseChangeVolume` transforms in the scene.
- `Save()` / `Load(JToken)`: type `"Spawner"`. Restores `currConcurrentSpawns` and re-attaches `DecrementConcurentSpawns` to `ISpawnable` objects found by saved id in the Usable or Brain tables. Logs failures.

### Numbers

- Spawner `Update` runs spawns while `spawnCounter > 0` or under the concurrency cap when `delay == 0`.
- Save gate: no spawns within 2 s of a load.

### Used by

- `HouseManager.looseChangeSpawner`, `HouseManager.dustSpawner` (subscribed to save), `HouseManager.spawners` (list gate).
- `Commons.SpawnPaintSpray` adds a runtime `Spawner` with `spawns = Water.liquidSpheres[color]`.
- `Player_Abilities.cs:1775` sets `spawnCondition`.

---

## EncounterSpawner

Source: `decompiled/Assembly-CSharp/EncounterSpawner.cs`. `public class EncounterSpawner : MonoBehaviour, ISaveable`.

**Purpose.** Runs the nightly visitor schedule. Each night has an ordered list of `Encounter` entries. The spawner instantiates the next visitor when its delay has passed, sets a door-mat message, and (through `CharacterStories`) gives the visitor their dialogue. It also hosts an Ink `Story` (`masterStory`) built from `masterScript`.

**Singleton.** `public static EncounterSpawner _;` (`Awake`, only if null).

### Static state

- `nightId` (static int, default 1, 1..5). Used by `CharacterStories` to build knot names (`"Night" + nightId`). Reset to 1 in `Awake`, so it is reset on every scene load.
- `encounterId` (static int, default 0). 1-based index of the current encounter within the night. Reset to 0 in `Awake`. `Dialogue.EncounterIncrement` also increments it.
- `Nights` (static `Encounter[][]`), built in `Start` from the five serialized arrays.

### Serialized and instance fields

| Field | Type | Meaning |
|---|---|---|
| `Night1Encounters` … `Night5Encounters` | `Encounter[]` | Per-night encounter lists. |
| `masterScript` | `TextAsset` | Ink source; `masterStory = new Story(masterScript.text)` in `Start`. |
| `masterStory` | `Ink.Runtime.Story` | The runtime story instance. |
| `doorMat` | `Text` | Shows the greeting. |
| `encounterDelayTimer` | `float` | Counts down in `FixedUpdate`. |
| `timeToIgnored` | `float` | Set from `Encounter.ignoredTimer` on spawn. Saved. |
| `characterPresent` | `bool` | True while a visitor exists. |
| `paused` | `bool` | Stops the schedule. Saved as `disabled` in JSON. |
| `currCharInstance` | `GameObject` | The current visitor. |
| `encounterIdInspectorOnly` | `int` | Initialised from `encounterId`; not used in code. |

`Encounter` (serializable class): `character` (GameObject), `initPosition`, `initRotation` (defaults used when zero: `roadSideSpawnPoint` and `(-90, 0, 0)`), `finalRotation` (not read), `denialTriggerObj` (not read), `delayToStart` (seconds to wait before the next encounter), `ignoredTimer` (time before the visitor counts as ignored), `SpawnIfVisitedKnots` (string[], skips this encounter if any listed knot has not yet been visited), `specifiedStitch` (`OverrideKnot` on `CharacterStories`), `doorMatText` (defaults to the literal `"Walcome"` when empty, see Gotchas).

### Methods

- `Awake()`: resets `nightId` and `encounterId` to 1 and 0, and sets `_`.
- `Start()`: builds `Nights`, creates `masterStory`, calls `SetupNextEncounter()` if `encounterId == 0`, subscribes `Save`.
- `FixedUpdate()`: if not paused, no visitor is present, and `encounterId` is within range, counts down `encounterDelayTimer`. When it reaches zero (and the encounter has a character prefab), instantiates the prefab at `initPosition` (or the roadside point), hooks `CharacterStories.OnDestroyChar` to `SetupNextEncounter`, sets the door destination, `OverrideKnot`, timers, and the door-mat text.
- `SetupNextEncounter()`: clears `characterPresent`, increments `encounterId`, and returns if past the end of the night. If the encounter's `SpawnIfVisitedKnots` are not all visited, skips it (recursively). Otherwise sets `encounterDelayTimer` to the next encounter's `delayToStart`.
- `ForceNextEncounter()`: sets `encounterDelayTimer = 0.2`.
- `getNight()` / `getNight(int)`: the current or given night array. `getNight()` uses `nightId - 1`, so it throws if `Nights` has not been built yet.
- `Save()` / `Load(JToken)`: type `"EncounterSpawner"`. Stores `nightId`, `encounterId`, the visitor's brain save id (or -1), paused, present, timers, and door-mat text.

### Used by

- `BrainTrickOrTreater.cs:126`, `BrainNeetAI.cs:261, 636`, `BrainWithSegments.cs:131` call `SetupNextEncounter()`.
- `BrainWithSegments.cs:148-149` uses `GetComponents<AudioSource>()` on the spawner for a sound.
- `Dialogue.cs:363` increments `encounterId`.
- `SaveLoadHandler.cs:189` restores it.

---

## PickupBank

Source: `decompiled/Assembly-CSharp/PickupBank.cs`. `public class PickupBank : ScriptableObject` (asset menu `SOs/PickupBank`).

**Purpose.** The item registry. `pickups` is the list of prefabs. `Init()` copies them into the static `pickupsDict`, keyed by the prefab's GameObject name, which is what save/load, `Dialogue.GetSpawnable`, `SequenceManager`, and the console use.

### Fields

| Field | Type | Meaning |
|---|---|---|
| `pickupBankType` | `PickupBankType` (`All`, `Normal`, `BlackMarket`, `OccultMarket`) | Category tag. Not read in code here. |
| `pickups` | `List<Spawnable>` (serialized) | The item list. |
| `pickupsDict` | `static Dictionary<string, Spawnable>` | Global registry. Static constructor creates it. `[SerializeField]` on a static field does nothing. |
| `pickupNames` | `string[]` (private) | Built in `Init` but not read elsewhere. |

### Methods

- `Init()`: if `pickupsDict` is empty, adds every non-null `pickups` entry keyed by `obj.name`. Then builds `pickupNames`, sized to the dictionary and indexed by list index (can overrun if the list has null or duplicate entries; inferred, not tested).
- `static GetSpawnable(string name)`: `pickupsDict[name]`. Throws if absent. Used by `Dialogue.cs:278` and `BrainBank`'s console commands.
- `static GetPickups(bool visibleOnly = false)`: all values (the `visibleOnly` flag is ignored).
- **Console commands** (`IngameDebugConsole`, `[ConsoleMethod]`):
  - `get` (`SpawnPickupAtPlayer(string name)`): spawns the item about 0.2 m in front of `Camera.main`'s parent.
  - `spawnAt` (`SpawnPickupInRoom(string name, string room)`): spawns at `Locator.GetRoomCenter(ParseLocationName(room))` and adds a random force scaled by mass.
  - Both names are also used by `BrainBank` (see below). See Modding notes.

### Named prefabs the code relies on

These names must exist in `pickups` for the corresponding code paths to work: `Gib1`-`Gib3`, `Turd1`-`Turd3`, `Heart`, `Brain`, `Eyeball`, `Liver`, `Guts`, `Soul_Sphere`, `IceCube`, `Pumpkin`, `Bone_Humerus`. Save-load also needs every saved `prefabName` (`SaveLoadHandler.cs:232`).

### Used by

`HouseManager.Start` (Init, NEET_House only), `HouseManager.SpawnReaper` (`Soul_Sphere`), `SequenceManager` (`SpawnPickupAt`), `SaveLoadHandler` (`prefabName` lookup), `Cardboard_Box`, `Piggybank`, `NakedTrigger`, `SpraySphere`, `LiquidContainer`, `Spider_House`, `Dialogue`, `DreamRandomizer` (reads `pickupBank.pickups` directly and filters `Usable.price < 50` and `purchaseableAt != Nowhere`).

---

## BrainBank

Source: `decompiled/Assembly-CSharp/BrainBank.cs`. `public class BrainBank : ScriptableObject` (asset menu `SOs/BrainBank`).

**Purpose.** The NPC registry, same shape as `PickupBank`: `brains` is the list of NPC prefabs and `brainsDict` is the static lookup, keyed by GameObject name.

| Member | Meaning |
|---|---|
| `brains` | `List<Spawnable>` (serialized). |
| `brainsDict` | `static Dictionary<string, Spawnable>`. Built by `Init()`. |
| `Init()` | Adds each non-null brain whose name is not yet in the dictionary. **Unlike `PickupBank.Init`, it does not require an empty dictionary, so it merges.** |
| `static GetSpawnable(name)` | `brainsDict[name]`. Throws if absent. |
| `static Getbrains(bool)` | All values (flag ignored). Note the lowercase `b`. |
| Console `get` (`SpawnbrainAtPlayer(string name)`) | Spawns the brain in front of the camera. |
| Console `spawnAt` (`SpawnbrainInRoom(string name, string room)`) | Spawns at the room centre with a random force. |

Named brains used in code: `SkeletonNeetNPC` and `Spider_House` (`HouseManager.StatCheck`), `Thief` (`ThiefHandler.cs:41`), and whatever `DreamRandomizer.SpawnNpcs` randomly picks from `brains`. `SequenceManager` (`SpawnBrainAt`) and `Bad_Thief` (scene object) also use it.

Called from: `HouseManager.Start` (`Init`, NEET_House only).

---

## DreamRandomizer

Source: `decompiled/Assembly-CSharp/DreamRandomizer.cs`. `public class DreamRandomizer : MonoBehaviour`.

**Purpose.** Procedurally scrambles the dream level on load: random materials, random light and sky colours, random time scale, random world objects, NPCs and furniture, then ends the dream after a fixed time.

### Fields

| Field | Type | Meaning |
|---|---|---|
| `randMats` | `List<Material>` | Material pool (4 picks per run). |
| `walls`, `floors`, `ceilings`, `doors`, `furniture` | `List<GameObject>` | Scene objects to shuffle. `CollectConstructions` (context menu) fills the first four by name (`"Floor"`, `"Ceiling"`, `"Wall"`, `"Doors"`). |
| `dirLight`, `dirLight2` | `Light` | Randomised directional lights. |
| `sceneSwitcher` | `SceneSwitcher` | Ends the dream. |
| `wakeupTimer` | `float` | Counts up in `Update`. At 15 s, calls `sceneSwitcher.SwitchScene()`. |

### Behaviour

- `Start()` subscribes `OnSceneUnload` (resets `Time.timeScale` to 1) to `SceneManager.sceneUnloaded`, subscribes `PersistHeldDreamPickups` to `sceneSwitcher.OnUnload`, sets a random `Time.timeScale` (0.6-1.5), culls/offsets/rotates/squeezes construction objects, applies random materials and `TextureScroll` to 60% of renderers, randomises the skybox tint and both lights, and spawns objects, NPCs and furniture.
- `PersistHeldDreamPickups()`: for each of the first two items the player holds (`Player_Abilities.pickups[0..1]`), with a 25% chance each, calls `Commons.SetPersistOnObj(item, "NEET_House")` so it carries back to the house.
- `SpawnObjects()`: `max(0, Random(0,60) - 5)` pickups, filtered to items with `Usable.price < 50` and `purchaseableAt != StoreType.Nowhere`. Each gets a `Loot` component with 50% chance.
- `SpawnNpcs()`: `Random(0,10)` NPCs from `HouseManager._.brainBank.brains`, with `CharacterStories` stripped and `Squeezer` added to random children.
- `SpawnFurniture()`: `max(0, Random(0,60) - 5)` items from `furniture`, random scale 0.5-2.5.
- `CullAndOffsetAndRandomConstructionObjs(lists, chance=0.1)`: per object, random rotation (chance / 6 for `"Floor"` names), jitter, squeeze, and destruction (chance / 6 for floors).
- `Update()`: rotates the lights, moves the FOV with a sine, counts `wakeupTimer` up, and calls `sceneSwitcher.SwitchScene()` at 15 s.

### Numbers

- Wake-up after 15 s.
- Time scale 0.6-1.5; `fuckifierChance` 0.01-0.1 (name in code, applied as `Squeezer` chance).
- Object culling default 0.1.
- Pickups per dream: up to 54. Furniture: up to 54.
- NPCs: 0-9.

Callers: none in code. Attached to a dream-scene object.

---

## AchievementManager

Source: `decompiled/Assembly-CSharp/AchievementManager.cs`. `public class AchievementManager : MonoBehaviour`.

**Purpose.** Tracks ten counters and money stats, and grants Bronze/Silver/Gold/Platinum goals when thresholds are crossed. Completion is stored in `PlayerPrefs` as `goalName = 1` (persists across runs). The metric itself is also written to `PlayerPrefs` under the achievement's name.

**Singleton.** `public static AchievementManager _;` (`Awake`, only if null).

### Classes

- `abstract class Achievement` (non-generic base): `goals` list, plus abstract `GetGoalCompletion`, `GetGoalCount`, `RegisterOnComplete`.
- `class Achievement<T>` (`T` is `int` or `float`): `name`, `goals` (`List<Goal>`), `trackedMetric` (private), `maxAttainableLevel` (name of last goal), property `TrackedMetric` (setter writes PlayerPrefs and re-evaluates all goals).
- `class Achievement<T>.Goal`: `name`, `completionCondition` (`Predicate<T>`), `OnCompleted` (`Action<string level, bool fullyCompleted>`), `hasBeenCompleted` (int 0/1). `Eval()` fires `OnCompleted` once and writes `PlayerPrefs.SetInt(name, 1)`.

`GetLevelOutOfGoalName` maps a goal name containing `Bronze`/`Silver`/`Gold`/`Platinum` to the level string.

### Achievements

| Field | PlayerPrefs / name | Goals (threshold) | Metric incremented at |
|---|---|---|---|
| `totalPlayerDeathsAchievement` | `totalPlayerDeaths` | Bronze 10, Silver 100, Gold 500 | `Player_Abilities.cs:1834` (`++`) |
| `burnedItemsCountAchievement` | `burnedItemsCount` | Bronze 10, Silver 500, Gold 1000 | `Usable.cs:320` (`++`) |
| `foodEatenCountAchievement` | `foodEatenCount` | Bronze 100, Silver 500, Gold 1000 | `Player_Abilities.cs:1535` (`++`) |
| `turdsEatenCountAchievement` | `turdsEatenCount` | Bronze 1, Silver 15, Gold 100 | `Food.cs:559` (`++`, for `Offering.isShit`) |
| `wearMultipleMasksAchievement` | `wearMultipleMasks` (goals `wear10MasksAtOnce*`) | Bronze 5, Silver 25, Gold 50 | `MaskFace.cs:88` (sets to mask count) |
| `breathCountAchievement` | `breathCount` | Bronze 25, Silver 150, Gold 500 | `Player_Abilities.cs:738` (`++`) |
| `totalMoneyAchievement` (`float`) | `totalMoney` | Bronze 1000, Silver 100000, Gold 1000000, Platinum 100000000 | `Computer.cs:133` (sets to money value) |
| `biggestMoneyGainAchievement` (`float`) | `biggestMoneyGain` | Bronze 100, Silver 500, Gold 1000, Platinum 10000 | `Computer.cs:140` (sets to gain) |
| `pissCountAchievement` | `pissCount` | Bronze 1, Silver 10, Gold 100, Platinum 500 | `FluidGun.cs:168` (`++`, clothId 1) |
| `pukeCountAchievement` | `pukeCount` | Bronze 1, Silver 10, Gold 100, Platinum 500 | `FluidGun.cs:172` (`++`, clothId 2) |

Other members: `achievements` (`List<Achievement>`, all ten, in insertion order, used by `AchievementViewer`), `static int TryGetPlayerPrefBool(name)` (returns the stored 0/1 for a goal, used as the already-completed flag at construction).

Notes:
- The Platinum goal of `totalMoneyAchievement` reads the PlayerPrefs key `"biggestMoneyGainPlatinum"` (copy-paste). It shares a completion flag with the `biggestMoneyGain` Platinum goal.
- `GetGoalCompletion()` has a loop that uses `i++` (not `i--`) with `i >= 0`. It would throw on the first iteration past the end. No callers exist in code.

---

## AchievementViewer

Source: `decompiled/Assembly-CSharp/AchievementViewer.cs`. Lays out wing and medal prefabs in `Start` (one wing per achievement, five per row, offset by `firstMedalPos`). It registers `RegisterOnComplete` callbacks on each goal, which swap the wing for a coloured wing and place a medal. `MedalLevelToColor` maps `Bronze`/`Silver`/`Gold`/`Platinum` to indices 1-4 (0 is the default, for example "not yet earned"). Only the scene uses it.

---

## Scores

Source: `decompiled/Assembly-CSharp/Scores.cs`. `public class Scores : MonoBehaviour`.

A paint-score HUD. Every `updateSpeed` seconds (range 0.1-5, default 1) it calls `PaintTarget.TallyScoreGlobal()` and writes each player's share of `PaintTarget.scores` (a `Vector4`) as a rounded percentage into `player1`..`player4` (`UI.Text`). If the total is zero it writes `"0%"`. No code callers (scene only).

---

## SeqSpawnThiefIntro

Source: `decompiled/Assembly-CSharp/SeqSpawnThiefIntro.cs`. `public class SeqSpawnThiefIntro : TimerSequence`.

- `Awake` sets `sequenceName = "SpawnThiefIntro"`, registers itself in `SequenceManager._.timedSequences`, and subscribes `base.Save` to `SaveLoadHandler`.
- `RunSequence` (when the timer hits zero): activates `SequenceObjDict["Bad_Thief"]` and clears its `Brain.disableOnStart`; destroys `SequenceObjDict["Player_Original_Distant_Silhouette"]`; calls `ThiefHandler.BreakWindowInRoom(Location.Lounge)`; and calls `Dialogue._.SwitchSegmentLine("Grandpa_NPC", "Thief_Intro")`.

Gotcha: `Awake` reads `SequenceManager._`, which is only set when `SequenceManager.Awake` has run. If this object awakes first, it throws.

---

## Nightmare1_Sequences

Source: `decompiled/Assembly-CSharp/Nightmare1_Sequences.cs`. `public class Nightmare1_Sequences : MonoBehaviour`.

- Fields: `deskJockey`, `hiddenRoomWall`, `glasses` (GameObjects).
- `Start` adds `"DisappearAndSpawnWeapon"` to `SequenceManager._.SequenceDict` and calls `Commons.SetPersistOnObj(glasses, "NEET_House")`.
- `DisappearAndSpawnWeaponCorout` spins and shrinks `deskJockey` over about 2 s (`disappearTimer` 1 with a `0.5*dt` decrement), then deactivates `deskJockey` and `hiddenRoomWall`.

**Surprise.** Despite its name, the sequence does not spawn a weapon. No code in this file creates one.

---

## TutorialJack

Source: `decompiled/Assembly-CSharp/TutorialJack.cs`. `public class TutorialJack : MonoBehaviour, CharacterStories.IEncounterable`.

Implements the `CharacterStories.IEncounterable` interface. `TriggerExit(int)` starts a coroutine that moves the object backwards (`-forward * 0.03`) every fixed step for 2 s. `CharacterStories.cs:279-281` calls `TriggerExit()` on an encounter exit. `Start` is empty.

---

## DemoScript

Source: `decompiled/Assembly-CSharp/DemoScript.cs`. `public class DemoScript : MonoBehaviour`.

A standalone demo-scene toy. It oscillates a light bulb between x = -5 and 5, spins the first of a list of `Mirrors` and toggles `MirrorScript.MirrorRecursion` from a UI `Toggle`, and provides mouse-look (`Input.GetAxis("LookHorizontal" / "LookVertical")`, legacy input, not the InputSystem) and WASD movement (`Input.GetKey`). Key `M` toggles recursion. Not used by the house game.

---

## FPS_

Source: `decompiled/Assembly-CSharp/FPS_.cs`. `[RequireComponent(typeof(Text))]`. Smooths `1/Time.deltaTime` with `Mathf.Lerp` and writes `"<n>fps"` to its `Text` each `LateUpdate`. Debug HUD only.

---

## Feedback_Sender

Source: `decompiled/Assembly-CSharp/Feedback_Sender.cs`. `public class Feedback_Sender : MonoBehaviour`.

**Purpose.** The in-game options and feedback panel. It owns the video, audio and input settings (all persisted to `PlayerPrefs`), the quit and reset-save confirmations, the rebinding UI, and a feedback form.

**Singleton.** `public static Feedback_Sender _;` (`Awake`; a duplicate is destroyed).

### Static state

- `mouseSensitivity` (`float`, default 1). Read by `CameraControl` and `BrainPlayerHijack`. Set through `SetMouseSensitivity`.

### Key methods

- `Start()` adds click, hover and select sounds to every `Button` and `Dropdown` (with `EventTrigger`), grabs `HouseManager._.mainCamera`'s `PostProcessVolume`, fills the resolution dropdown from `Screen.resolutions`, restores settings from `PlayerPrefs` (`LoadSettingsPlayerPref`), and ends by deactivating its own GameObject (it starts hidden).
- `Toggle()` / `Toggle(bool)`: shows or hides the panel, plays a sound, sets `CanvasGroup.interactable`, and sets the EventSystem selection. `CameraControl.cs:139` calls this to open the menu.
- Quality: `Settings_QualityPreset(level)` for `QualitySettings` levels 0-2 (3 = custom). `Settings_VerticalSync`, `Settings_TextureQuality`, `Settings_ShadowResolution`, `Settings_AntiAliasing` (`level*2`, max 8), `Settings_AnisotropicFiltering`. Any direct change moves the preset to 3 (custom).
- Display: `Set_Resolution(index)` with a 10 s "KEEP RESOLUTION?" prompt that reverts unless confirmed (`ConfirmKeepResolution`). `SwitchFullScreenMode` (`FullScreenWindow` or `Windowed`). `Set_Framerate` sets `Application.targetFrameRate`.
- Post-processing: `SetBrightness` (color grading brightness × 100), `ToggleBloom`.
- Audio: `SetVolumeMaster` writes `AudioListener.volume`.
- Input: `OpenRebindingMenu` loads `PlayerPrefs["rebinds"]` into `actions` (the `Input_Asset`), and disables `HouseManager._.inputAsset` while it is open. `CloseRebindingMenu` saves the overrides as JSON to `PlayerPrefs["rebinds"]` and re-enables the asset.
- `SendFeedback()` POSTs to a Google Forms URL with the feedback type and text, the player's position, and system info (OS, CPU, GPU, RAM, playtime, resolution). `postRequest` has a cooldown of 3 s by default, 5 s after a send. Note this is an outbound network call to a third-party service.
- `ResetSave(bool)`: two-step; with `true`, deletes every file in `Application.persistentDataPath` whose name starts with `"neet"` and deletes the `Textures` folder.
- `Quit(bool)`, `LoadMainMenu()` (`SceneManager.LoadScene("MainMenu")`, directly, not through the Loading screen).

### PlayerPrefs keys

`Framerate`, `QualityPreset`, `VerticalSync`, `TextureQuality`, `ShadowResolution`, `AntiAliasing`, `AnisotropicFiltering`, `Fullscreen`, `Resolution`, `Brightness`, `Bloom`, `MouseSensitivity`, `VolumeMaster`, `rebinds`. Also `neet*` files on disk for saves.

### Used by

`Feedback_Sender._` in `BrainRat`, `BrainPlayerHijack`, `CameraControl`. `mouseSensitivity` in `BrainPlayerHijack` and `CameraControl`.

---

## GetBuildScenes

Source: `decompiled/Assembly-CSharp/GetBuildScenes.cs`. `[ExecuteInEditMode] [RequireComponent(typeof(Dropdown))]`. `FindAllBuildScenes()` is an empty method, so it fills nothing. Intended to list scenes for `SceneSwitch`. Not functional as decompiled.

---

## SceneSwitch

Source: `decompiled/Assembly-CSharp/SceneSwitch.cs`. A persistent (`DontDestroyOnLoad`) singleton for a scene-picker `Dropdown`. `SwitchScene(int buildIndex)` calls `SceneManager.LoadSceneAsync(val)` if it is different from the current scene. In `Update`, **pressing Escape quits the application**. No code callers. Only applies if the component is in a scene.

---

## MyCanvasSetting

Source: `decompiled/Assembly-CSharp/MyCanvasSetting.cs`. Subscribes an empty `OnLevelFinishedLoading` to `SceneManager.sceneLoaded`. `mySetting()` would set every `CanvasScaler` to `ScaleWithScreenSize`, but is never called. Effectively a no-op.

---

## StandaloneInputOverride

Source: `decompiled/Assembly-CSharp/StandaloneInputOverride.cs`. Subclass of `StandaloneInputModule`. Overrides `GetMousePointerEventData`, `ProcessMove`, and `ProcessDrag` to temporarily set `Cursor.lockState` to `None` while the base runs, so UI pointer events work when the mouse is locked. `Player_Abilities.cs:1944-1990` enables and disables the component on the camera.

---

## Console

Source: `decompiled/Assembly-CSharp/Console.cs`. An empty `MonoBehaviour` (`Start`/`Update` empty). It shares a name with nothing else in this assembly, but is unused. The actual debug console is the `IngameDebugConsole` package (see PickupBank and BrainBank).

---

## ReadOnlyAttribute

Source: `decompiled/Assembly-CSharp/ReadOnlyAttribute.cs`. `public class ReadOnlyAttribute : PropertyAttribute {}`. A marker only. No `PropertyDrawer` exists in the decompiled code, so there is no runtime effect.

---

## Input_Asset

Source: `decompiled/Assembly-CSharp/Input_Asset.cs`. Generated wrapper around an `InputActionAsset` named `Input_Asset`, created by the Unity InputSystem code generator. It exposes `PlayerActions` and `UIActions` structs, `FindAction`, `Enable`/`Disable`, and callback interfaces. The asset JSON is embedded as a string in the constructor (around line 456). Used by `HouseManager.inputAsset` and `Feedback_Sender.actions`.

### Action maps

**Player** (19 actions): `Move` (Value, Vector2), `Look` (Value, Vector2), `Left_Hand`, `Right_Hand`, `Pickup_Rotation`, `Sprint`, `Crouch`, `Use`, `Drop`, `Eyelid_Menu`, `Exit`, `Lock_View`, `Hunger`, `Bladder`, `Hygiene`, `Energy`, `Breath`, `Submit` (all Button), and `Zoom_Pickup` (Value, Vector2).

**UI** (1 action): `New action` (Value, Vector2, bound to `<Gamepad>/dpad`). Template leftover.

**Control schemes**: `Keyboard&Mouse`, `Gamepad`, `XR`.

### Default bindings (from the embedded JSON)

| Action | Keyboard & Mouse | Gamepad | XR |
|---|---|---|---|
| Move | W/A/S/D | left stick, D-pad (mode 1) | Primary2DAxis |
| Look | mouse delta (scale 0.1) | right stick (scale 6) | none |
| Left_Hand | left mouse | left trigger | PrimaryAction |
| Right_Hand | right mouse | right trigger | PrimaryAction |
| Pickup_Rotation | middle mouse | West button | PrimaryAction |
| Sprint | Shift | left stick press | none |
| Crouch | Ctrl | right stick press | none |
| Zoom_Pickup | mouse scroll (scale 0.005) | North / South buttons | none |
| Use | E | right shoulder | none |
| Drop | Q | left shoulder | none |
| Eyelid_Menu | Space | Select | none |
| Exit | Escape | Start | none |
| Lock_View | Tab | North button | none |
| Hunger | 1 | D-pad up | none |
| Bladder | 2 | D-pad right | none |
| Hygiene | 3 | D-pad down | none |
| Energy | 4 | D-pad left | none |
| Breath | B | East button | none |
| Submit | Enter | South button | none |

Rebinding overrides are stored in `PlayerPrefs["rebinds"]` by `Feedback_Sender`.

---

## Modding notes

### Good Harmony patch targets

- **`SequenceManager.TriggerSequence(string funcName, string arg)`.** Ink dialogue calls every scripted event through here. A prefix can intercept custom verbs before the `switch` (return `false` to skip the default, which only logs). This is the cleanest way to add new scripted verbs without touching Ink.
- **`HouseManager.Start()`** (postfix). Runs once per `NEET_House` load, after `Awake`, and is where the thunder, bell, pickup and brain banks, and the save subscriptions get set up. Good for adding registries or subscribing to `SaveLoadHandler`. Remember it runs only for `NEET_House`.
- **`HouseManager.CreateAndSetNewPlayer(...)`** (postfix). Fires on every respawn after the player is rebuilt. Good place to re-apply mod state to the new player.
- **`HouseManager.GetValidSpawnPointForBrain(bool)`** (prefix/postfix). Central place to change where brains spawn (for example, to remove the default exclusions or add new rooms).
- **`HouseManager.StatCheck`** is generic and private, so patch with care. A cleaner hook is to subscribe to `HouseManager.OnLightningStrike` or the public counters.
- **`HouseManager.SpawnCopVan()`**, **`QueueCorpseReaping`** and **`SpawnReaper`** are good points to add side effects to police or death events.
- **`Spawner.Spawn()`** (postfix) sees every spawn, and the object it returns can be modified. `Spawner.CanSpawn()` (prefix/postfix) can gate spawning without changing the spawner's data.
- **`EventConnector.ExternalExecute(...)` / `ExternalExecuteInverse(...)`** let a mod fire every `External` entry of a connector.
- **`EncounterSpawner.SetupNextEncounter()`** (postfix) for custom encounter flow. `EncounterSpawner.Start` builds `Nights` from the serialized arrays, so patch `Start` (prefix) to change nights before the array is captured.
- **`Feedback_Sender.SendFeedback()`** / `postRequest` if you want to stop or redirect the outbound feedback form.
- **`Feedback_Sender.ResetSave(bool)`**: a reset path that deletes `neet*` files. Be aware before testing.
- **`LoadSceneOnDeath.SwitchScene`** / `Dialogue.AddPlayerSceneSwitchOnDeath`, if you want custom death-scene routing.
- **`Locator.Awake`** (postfix) if you want to add a room or change the room key. Patch `Locator.LocatePoint` if you need a different "which room" answer.
- **`AchievementManager.Awake`** (postfix) is where achievements are constructed. You can append `Achievement<T>` objects to `achievements`. **`AchievementViewer.Start`** builds the viewer from that list.
- **`PickupBank.Init`** / **`BrainBank.Init`** (postfix) for adding content to the registries (see below).

### Static registries and lists a mod can add to

- **`PickupBank.pickupsDict`** (`Dictionary<string, Spawnable>`, static). Key = prefab GameObject name. Add an entry with `new Spawnable(prefab)` and it becomes available to `GetSpawnable`, `SaveLoadHandler` (`prefabName`), the `get`/`spawnAt` console commands, `Dialogue.GetSpawnable`, and `SequenceManager` `SpawnPickupAt`. Note: `Init()` only fills it when it is empty, so add after `Init`, or add to `pickups` and call `Init` yourself.
- **`PickupBank.pickups`** (`List<Spawnable>`, on the ScriptableObject). Serialized. Changing it at runtime works only if `Init` has not run yet for this session. It is also what `DreamRandomizer.SpawnObjects` reads to choose random loot.
- **`BrainBank.brainsDict`** (`Dictionary<string, Spawnable>`, static). `Init()` merges, so you can add entries after it runs. Used by `HouseManager.StatCheck`, `ThiefHandler`, `SequenceManager`, and the console.
- **`BrainBank.brains`** (`List<Spawnable>`). `DreamRandomizer.SpawnNpcs` picks uniformly from this list.
- **`Locator.rooms`** (`Dictionary<Location, GameObject>`, static). Each room GameObject with a `Locator` registers itself. Adding a room means adding a new `Location` value, which is unsafe (see the next section).
- **`SequenceManager._.SequenceDict`** (`Dictionary<string, Action<string>>`). Named callbacks for the `Execute` verb. The existing ones are `"Thunder"` and `"DisappearAndSpawnWeapon"`. A mod can add its own key and then have Ink call `TriggerSequence("Execute", "MyKey")`.
- **`SequenceManager._.SequenceObjDict`** (`Dictionary<string, GameObject>`). Scene objects addressed by name. Add entries to make objects scriptable.
- **`SequenceManager._.timedSequences`** (`Dictionary<string, TimerSequence>`). Subclass `TimerSequence`, set `sequenceName`, and add yourself here (the same pattern as `SeqSpawnThiefIntro`).
- **`EventConnector` delegates** (`colEnterDeleg`, `triggerEnterDeleg`, `colExitDeleg`, `triggerExitDeleg`) and the `Flame.OnIgnited` / `OnExtinguished` events, which are subscribed on the same GameObject.
- **`HouseManager.OnLightningStrike`** (public event). Subscribe for lightning-driven effects. `LightSwitch` already does.
- **`HouseManager.statusEffectDict`** (`Dictionary<string, StatusEffect>`, built from `statusEffects` in `Awake`). Add new status effects with their name as key.
- **`HouseManager.characterStoriesDict`** (`Dictionary<string, CharacterStories>`). Keyed by character name. Used by `Dialogue.SetOverrideKnot`.
- **`AchievementManager.achievements`** (`List<Achievement>`). Append in `Awake` (postfix).
- **`SaveLoadHandler.SubscribeSave(Func<SaveData>)`**. This is the official extension point for persisting custom data. Subscribe a function that returns a `SaveData` subclass (see `EventConnector.SaveDataEventConnector`, `SequenceManager.SaveDataSequenceManager` for the pattern).
- **`Feedback_Sender.mouseSensitivity`** (static float). Read by camera code each frame, so setting it at runtime works.
- **`SceneLoadingData.sceneNameToLoad`** (static string). Set it, then load `"Loading"` to route to any build scene.
- **`EncounterSpawner.Nights`** (`Encounter[][]`, static). Built from the five serialized arrays in `Start`. To add a visitor, patch `Start` or append to the arrays before `Start` runs.
- **`EncounterSpawner.nightId` / `encounterId`** (static ints). Direct control over the schedule. `nightId` is also part of `CharacterStories` knot names.
- **`PaintTarget.scores`** is read by `Scores`. Not in this area, but the same HUD reads it.

### Singletons to grab

| Singleton | Access | Notes |
|---|---|---|
| `HouseManager` | `HouseManager._` | Player, camera, banks, lists, input actions. Null before `Awake`. |
| `SequenceManager` | `SequenceManager._` | Set in `Awake`. Used by all scripted scenes. |
| `EncounterSpawner` | `EncounterSpawner._` | Set in `Awake`. Also has static `nightId`/`encounterId`. |
| `AchievementManager` | `AchievementManager._` | Set in `Awake`. |
| `Feedback_Sender` | `Feedback_Sender._` | Settings panel. Created and then deactivated. |
| `SaveLoadHandler` | `SaveLoadHandler._` | Not in this area, but needed for `SubscribeSave` and `SaveAll`. |
| `Dialogue` | `Dialogue._` | Holds `masterStory`, used by `SequenceManager.Start` and `HouseManager.CreateAndSetNewPlayer`. |
| `PickupBank`, `BrainBank` | `HouseManager._.pickupBank`, `HouseManager._.brainBank` | ScriptableObjects. The static dictionaries are the lookup. |
| Player | `HouseManager._.player` | Has `Player_Abilities`, `PlayerStats`, `Brain` (`currentLocation`). |

### Gotchas

- **`Location` enum values are persisted.** `SaveLoadHandler` reads `currentLocation` as an int. Reordering `Location` or inserting values in the middle changes meaning for existing saves. Append new rooms at the end, after `Dream` (22), and check every `Location` use in saves.
- **`Locator.GetRandomRoom()` (no args) is broken** (it casts a random index to `Location`). Use `GetRandomRoom(Location[])` or `GetRandomRoomObj()` instead.
- **Registry population is scene-dependent.** `PickupBank.Init()` and `BrainBank.Init()` run only from the `NEET_House` branch of `HouseManager.Start`. If a mod or a scene uses `pickupsDict` or `brainsDict` before that, lookups throw `KeyNotFoundException`. Call `Init()` yourself or populate the dictionary directly.
- **`PickupBank.Init()` only fills an empty dictionary,** so a second `Init` does nothing. `BrainBank.Init()` merges and skips duplicate names.
- **Console command names collide.** `PickupBank` and `BrainBank` both define `[ConsoleMethod("get", ...)]` with `(string)` and `[ConsoleMethod("spawnAt", ...)]` with `(string, string)`. Which one the console uses is up to IngameDebugConsole (not verified here). Expect `get` and `spawnAt` to be ambiguous.
- **`HouseManager.queuedCorpsesToReap` is only created in `Start` in `NEET_House`.** `QueueCorpseReaping` adds to it without a null check, so calling it in other scenes can throw.
- **`HouseManager.Awake` destroys duplicates with `Destroy(this)`** (the component), not the GameObject.
- **`HouseManager.Start` uses `SequenceManager._` without a null check.** Scene load order decides whether `SequenceManager.Awake` has run first. The same applies to `SeqSpawnThiefIntro.Awake`, which reads `SequenceManager._` directly.
- **`SequenceManager.Awake`** adds names to `SequenceObjDict` with `Dictionary.Add`. Two objects with the same `name` throw.
- **`SequenceManager.Load`** rebuilds `SequenceObjDict` without a null check, so a destroyed entry in `SequenceObjs` can throw during load.
- **`SpawnBrainAt` rotation bug.** The rotation for `SpawnBrainAt` is read from the position string. Any Ink that uses it gets rotation = position. Use `SpawnPickupAt` (correct) or patch the verb.
- **`Spawner.Spawn` uses `Random.Range(0, spawns.Length)`,** which is correct for int arrays, but a null entry in `spawns` will throw on `Instantiate`.
- **`Spawner` only saves when registered.** A spawner is saved only if it is listed in `HouseManager.spawners` at `Start`. Add new spawners to that list (in the scene or from code) if you need persistence.
- **`EventConnector` naming trap.** `SetKinematic` sets `isKinematic = false`, the reverse of its name.
- **`EncounterSpawner.Awake` resets `nightId` and `encounterId` every load.** If you want a mod to carry progress across scene loads, store it yourself and restore it in `Start` (or subscribe to `SaveLoadHandler`).
- **Default door mat is the literal `"Walcome"`** (`EncounterSpawner.cs:185`). Likely a typo, but it is what the game shows when `doorMatText` is empty.
- **`Church_Bell` does not play sound.** It only waits 300-420 s and returns. The bell audio is `church_Bell`, which nothing plays here.
- **`DreamRandomizer` changes `Time.timeScale` and resets it on unload.** Other systems may depend on the scale (for example `WaitForSeconds` durations).
- **`RandomGO()`** on `GameObject[]` never returns the last element.
- **`ExtensionMethods.AddParentConstraint`** sets the position and rotation offsets twice. The second call overwrites the first, so the first has no effect.
- **`AchievementManager.totalMoneyAchievement`'s Platinum goal** shares its completion key with `biggestMoneyGain` Platinum.
- **Achievement metrics are PlayerPrefs.** Resetting an achievement means clearing the `PlayerPrefs` keys (`totalPlayerDeaths`, `totalPlayerDeathsBronze`, and so on).
- **`Feedback_Sender` sends telemetry.** `SendFeedback` posts the player's position and system details to an external Google Form. This is worth knowing before you ship a mod that changes that code.
- **`Feedback_Sender.ResetSave`** deletes `neet*` files in `persistentDataPath` and the `Textures` folder. A mod's own save files should not start with `neet`, or they will be wiped by this.
- **Hard-coded resource and object names** that code depends on. These names must match or the code path fails. Resources: `DarkFogParticle`, `WaterRainbow`, `CopVan`, `FireSpreader`, `Particle_BloodBurst`, `Audio/CorpseExplosion`, `Audio/Meat_Squish`. Pickups: see the PickupBank list. Brains: `SkeletonNeetNPC`, `Spider_House`, `Thief`. Scene objects: `Bad_Thief`, `Player_Original_Distant_Silhouette`, and the `"Floor"`/`"Ceiling"`/`"Wall"`/`"Doors"` name substrings used by `DreamRandomizer.CollectConstructions`.
- **Scene names used in code:** `NEET_House` (house-only logic, persistence target), `Loading`, `MainMenu`. The dream scene name is not in code (see Open questions).

### Adding things

- **New item.** Add a prefab with a `Usable` (price and `purchaseableAt` matter for `DreamRandomizer` and shops), put it in `PickupBank.pickups` (or add to `pickupsDict` at runtime after `Init`), and make sure its prefab name is unique. Save/load finds it by name.
- **New NPC (brain).** Add a prefab to `BrainBank.brains` and `brainsDict` (merge-safe). To spawn it on a stat check you would need a new `StatCheck` call (`HouseManager.Update` is the place) or a patch.
- **New event.** Subclass `TimerSequence` (set `sequenceName`, register in `SequenceManager._.timedSequences`, implement `RunSequence`), or add an entry to `SequenceDict` and call it from Ink with the `Execute` verb, or add a new verb in `SequenceManager.TriggerSequence` via a prefix patch.
- **New visitor (encounter).** Append an `Encounter` to the relevant night (or patch `Nights`). Set `character` (a prefab with `CharacterStories`), `delayToStart`, `ignoredTimer`, and optionally `specifiedStitch` (the Ink knot).
- **New scene.** Add to build settings, then route to it with `SceneLoadingData.sceneNameToLoad` plus `SceneManager.LoadScene("Loading")`. Scene-specific managers are scene objects, so you will need to add `HouseManager`-style setup or your own `Start` for registries.
- **New room.** Requires a `Location` value (append it), a `Locator` on a room GameObject with a `BoxCollider` and `roomId`, and the room's collider must exist in the scene so `LocatePoint` and `GetRandomPositionInRoom` work. Save compatibility: appended values are safe.
- **New achievement.** Construct an `Achievement<T>` in an `Awake` postfix, add its goals, and add it to `AchievementManager._.achievements`. Increment its `TrackedMetric` from your code.
- **New save data.** Subscribe to `SaveLoadHandler._.SubscribeSave` and make your `SaveData` subclass serialisable. For objects that are referenced by id, follow `Spawner`/`EventConnector` (`saveId`, `ISaveableObj`).

---

## Open questions

These depend on scene or prefab data, or on the Ink dialogue, which is not in the decompiled code.

1. **Scene contents and build order.** The build index order for `MainMenu`, `Tutorial`, `NEET_House`, `Loading`, `DreamZone`, `Nightmare1` is unknown. Which scene holds `DreamRandomizer` and `Nightmare1_Sequences` is inferred from names only.
2. **Which `Location` rooms exist in each scene.** `Locator.rooms` is filled per scene. Whether `Dream` and `Shed` have `Locator`s, and which rooms have `BoxCollider`s, is scene data.
3. **Inspector values on `HouseManager`.** Whether `performStatChecks` is enabled, the state of `noThunder`/`noChurchBell`/`noWaterDrying`, the contents of `spawners`, `paintTargets`, `statusEffects`, `doors`, `lightSwitches`, and `mouseHoles`.
4. **`PickupBank` and `BrainBank` assets.** The contents of `pickups` and `brains`, and whether the `pickupBankType` values are used anywhere (no code reads them in this area).
5. **Ink script (`masterScript`).** Which `TriggerSequence` verbs and arguments the story actually calls, which knot names exist (`Thief_Intro`, `Grandpa_NPC`, per-night knot naming), and whether `Execute` is used. The external function binding is in code, the calls are not.
6. **`EncounterSpawner` data.** The visitor prefabs, `delayToStart`/`ignoredTimer` values, `SpawnIfVisitedKnots`, and `specifiedStitch` values for all five nights.
7. **`SpawnNoReturnGO` and `Spawner` callers.** No code calls `SpawnNoReturnGO`. It is probably wired from a UnityEvent or Animator in a prefab.
8. **`EventConnector.useEvent` wiring.** The UnityEvent targets are in the prefab/scene.
9. **`ReadOnlyAttribute`.** Whether a `PropertyDrawer` exists in an editor assembly that is not in the decompiled output.
10. **`IngameDebugConsole` command resolution.** Which of the duplicate `get` and `spawnAt` commands the console actually runs, and whether both are registered.
11. **`Feedback_Sender` UI hierarchy.** Which `Dropdown`, `Toggle`, `Slider` and `Button` references are assigned, and whether `keepResolutionPrompt` and `quitPrompt` exist in the scene.
12. **`StandaloneInputOverride` and `ClosedEyeTrigger` placement.** Which camera GameObjects carry them.
13. **`LooseChangeVolume` and dust spawn anchors.** Positions and count.
14. **`DemoScript` scene.** Whether it lives in a demo scene that is not in the build list.
15. **`Dream` location and `DreamZone` scene.** The mapping between `Location.Dream` and the `DreamZone` scene name is inferred. Confirm by inspecting the scene.
16. **`Nightmare1` scene.** The `Nightmare1_Sequences` object references (`deskJockey`, `hiddenRoomWall`, `glasses`) and whether the weapon-spawn step was removed or never implemented.
17. **`Loading` scene.** Whether it has a `SceneLoader` and a `MeshRenderer` with a face texture target.
18. **Save and load ordering.** Exactly when `SaveLoadHandler.LoadAll` runs relative to the `Start` methods of these components (covered in `SaveLoadHandler`, not verified here).
19. **Feedback form ownership.** The Google Form URL in `Feedback_Sender.SendFeedback` belongs to the original developer. Whether it still accepts submissions is unknown.
