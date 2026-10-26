# NEET Demo: Save system

Scope: everything below comes from reading the decompiled C# under `decompiled/Assembly-CSharp/`. Nothing here was tested in-game. Where a conclusion is an inference from the code rather than something directly visible, it is marked **(inferred)**. Line numbers refer to the files under `decompiled/Assembly-CSharp/`.

Core files: `SaveLoadHandler.cs` (orchestration), `SaveData.cs` (base class), `ISaveable.cs`, `ISaveableObj.cs`, `ISaveableStatic.cs`, `ISaveableUsable.cs`.

---

## 1. Where saves live

| Item | Value | Source |
|---|---|---|
| Save directory | `Application.persistentDataPath` (Unity's per-user data dir; on Windows typically `%USERPROFILE%\AppData\LocalLow\<company>\<product>`, not verified here) | `SaveLoadHandler.cs:115` |
| Save file name | `"neetSave_" + DateTime.Now.ToString()` with spaces, `/` and `:` removed, plus `.json`. Culture-dependent. For en-US this gives names like `neetSave_1026202630405PM.json` (derived by hand, not observed) | `SaveLoadHandler.cs:113-114` |
| Splat PNG folder | `persistentDataPath/Textures/PaintTargetSplats/<saveId>.png` | `SaveLoadHandler.cs:118, 127`; `PaintTarget.cs:89-94` |
| Which file is loaded | Any file in `persistentDataPath` whose name starts with `"neet"` (first 4 chars, not extension-checked), ordered by `LastWriteTime` descending, first one wins. The file name timestamp is irrelevant. | `SaveLoadHandler.cs:145-162` |
| Old saves | Never deleted by `SaveAll`. They accumulate. Only the "reset save" UI deletes them. | `Feedback_Sender.cs:538-562` |
| Reset save | Deletes every file whose name starts with `"neet"`, and the whole `persistentDataPath/Textures` folder | `Feedback_Sender.cs:538-562` |

Failure note: `f.Name.Substring(0, 4)` throws `ArgumentOutOfRangeException` for any file in `persistentDataPath` whose name is shorter than 4 characters. The loader scan at `SaveLoadHandler.cs:146` and `:160` and the reset scan at `Feedback_Sender.cs:543` all do this with no guard. Any file with a short name in that folder breaks load.

---

## 2. JSON layout

Root object: `{ "Save": [ ... ] }`. Each array element is one `SaveData` (or subclass) serialized by Newtonsoft with default public-field serialization.

Every element has:
- `"type"`: a string used by the loader to route the element (see section 9).
- `"subSaveData"`: an array of nested SaveData objects (may be empty or null), filled from child components.
- The subclass's public fields (see section 12 for the full list).

**Save[0] is special.** `AllSaveDataToJSon` writes two extra properties onto `jObject["Save"][0]` (`SaveLoadHandler.cs:101-102`):
- `"mainStory"`: `Dialogue._.masterStory.state.ToJson()` (Ink story state, parsed back as a JObject).
- `"maxIdSequence"`: `Mathf.Max(maxStaticSaveId, idSequence)`.

Load reads them back from index 0 only (`SaveLoadHandler.cs:163` for `maxIdSequence`, `:188` for `mainStory`).

What "index 0" means: `Save[0]` is the first non-null entry produced by the `OnSave` invocation list, which is subscription order. It is **not** a fixed type. If the list is empty, `jObject["Save"][0]` throws a `NullReferenceException` at line 101 and no file is written.

Abbreviated shape:

```json
{
  "Save": [
    { "type": "Usable", "saveId": 1204, "prefabName": "SomeItem", "subSaveData": [ { "type": "Food", "...": "..." } ],
      "mainStory": { "...Ink state..." }, "maxIdSequence": 2310 },
    { "type": "Brain", "basicBrainData": { "type": "BrainCat", "saveId": 17, "...": "..." }, "subSaveData": [] },
    { "type": "Door", "saveId": 3, "isClosed": true, "...": "..." }
  ]
}
```

---

## 3. Auto-save and manual saves

**Auto-save** (`SaveLoadHandler.cs:77-85`, `Update`):
- Timer: `timeToLastSave` starts at 300 s (`SaveFrequency`, line 15; set in `Awake` line 58; reset to 300 when firing, line 82).
- Fires when all three hold (line 80):
  - `timeToLastSave < 0f`
  - `timeToLastLoad <= 0f`
  - the player's `Brain.currentLocation == Location.Bedroom_Neet`
- The timer is not reset when the condition is false, so the save fires on the first frame the room condition becomes true.

**Important: `timeToLastLoad` is never reset to 0.** It is only assigned in `LoadAll` (`SaveLoadHandler.cs:151`, `timeToLastLoad = Time.time`). `LoadAll` only sets it when a `neet*` file exists at startup. Once that happens, `timeToLastLoad <= 0f` is false for the rest of the session, so **auto-save is disabled for that session** (as decompiled; not verified in play). A session that starts with no save file keeps auto-save working. Other code uses `timeToLastLoad` as a "just loaded" window or "no load happened" check: `Spawner.cs:274`, `DeliveryGuy.cs:53`, `Toilet_Flush.cs:107`, `RefrigerationZone.cs:54, 76`.

**Other `SaveAll` callers**:

| Caller | Line | When |
|---|---|---|
| `LoadSceneOnDeath.SwitchScene` | `LoadSceneOnDeath.cs:17` | Player death (subscribed to `Player_Abilities.OnPreDeath` at `LoadSceneOnDeath.cs:12`), then `LoadScene(sceneNameToLoad)` |
| `Player_Movement` | `Player_Movement.cs:315` | Dream/sleep: first save once `dreamTimer > 5` |
| `Player_Movement` | `Player_Movement.cs:324` | Dream: saves again when `dreamTimer > 20`, then `LoadScene("DreamZone")` (line 325) |

---

## 4. SaveAll step by step

`SaveLoadHandler.cs:106-123`:

1. `OnPreSave?.Invoke()` (line 109).
2. `AllSaveDataToJSon()` (lines 87-104):
   - Invokes every `Func<SaveData>` in the private `OnSave` delegate chain via `DynamicInvoke` (line 93), in invocation order.
   - Adds each non-null result to a list.
   - No try/catch. **If any subscribed `Save()` throws, the whole save aborts and no file is written.** (`OnPreSave` has already run.)
   - Builds `JObject["Save"] = JArray.FromObject(list)`, then writes `mainStory` and `maxIdSequence` onto `[0]`.
3. Builds the file name, calls `MonoBehaviour.print(path)`, then `File.WriteAllText(path, jObject.ToString(Formatting.Indented))` (lines 113-117).
4. `SavePaintSplats(persistentDataPath)` (line 118, body 125-141): deletes all existing files in `Textures/PaintTargetSplats`, then for each `HouseManager._.paintTargets` entry with `setupComplete`, calls `PaintTarget.SaveSplat` which writes `<saveId>.png` (`PaintTarget.cs:89-94`).
5. `OnPostSave?.Invoke()` (lines 119-122).

---

## 5. LoadAll(): full order

`LoadAll` is public (`SaveLoadHandler.cs:143`). Its only call site in the decompiled code is `SaveLoadHandler.Start()` (line 73). `Start()` then creates `spawnAfterLoad` (line 74). `spawnAfterLoad` is therefore still null during the first `LoadAll`, and the drain loop at line 194 is guarded by `spawnAfterLoad != null`.

| # | Lines | Step | Notes / what fails if the expected data is missing |
|---|---|---|---|
| 0 | 145-150 | Scan `persistentDataPath`. If no file starts with `neet`, log and return. | Nothing loads. `timeToLastLoad` is not set. |
| 1 | 151 | `timeToLastLoad = Time.time` | Disables auto-save for the session (section 3). |
| 2 | 152-155 | `OnPreLoad?.Invoke()` | Hook. |
| 3 | 156-158 | Reset `loadedUsablesById`, `loadedBrainsById`, `secondPassNeeders`. | |
| 4 | 159-162 | Parse newest `neet*` file (`File.ReadAllText`, `JObject.Parse`). | Parse error aborts. |
| 5 | 163 | `idSequence = Save[0].maxIdSequence` | Missing field throws. |
| 6 | 164 | `LoadUsables(type "Usable")` | Section 6. Destroys scene copies first, then recreates from save. |
| 7 | 165 | `LoadBrains(type "Brain")` | Section 7. Runs **after** usables, so `loadedBrainsById` is empty during step 6. |
| 8 | 166-174 | `LoadStatic<>` for: Door, LightSwitch, SinkFaucet, GasButton, Sit_Enabler, Rest_Enabler, EventConnector, Spawner (`ignoreIdZero`), SmokeDetector (`ignoreIdZero`) | Section 8. Each token is matched to a scene object by `SaveID`. |
| 9 | 175 | `HouseManager._.tv.Load(Save.Where(type=="TV")[0])` | `[0]` throws if absent. |
| 10 | 176 | `looseChangeSpawner.Load(Save.Where(type=="Spawner")[0])` | **Index-based.** See note below. |
| 11 | 177 | `dustSpawner.Load(Save.Where(type=="Spawner")[1])` | Needs at least 2 Spawner entries. |
| 12 | 178 | `pickupPackageArea.Load(type "PickupPackageArea"[0])` | `[0]` throws if absent. |
| 13 | 179 | `fuseBox_Manager.LoadAllFuseSlots(type "FuseBox_Slot" list)` | Empty list is fine. |
| 14 | 180 | `LoadComputer(jObject)` (lines 374-403) | `Computer_PowerButton[0]` and `StockMarket[0]` throw if absent. `AnimeCardGenerator` and `AppDownloader` are reset if absent. `EmailHandler` is skipped if absent. |
| 15 | 181 | `LoadSecondPass()` | Section 10. |
| 16 | 182 | `LoadPaintTargets()` | Section 11. |
| 17 | 183 | `LoadFireSpreaders(type "FireSpreader")` | Clears rooms' fire spreaders, then starts a fire per saved token. |
| 18 | 184 | `SequenceManager._.Load(type "SequenceManager"[0])` | `[0]` throws if absent. |
| 19 | 185 | `LoadTimerSequences(type "TimerSequence")` | Looks up `SequenceManager._.timedSequences[sequenceName]`, which throws if missing. |
| 20 | 186 | `TaxHandler.Load(type "TaxHandler"[0])` | `[0]` throws if absent. |
| 21 | 187 | `Dialogue._.Load(type "Dialogue"[0])` | `[0]` throws if absent. |
| 22 | 188 | `Dialogue._.masterStory.state.LoadJson(Save[0].mainStory)` | Missing `mainStory` throws. |
| 23 | 189 | `EncounterSpawner._.Load(type "EncounterSpawner"[0])` | `[0]` throws if absent. |
| 24 | 190-193 | `OnPostLoad?.Invoke()` | Hook. |
| 25 | 194-202 | Drain `spawnAfterLoad` stack: `Instantiate(obj, pos, identity)`, then the optional callback. | Used by `Commons.SetPersistOnObj` (`Commons.cs:257-269`). |
| 26 | 203-204 | `Resources.UnloadUnusedAssets(); GC.Collect();` | |

Note on step 10-11 **(inferred)**: the two singleton Spawners are chosen by position in the JSON array, and that order follows `OnSave` subscription order, which depends on `Start()` order. The code does not guarantee that index 0 is the looseChange spawner. The static-spawner list at line 173 excludes `saveId == 0` entries (`ignoreIdZero`), which suggests the two singletons are the id-0 ones, but this was not confirmed.

Note on `OnPreLoad` / `OnPostLoad`: these are `SaveLoadHandler` public Actions (`SaveLoadHandler.cs:33, 35`). The same names also exist on `EmailHandler` (`EmailHandler.cs:50, 52`) and `StockMarket` (`StockMarket.cs:29, 31`), which invoke their own copies from their own `Load` methods, not from `LoadAll`.

---

## 6. Usables: LoadUsables (lines 212-300)

1. **Pre-clean (214-221):** every `ISaveableUsable` scene object whose `Usable.prefabName != ""` and that is not a lever/button is destroyed. Scene copies of saved items are replaced by the save's copies.
2. **Order (222):** `tokens.OrderByDescending(t => t["prefabName"] == "Fuse")`. Fuse entries first, then the rest in file order.
3. **Dedup (226, 295-298):** skip if `loadedUsablesById` already has the `saveId`. Logs "Duplicate usable".
4. **Recreate (232):** `UnityEngine.Object.Instantiate(PickupBank.pickupsDict[saveDataUsable.prefabName].obj, savedPos, savedRot)`. This is the **prefab lookup by name** (section 9b).
5. Then:
   - `saveId` is set from the file (233) **before** `Start()` (234), so the id is preserved.
   - Scale, `name = instanceName`, `SetActive(isActive)` (235-237).
   - `FixedJoint` added if `stickyId != -1` (238-241).
   - Added to `secondPassNeeders` if `stickyId > 0` (242-245), if it has a `Spawner` (246-249), or if it has a `HangNoose` (250-253).
   - `DamageRaw(damage)` (254); velocity applied if non-zero and has a Rigidbody (255-259).
6. **Sub-saves (260-281):** `GetComponentsInChildren<ISaveableObj>(includeInactive: true)`. For each entry in `subSaveData`, every component with `item4.GetType().ToString() == entry["type"]` gets `Load(entry)`, inside try/catch (logs and continues). Special case: `type == "Flame"` with `runtimeAddedFlame == true` calls `Flame.AddBurnCollider(gameObject).Load(item3)` (277-279).
7. Disable if `disableOnStart` (282-285).
8. `loadedUsablesById.Add(saveId, obj)` (286-293). The `catch (KeyNotFoundException)` here logs "couldn't find it in PickupBank.pickupsDict", but that message is wrong: the missing-prefab exception happens at line 232, which is not inside any try.

Failure mode details are in section 13.

---

## 7. Brains: LoadBrains (lines 302-358)

1. Destroys every `Brain` in the scene (304-307). Clears `HouseManager._.player`, `mainCamera`, `characterStoriesDict` (308-310).
2. Order (311-313): `Player` first (`basicBrainData.instanceName == "Player"`), then by `instanceName`.
3. Instantiates `BrainBank.brainsDict[saveDataBrain.prefabName].obj` (318). **No try/catch.** See section 13.
4. Sets `saveId` (319), scale, name, velocity.
5. Loads `basicBrainData.subSaveData` entries into matching `ISaveableObj` components by type (328-337). **No try/catch**, unlike usables.
6. `Brain.Load(item2)` (338), `Brain.Start()` (339), and `bt.Load(behaviorTree)` if present (340-343).
7. `loadedBrainsById.Add(saveId, obj)` (344-351).

**Every Brain token's top-level `"type"` is the literal `"Brain"`.** Each Brain subclass hardcodes that string in its own SaveData (for example `BrainCat.cs:68`, `BrainNeetAI.cs:133`, `DeliveryGuy.cs:33`, `SkeletonParasite.cs:54`, `Spider_House.cs:32`). The concrete class is not in `"type"`. Routing to behavior is done by `prefabName`, and the brain's own `Load(JToken)` (for example `BrainCat.cs:71`) reads its specific fields. Consequence: `GetTokensByType(jObject, "Brain")` returns every brain.

---

## 8. Static objects (LoadStatic) vs spawned objects

**Static (scene-placed) objects** implement `ISaveableStatic`, which adds `int SaveID { get; set; }` (`ISaveableStatic.cs:23`). Their ids come from **Unity scene/inspector data**. Nothing in code assigns them: `SaveID` setters only copy into the backing `saveId` field (for example `Door.cs:137-147`). The lists are on `HouseManager`: `doors`, `lightSwitches`, `sinkFaucets`, `gasButtons`, `sitEnablers`, `restEnablers`, `eventConnectors`, `spawners`, `smokeDetectors` (`HouseManager.cs:128-152`).

`LoadStatic<O,S>` (lines 360-372):
- Builds `Dictionary<int, O>` from `staticObjList` using `SaveID` (`dictionary.Add`, so a duplicate `SaveID` throws `ArgumentException`).
- Line 367 deserializes all tokens with `ToObject<S>()` and discards the result.
- For each token: `dictionary[token["saveId"]].Load(token)`. A saveId that is in the file but not in the scene throws `KeyNotFoundException`, uncaught.

Static objects re-subscribe to saving during `Load` because `Door.Load` calls `Start()`, which subscribes (`Door.cs:162-171`, `:193-196`).

**Spawned objects** (usables and brains) are instantiated from the banks at load time (section 9). They get dynamic ids from `IdSequence`.

---

## 9. ID semantics

### 9a. Id counters

| Member | Where | Meaning |
|---|---|---|
| `SaveLoadHandler.idSequence` (static int) | `SaveLoadHandler.cs:19` | Last issued id. |
| `IdSequence` (static property) | `SaveLoadHandler.cs:43` | `++idSequence`. Issues a new id. |
| `IdSequenceCurrent` | `SaveLoadHandler.cs:45` | Read-only current value. Not used elsewhere. |
| `maxStaticSaveId` (public int) | `SaveLoadHandler.cs:21` | Top of the static id range. Read in `Awake` (line 49) and `AllSaveDataToJSon` (line 102). **Not assigned in code**, so it is set in Unity data. |
| `Awake` | `SaveLoadHandler.cs:49` | `idSequence = maxStaticSaveId + 1`. |
| `LoadAll` | `SaveLoadHandler.cs:163` | Overwrites `idSequence` with the saved `maxIdSequence`. |
| `AllSaveDataToJSon` | `SaveLoadHandler.cs:102` | Writes `Max(maxStaticSaveId, idSequence)`. |

So after a load the next new id is `maxIdSequence + 1`.

### 9b. Usable and Brain ids

- `Usable.saveId` (`Usable.cs:187`). `GenerateSaveId()` (`Usable.cs:262-268`) assigns `IdSequence` only if the current value is 0.
- Usable: called from `SubSaveUsable` (`Usable.cs:330-337`, which runs from `Start` at line 312 when `!dontSave`), and from `Usable.Save` when another object is sticky to this one (`Usable.cs:223`).
- `Brain.saveId`: `Brain.cs:113-120`. Called from `Brain.Start` (line 142).
- On load, the saved `saveId` is applied before `Start()` (for usables at `SaveLoadHandler.cs:233`, for brains at `:319`), so the id is stable across save/load.
- `stickyId` (`Usable.cs:223-231`):
  - `-1`: no sticky joint.
  - `0`: has a `FixedJoint` but `connectedBody == null`.
  - `> 0`: saveId of the connected Usable (via `ISaveableUsable.GenerateSaveId()`), Door (`Door.saveId`), or Brain (`Brain.saveId`).

### 9c. Prefab keys

| Dictionary | Declared | Populated | Key | Used by |
|---|---|---|---|---|
| `PickupBank.pickupsDict` (static `Dictionary<string, Spawnable>`) | `PickupBank.cs:19` | `PickupBank.Init()` (lines 30-41), only when `pickupsDict.Count == 0` (line 32). Key is `pickup.obj.name`. Init is called from `HouseManager.Start` (`HouseManager.cs:357-359`), and only in scene `"NEET_House"` (line 339). | prefab GameObject name | `SaveLoadHandler.cs:232`; many other spawns |
| `BrainBank.brainsDict` (static `Dictionary<string, Spawnable>`) | `BrainBank.cs:9` | `BrainBank.Init()` (lines 20-37): adds each brain whose key is not yet present (no `Count == 0` guard). Called from `HouseManager.cs:361-364`, same scene check. | prefab GameObject name | `SaveLoadHandler.cs:318` |

Both static dictionaries are created in static constructors (`PickupBank.cs:25-28`, `BrainBank.cs:15-18`) and persist across scene loads.

---

## 10. Second-pass loading (LoadSecondPass, lines 405-441)

`secondPassNeeders` is filled during `LoadUsables` with:
- usables where `stickyId > 0` (`SaveLoadHandler.cs:242-245`)
- usables that have a `Spawner` component (`:246-249`)
- usables that have a `HangNoose` component (`:250-253`)

`LoadSecondPass`:
- For `stickyId > 0` (lines 409-435): sets `FixedJoint.connectedBody` on `loadedUsablesById[saveId]`, pointing at the target's rigidbody. Fallbacks: `loadedBrainsById[stickyId]` (line 419), then `HouseManager._.doors` by `saveId` (line 425). Each level is in a try/catch, so a missing target is silently skipped.
- **HangNoose branch (lines 436-439) is effectively dead.** It checks `sd.type == "HangNoose"`, but `sd` is a `Usable.SaveDataUsable` whose `type` is always `"Usable"` (`Usable.cs:241` writes `GetType()`, and no class derives from `Usable`). Consequence **(inferred)**: the hanging corpse is probably not restored from this path. Separately, `HangNoose`'s own data is loaded during the `LoadUsables` sub-save loop (step 6), which runs before `LoadBrains` fills `loadedBrainsById`. `HangNoose.Load` (`HangNoose.cs:33-45`) then fails to find the brain and logs "HangNoose couldn't load hanging corpse". Not runtime-verified.
- **Spawners on usables** are only handled once, inside `Spawner.Load` during the sub-save loop (`Spawner.cs:115-`). That runs before every usable exists, and `Spawner.Load` uses try/catch around `loadedUsablesById` lookups. `LoadSecondPass` has no Spawner branch.

---

## 11. Paint splats (LoadPaintTargets, lines 443-467)

1. `PaintTarget.ClearAllPaint(clearMemory: true)` (line 445).
2. `Dictionary<int, PaintTarget>` built from `HouseManager._.paintTargets` keyed by `paintTarget.saveId` (lines 446-450). `dictionary.Add` throws on a duplicate saveId.
3. For each key: if `<saveId>.png` exists in `persistentDataPath\Textures\PaintTargetSplats` (backslash paths, lines 451-455), loads it as a `Texture2D` and calls `SetupPaint(tex)` (457-460). Otherwise `ClearPaint` (464).

The paint targets come from the scene list, not from the JSON. Only the PNGs are persistent state for paint. Both save and load are Windows-path-specific (`\\`), which matches this build.

---

## 12. Sub-save-data matching and the SaveData table

- Usable and Brain sub-saves are matched by **exact string equality** of `subSaveData[i]["type"]` against each child component's `GetType().ToString()` (`SaveLoadHandler.cs:265`, `:332`). The type strings are bare class names: all the save-owning classes are in the global namespace. The only namespaced folders are `AdvancedGizmo` and `WorldSpaceTransitions`, and none of the classes in the table below are in them.
- A few `type` values are hardcoded instead of `GetType()`: `"Brain"` on all Brain subclasses, and `"TimerSequence"` on `SaveDataTimerSequence`.
- Unknown sub-types are ignored (nothing matches). See section 13.
- `AnimeCardGenerator` does **not** match by type. See section 13.

### SaveData subclass table

66 classes derive from `SaveData` (found with `grep ": SaveData"`). Column "type string" is what the owner writes into `"type"` (`GetType()` means the runtime class name). "Fields" lists public fields as declared; the constructor sets them all except where noted. `subSaveData` is inherited from the base class and is not repeated.

| SaveData class | Owning component (file:line) | "type" string written | Fields |
|---|---|---|---|
| SaveDataAnimeCard | AnimeCard (`AnimeCard.cs:12`) | `GetType()` = `AnimeCard` | level, medalLvl, locked, exp, expToLevel, income, purchasePrice |
| SaveDataAnimeCards | AnimeCardGenerator (`AnimeCardGenerator.cs:10`) | `GetType()` | saveId, maxCardUnlocked, crystals; subSaveData = card saves |
| SaveDataApps | AppDownloader (`AppDownloader.cs:9`) | `GetType()` | boughtApps (bool[]) |
| SaveDataBrain | Brain base (`Brain.cs:29`) | `GetType()` (only nested as `basicBrainData`; not a top-level type) | saveId, prefabName, instanceName, behaviorTree (JObject), position, rotation, scale, velocity, disableOnStart; subSaveData = ISaveable children |
| SaveDataBrainAgentCop | BrainAgentCop (`BrainAgentCop.cs:11`) | `"Brain"` | basicBrainData, enabled |
| SaveDataBrainCat | BrainCat (`BrainCat.cs:9`) | `"Brain"` | basicBrainData, dead, pauseBT, canSpawnRatGang |
| SaveDataBrainNeet | BrainNeetAI (`BrainNeetAI.cs:12`) | `"Brain"` | basicBrainData, currSegmentIndex, currentSegmentLineName, dontLoadWig |
| SaveDataBrainPlayer | BrainPlayer (`BrainPlayer.cs:8`) | `"Brain"` | basicBrainData |
| SaveDataBrainRat | BrainRat (`BrainRat.cs:12`) | `"Brain"` | basicBrainData, dead, skinned, pauseBT, canSpawnRatGang |
| SaveDataBrainTrickOrTreater | BrainTrickOrTreater (`BrainTrickOrTreater.cs:9`) | `"Brain"` | basicBrainData, hasAnswer |
| SaveDataBrainTrickOrTreater (same name, different class) | BrainWithSegments (`BrainWithSegments.cs:11`) | `"Brain"` | basicBrainData |
| SaveDataBreakOnImpact | BreakOnImpact (`BreakOnImpact.cs:8`) | `GetType()` | saveId, breakCollisionAmounts |
| SaveDataCardboardBox | Cardboard_Box (`Cardboard_Box.cs:29`) | `GetType()` | isTaped, isOpened, isVirgin, contentPrefab |
| SaveDataCerealBox | CerealBox (`CerealBox.cs:11`) | `GetType()` | saveId, opened, particlePositions, particleVelocities, particleRemainingLives |
| SaveDataCharacterStories | CharacterStories (`CharacterStories.cs:13`) | `GetType()` | playerIdRef, playerInRange, leaving, talkInput, timeToIgnored, overrideKnot |
| SaveDataComputer | Computer_PowerButton (`Computer_PowerButton.cs:10`) | `GetType()` | isOn, isPowered, money, wallpaperImgPath |
| SaveDataCustomMusicSaveLoad | CustomMusicSaveLoader (`CustomMusicSaveLoader.cs:10`) | `GetType()` | persistentMusicPath |
| SaveDataCustomTextureSaveLoad | CustomTextureSaveLoader (`CustomTextureSaveLoader.cs:10`) | `GetType()` | persistentTexturePath |
| SaveDataBrainDeliveryGuy | DeliveryGuy (`DeliveryGuy.cs:9`) | `"Brain"` | basicBrainData, enabled |
| SaveDataDialogue | Dialogue (`Dialogue.cs:19`) | `GetType()` | firstKnotLoad, storyTextEnabled, bucketActive, currKnot, currSpeakerId, currentText, aliveFlowNames |
| SaveDataDoor | Door (`Door.cs:17`) | `GetType()` | saveId, isClosed, isLocked, openRotation, enabled, unbreakable |
| SaveEggCarton | Egg_Carton (`Egg_Carton.cs:9`) | `GetType()` | opened, eggCount |
| SaveDataEmailHandler | EmailHandler (`EmailHandler.cs:11`) | `GetType()` | randomEmails, pendingEmails, deliveredEmails, namesOpened, timeToNextRandom |
| SaveDataEncounterSpawner | EncounterSpawner (`EncounterSpawner.cs:43`) | `GetType()` | nightId, encounterId, currCharInstanceId, disabled, characterPresent, encounterDelayTimer, timeToIgnored, doorMatText |
| SaveDataEventConnector | EventConnector (`EventConnector.cs:9`) | `GetType()` | saveId, executed |
| SaveDataFireParticle | FireParticle (`FireParticle.cs:8`) | `GetType()` | saveId, position |
| SaveDataFireSpreader | FireSpreader (`FireSpreader.cs:10`) | `GetType()` | saveId, currentLocation, filled; subSaveData = spheres |
| SaveDataFireSpreaderSphere | FireSpreaderSphere (`FireSpreaderSphere.cs:10`) | `GetType()` | saveId, position, scale; subSaveData = particles |
| SaveDataFlame | Flame (`Flame.cs:11`) | `GetType()` | saveId, heatLevel, maxHeatLevel, lit, inert, sterile, canStartFires, deleteFlameOnExtinguish, runtimeAddedFlame, refillCooldown |
| SaveDataFlashlight | Flashlight (`Flashlight.cs:8`) | `GetType()` | isOn |
| SaveDataFood | Food (`Food.cs:18`) | `GetType()` | saveId, cookedMeterCooldownDelayed, cookedMeter, cookCooldownTimer |
| SaveDataFuse | Fuse (`Fuse.cs:8`) | `GetType()` | batteryPower |
| SaveDataFuseSlot | FuseBox_Slot (`FuseBox_Slot.cs:8`) | `GetType()` | fuseId, name |
| SaveDataGasButton | GasButton (`GasButton.cs:9`) | `GetType()` | saveId, gasLeaking, flameIgnited, gasVolumeScale; subSaveData = Flame save |
| SaveDataRestEnabler (nested in Gun) | Gun (`Gun.cs:9`) | `GetType()` = `Gun` | magRefId, inTriggerChair, bedOccupied |
| SaveDataHangNoose | HangNoose (`HangNoose.cs:9`) | `GetType()` | heldBrainId |
| SaveDataHeatHolder | HeatHolder (`HeatHolder.cs:11`) | `GetType()` | saveId, heatIndex, decayDelayTimer |
| SaveDataLightSwitch | LightSwitch (`LightSwitch.cs:9`) | `GetType()` | saveId, isPowered, switchedOn |
| SaveDataBBQLighter | Lighter_BBQ (`Lighter_BBQ.cs:9`) | `GetType()` | saveId, isOn |
| SaveDataLiquidContainer | LiquidContainer (`LiquidContainer.cs:8`) | `GetType()` | saveId, filled, refillCooldown |
| SaveDataPickupPackageArea | PickupPackageArea (`PickupPackageArea.cs:9`) | `GetType()` | lockedBoxesIds |
| SaveDataPicture | Picture (`Picture.cs:8`) | `GetType()` | usableRefSaveId, storedImagePath |
| SaveDataPiggyBank | Piggybank (`Piggybank.cs:9`) | `GetType()` | saveId, cashValue |
| SaveDataPlayerMovement (PlayerStats) | PlayerStats (`PlayerStats.cs:11`) | `GetType()` = `PlayerStats` | statVals (float[,]), statTypes, pausedNeeds, muscle, fat |
| SaveDataPlayerAbilities | Player_Abilities (`Player_Abilities.cs:44`) | `GetType()` | pickupLeftId, pickupRightId, bodyType, killtimerCurrent, clothingIds, peeing, vomiting, dead |
| SaveDataPlayerMovement (Player_Movement) | Player_Movement (`Player_Movement.cs:12`) | `GetType()` = `Player_Movement` | saveId, cachedSlippedConst, isWalking, isCrouching, isSitting, isResting, slipped, velXZ, velY, cachedPreSlipPos, cachedPreSlipRot, slipWaitTimeLeft |
| SaveDataPrefabContainer | PrefabContainer (`PrefabContainer.cs:9`) | `GetType()` | objCount, initObjCount |
| SaveDataRestEnabler | Rest_Enabler (`Rest_Enabler.cs:8`) | `GetType()` | saveId, playerSaveId, inTriggerChair, bedOccupied |
| SaveDataSequenceManager | SequenceManager (`SequenceManager.cs:9`) | `GetType()` | seqObjsIds, seqObjsType |
| SaveDataSinkFaucet | SinkFaucet (`SinkFaucet.cs:8`) | `GetType()` | saveId, pouring, waterVolumeLevel |
| SaveDataSitEnabler | Sit_Enabler (`Sit_Enabler.cs:8`) | `GetType()` | saveId, playerSaveId, inTriggerChair, seatOccupied |
| SaveDataSkeletonParasite | SkeletonParasite (`SkeletonParasite.cs:12`) | `"Brain"` | basicBrainData, navEnabled |
| SaveDataSmokeDetector | SmokeDetector (`SmokeDetector.cs:11`) | `GetType()` | saveId, fuseId |
| SaveDataSpawner | Spawner (`Spawner.cs:10`) | `GetType()` | enabled, saveId, currConcurrentSpawns, concurrentSpawnIds |
| SaveDataBrainSpiderHouse | Spider_House (`Spider_House.cs:8`) | `"Brain"` | basicBrainData, enabled |
| SaveDataStatusHandler | StatusHandler (`StatusHandler.cs:11`) | `GetType()` | effectTypes, effectsToAdd, effectsDelayed, effectTimings, effectsDelayedRemainingTime |
| SaveDataStock | Stock (`Stock.cs:9`, plain class, not a MonoBehaviour) | `GetType()` = `Stock` | ticker, lastPrice, currentProfile, quantityOwned |
| SaveDataStockMarket | StockMarket (`StockMarket.cs:10`) | `GetType()` | stockNames; subSaveData = Stock saves |
| SaveDataTV | TV (`TV.cs:13`) | `GetType()` | displayedScreenModule, isPowered, isOn |
| SaveDataTaxBill | TaxBill (`TaxBill.cs:7`) | `GetType()` | timer, amount, billObjId, stage |
| SaveDataTaxHandler | TaxHandler (`TaxHandler.cs:10`) | `GetType()` | nextBillTimer; subSaveData = TaxBill saves |
| SaveDataTimerSequence | TimerSequence (`TimerSequence.cs:8`) | `"TimerSequence"` (literal) | sequenceName, active, timer |
| SaveDataToiletPaperRoll | Toilet_Paper_Roll (`Toilet_Paper_Roll.cs:9`) | `GetType()` | sheetsCount |
| SaveDataUsable (nested as `Usable.SaveDataUsable`) | Usable (`Usable.cs:19`) | `GetType()` = `Usable` for all current usables (no subclass exists) | saveId, stickyId, prefabName, instanceName, position, rotation, scale, velocity, damage, isChild, isKinematic, isActive, pickupEventOnceConsumed, disableOnStart |
| SaveDataVacuum | VacuumCleaner (`VacuumCleaner.cs:9`) | `GetType()` | fuseId, containedDust, on |
| SaveDataWig | Wig (`Wig.cs:9`) | `GetType()` | alreadyPickedUpOnce |

Notes:
- `SaveDataBrainTrickOrTreater` and `SaveDataPlayerMovement` each have two classes with the same name in different owners. They are distinct nested classes, but any tool that works by simple name will confuse them.
- `SaveDataRestEnabler` likewise exists under both `Gun` and `Rest_Enabler`.
- `Stock` and `StockMarket`'s saves are nested under StockMarket's `subSaveData`. `Stock` is not a MonoBehaviour and does not subscribe on its own.

### ISaveable implementers (grep, class declarations)

Counts are from the class-declaration regex: 58 `ISaveable`, 43 `ISaveableObj`, 9 `ISaveableStatic`, 1 `ISaveableUsable`.

- **ISaveable (58):** AnimeCard, AnimeCardGenerator, AppDownloader, BrainAgentCop, BrainCat, BrainNeetAI, BrainPlayer, BrainRat, BrainTrickOrTreater, BrainWithSegments, BreakOnImpact, Cardboard_Box, CerealBox, CharacterStories, Computer_PowerButton, CustomMusicSaveLoader, CustomTextureSaveLoader, DeliveryGuy, Dialogue, Door, Egg_Carton, EmailHandler, EncounterSpawner, EventConnector, FireParticle, FireSpreader, FireSpreaderSphere, Flame, Flashlight, Food, Fuse, GasButton, Gun, HangNoose, HeatHolder, LightSwitch, Lighter_BBQ, LiquidContainer, PickupPackageArea, Picture, Piggybank, PlayerStats, Player_Abilities, Player_Movement, PrefabContainer, Rest_Enabler, SinkFaucet, Sit_Enabler, SkeletonParasite, SmokeDetector, Spawner, StatusHandler, Stock, StockMarket, TV, TaxHandler, Usable, VacuumCleaner.
- **ISaveableObj (43):** AnimeCard, AnimeCardGenerator, AppDownloader, BrainNeetAI, BreakOnImpact, Cardboard_Box, CerealBox, CharacterStories, Computer_PowerButton, CustomMusicSaveLoader, CustomTextureSaveLoader, Door, Egg_Carton, EmailHandler, EventConnector, FireParticle, FireSpreader, FireSpreaderSphere, Flame, Flashlight, Food, Fuse, GasButton, Gun, HangNoose, HeatHolder, LightSwitch, Lighter_BBQ, LiquidContainer, Picture, PlayerStats, Player_Abilities, Player_Movement, PrefabContainer, Rest_Enabler, SinkFaucet, Sit_Enabler, SkeletonParasite, SmokeDetector, Spawner, StatusHandler, Stock, VacuumCleaner.
- **ISaveableStatic (9):** Door, EventConnector, GasButton, LightSwitch, Rest_Enabler, SinkFaucet, Sit_Enabler, SmokeDetector, Spawner.
- **ISaveableUsable (1):** Usable.

Note: `Brain` itself (`Brain.cs`) is abstract, so the Brain subclasses listed above are the real implementers.

### SaveData-related subscribers (grep `SaveLoadHandler._.SubscribeSave`)

| Enclosing method | Class (file:line) |
|---|---|
| Awake | EmailHandler (`EmailHandler.cs:112`), SeqSpawnThiefIntro (`SeqSpawnThiefIntro.cs:11`) |
| SubSaveUsable | Usable (`Usable.cs:335`) |
| Start | AppDownloader 64, PickupPackageArea 36, AnimeCardGenerator 140, FireSpreader 104, EncounterSpawner 161, Dialogue 239, FuseBox_Slot 39, GasButton 84, SmokeDetector 107, Rest_Enabler 82, SinkFaucet 71, Sit_Enabler 84, SequenceManager 118, Spawner 171, StockMarket 84, Door 195, EventConnector 331, TV 123, Brain 138 (base Start, inherited by subclasses), Computer_PowerButton 92, LightSwitch 89, HouseManager 367 and 371 (looseChangeSpawner.Save, dustSpawner.Save), TaxHandler 77 |

Unsubscribes: `SaveLoadHandler._.UnsubSave(Save)` in `Usable.OnDestroy` (`Usable.cs:253`), `Brain.OnDestroy` (`Brain.cs:126`), and every Brain subclass's OnDestroy (BrainAgentCop 103, BrainPlayer 101, BrainCat 124, BrainWithSegments 54, BrainRat 140, DeliveryGuy 70, BrainTrickOrTreater 48, SkeletonParasite 158).

Subscription happens in `Start`, so the order in which components subscribe (and thus `Save[0]` and the `type` ordering) depends on Unity's Start order. The decompiled code does not fix it.

---

## 13. Failure modes

| Situation | What happens | Where |
|---|---|---|
| Unknown top-level `"type"` | Ignored. Every lookup is an exact-literal filter. | `SaveLoadHandler.cs:159-190`, `GetTokensByType` (207-210) |
| Unknown sub-save `"type"` | Ignored (no component matches). | `SaveLoadHandler.cs:265, 332` |
| An entry with no `"type"` key | `s["type"].ToString()` throws `NullReferenceException` inside the LINQ `Where`. LoadAll aborts. | `SaveLoadHandler.cs:175-190`, `:209` |
| Singleton type absent (TV, PickupPackageArea, SequenceManager, TaxHandler, Dialogue, EncounterSpawner, Computer_PowerButton, StockMarket, or fewer than 2 Spawners) | `.ToList()[0]` throws `ArgumentOutOfRangeException`. LoadAll aborts at that step. | Steps 9-23 in section 5 |
| Absent AnimeCardGenerator, AppDownloader, EmailHandler | Guarded: reset or skipped. | `SaveLoadHandler.cs:378-402` |
| `prefabName` not in `PickupBank.pickupsDict` | `KeyNotFoundException` at line 232. **Uncaught.** LoadUsables aborts, LoadAll aborts, `Start()` aborts. Later usables, brains, static objects, singletons, `OnPostLoad`, and the `spawnAfterLoad` stack init (line 74) are all skipped. The file is not deleted. | `SaveLoadHandler.cs:232` |
| `prefabName` not in `BrainBank.brainsDict` | Same as above, `KeyNotFoundException` at line 318, uncaught. | `SaveLoadHandler.cs:318` |
| Static `saveId` in file, but no scene object with that `SaveID` | `KeyNotFoundException`, uncaught. | `SaveLoadHandler.cs:370` |
| Duplicate `SaveID` among scene static objects | `Dictionary.Add` throws `ArgumentException`, uncaught. | `SaveLoadHandler.cs:365` |
| Duplicate usable `saveId` in file | Logged and skipped. | `SaveLoadHandler.cs:295-298` |
| A usable sub-save's `Load` throws | Caught and logged. Loading continues. | `SaveLoadHandler.cs:269-273` |
| A brain sub-save's `Load` throws | **Not caught.** LoadBrains aborts. | `SaveLoadHandler.cs:334` |
| `AnimeCardGenerator` count mismatch | The loop is `cards[num++].Load(item)` for each subSaveData entry, with no type check. More entries than `cards` throws `IndexOutOfRangeException`, uncaught. Fewer entries leaves the remaining cards at their Start defaults. | `AnimeCardGenerator.cs:86-93` |
| Duplicate `PaintTarget.saveId` | `Dictionary.Add` throws, uncaught. | `SaveLoadHandler.cs:449` |
| A subscribed `Save()` throws during a save | `DynamicInvoke` rethrows; nothing is written. | `SaveLoadHandler.cs:93`, `:106-117` |
| `OnSave` has no subscribers | `OnSave.GetInvocationList()` throws `NullReferenceException`. | `SaveLoadHandler.cs:90` |
| Save file name shorter than 4 characters in `persistentDataPath` | `ArgumentOutOfRangeException` in the scan. | `SaveLoadHandler.cs:146`, `:160`; `Feedback_Sender.cs:543` |
| Newer file with `neet` prefix that is not a save | It can be picked as the newest and fail to parse. | `SaveLoadHandler.cs:159-162` |

---

## Modding notes

### (a) Persisting your own data in the save

Recommended approach:

1. **Subscribe a `Func<SaveData>`** with `SaveLoadHandler._.SubscribeSave(...)` (`SaveLoadHandler.cs:61-64`). Wait for `SaveLoadHandler._ != null` first (`SaveLoadHandler.cs:52`). Use `UnsubSave` on teardown (`:66-69`).
2. **Make a class `: SaveData`** with public fields. Give it a public parameterless constructor so Newtonsoft `ToObject<T>` is straightforward. Set `type` to a **unique string**, for example `"MyMod.Thing"`. Do not use a core class name (`"Usable"`, `"Brain"`, `"Door"`, `"TimerSequence"`, and so on). A collision sends your JSON into a core loader. For example a `"Brain"` entry goes to `LoadBrains`, which looks up `BrainBank.brainsDict[prefabName]` and crashes.
3. **Return `null`** from your Func to skip saving. **Wrap your body in try/catch.** An exception from your Func aborts the whole save (`SaveLoadHandler.cs:93`, no try/catch in `AllSaveDataToJSon` or `SaveAll`).

**Confirmed: LoadAll ignores unknown types.** Every typed read is an exact-match filter (`SaveLoadHandler.cs:159-190`, `:209`, `:265`, `:332`). Your entry is not touched by the game's loaders. Newtonsoft ignores extra properties during `ToObject`, so an entry with unknown fields is harmless. Unknown entries are dropped on the next save, since they are not re-subscribed.

**Save[0]:** the code does not require a specific ordering. `mainStory` and `maxIdSequence` are written onto whichever entry is first, and load reads them from index 0 of the same file, so the round trip works whichever entry that is. Keeping your entry **not** first is still the safer default, because if your entry is first, the core fields live inside your object in the file. If that matters to you, subscribe later than a core object (for example from a component that starts after `Dialogue`), but this order is not guaranteed by the code.

**Reading your data back:** the game does not hand your token to you. Options:
- Harmony postfix on `SaveLoadHandler.LoadAll` (public, `:143`): read the newest `neet*` file yourself (same rule as `:159-162`), find your `type`, apply.
- Harmony postfix on `GetTokensByType` (private, `:207`) if you want to observe each typed lookup.

**File hygiene:** do not put your own files under `persistentDataPath` with a name starting `neet`. The loader picks the newest such file, and `ResetSave` deletes all of them (`Feedback_Sender.cs:538-562`).

### (b) Making a modded item survive save and load

A modded **usable** round-trips through:
1. Save: `Usable.Save()` writes `prefabName` (`Usable.cs:239-241`), but only if `prefabName != ""` and the object is not a lever or button. Items without a `prefabName` are never saved.
2. Load: `PickupBank.pickupsDict[prefabName].obj` is instantiated (`SaveLoadHandler.cs:232`).

**Registration requirements:**
- The key is the **prefab GameObject name** (`PickupBank.cs:38`: `pickupsDict.Add(pickup.obj.name, pickup)`).
- Register it in `PickupBank.pickupsDict` (public static, `PickupBank.cs:19`) **before `LoadAll` runs** (`SaveLoadHandler.Start`, `:73`).
- Do not rely on adding to the `pickups` list. `PickupBank.Init` only fills the dictionary when it is empty (`PickupBank.cs:32`), and it only runs from `HouseManager.Start` in scene `NEET_House` (`HouseManager.cs:339, 357-359`). Once the dictionary has entries, later additions to `pickups` are ignored. Add to `pickupsDict` directly.
- Timing: the decompiled code does not fix the order between `SaveLoadHandler.Start` and `HouseManager.Start` (it depends on Unity execution order and scene layout, not verified). On the first load of a session, if `LoadAll` runs first, `pickupsDict` may still be empty and the first usable lookup throws. Registering from your plugin's `Awake` or a Harmony prefix on `LoadAll` avoids this. Static dictionaries persist across scene loads, so once `NEET_House` has started once, the dictionary stays populated.
- Brains work the same way with `BrainBank.brainsDict` (`BrainBank.cs:9`). Its `Init` has no empty-check, so it adds each brain that is not already present (`BrainBank.cs:24`).

**What happens if the mod is removed (exact line):**
- On the next launch, the save still contains a usable with `prefabName == "MyItem"`.
- `PickupBank.pickupsDict["MyItem"]` throws `KeyNotFoundException` at **`decompiled/Assembly-CSharp/SaveLoadHandler.cs:232`**.
- That line is inside the `foreach` in `LoadUsables` with **no try/catch**. The try at lines 286-292 comes after instantiation and does not cover it.
- The exception propagates out of `LoadUsables` and `LoadAll` to `Start()`. Unity logs it. `Start()` does not continue, so the following are skipped: `LoadBrains` and all static objects, `LoadComputer`, `LoadSecondPass`, paint, `Dialogue` (including the Ink state), `EncounterSpawner`, `OnPostLoad` hooks, and the `spawnAfterLoad` stack initialization (`SaveLoadHandler.cs:74`).
- Usables before the missing one are loaded. Usables after it are not. Scene copies were already destroyed by the pre-clean (`:214-221`), so the scene may be left partially populated.
- The game does not hard-crash from this alone (Unity logs and continues). Whether the resulting state is playable was **not verified**.
- A removed modded **brain** does the same thing at `SaveLoadHandler.cs:318`.

Mitigations for a mod author:
- Keep the mod installed, or accept the partial load.
- A Harmony prefix on `LoadUsables` (private, `SaveLoadHandler.cs:212`; patch via `AccessTools.Method`) can filter out tokens whose `prefabName` is not in `pickupsDict` before the loop starts.
- Register a harmless placeholder under the same key so the load succeeds with a stand-in item.

### (c) Good Harmony patch points

| Target | Signature (file:line) | Use |
|---|---|---|
| `SaveLoadHandler.AllSaveDataToJSon` | `public JObject AllSaveDataToJSon()` (`:87`) | **Best injection point.** A postfix can add or edit entries in the returned JObject without a subscription. The Save[0] fields are added inside this method. |
| `SaveLoadHandler.SaveAll` | `public void SaveAll()` (`:106`) | Pre/post save. Covers all callers (auto-save, death, dream, reset). Or use the public Action fields (`OnPreSave` `:29`, `OnPostSave` `:31`), which need no Harmony. |
| `SaveLoadHandler.LoadAll` | `public void LoadAll()` (`:143`) | Prefix: populate `PickupBank.pickupsDict` and `BrainBank.brainsDict` before loading (section b). Postfix: post-load work after `OnPostLoad`. Returning `false` from a prefab would skip the original, leaving the game with no loaded state. |
| `SaveLoadHandler.LoadUsables` | `private void LoadUsables(List<JToken>)` (`:212`) | Prefix: filter tokens. Postfix: inspect loaded usables. |
| `SaveLoadHandler.LoadBrains` | `private void LoadBrains(List<JToken>)` (`:302`) | Same as usables, for brains. |
| `SaveLoadHandler.GetTokensByType` | `private List<JToken> GetTokensByType(JObject, string, bool)` (`:207`) | Single choke point for typed lookups (except the singleton `.Where(...)` calls). |
| `Usable.Save` / `Brain.SaveBasicBrain` | `public SaveData Save()` (`Usable.cs:212`); `protected SaveDataBrain SaveBasicBrain()` (`Brain.cs:97`) | Per-object data edits. Patching a method does not change which delegate is subscribed, since subscription uses the method group at `Start`. |
| `SaveLoadHandler.LoadStatic<O,S>` | generic (`:360`) | Avoid. Patch the concrete `Load(JToken)` methods instead (for example `Door.Load`, `Door.cs:162`). |

Harmony caveat **(not verified)**: delegates are created from method groups at subscription time (`SubscribeSave(Save)`). Harmony rewrites the method's entry point, so existing delegates should call the patched body, but this should be tested.

Also: `SaveLoadHandler._` is only valid after `Awake` (`SaveLoadHandler.cs:52`). Mods that touch it earlier need a null check.
