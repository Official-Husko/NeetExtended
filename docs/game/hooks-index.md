# NEET Demo: hooks and extension-surface index

Mechanically gathered with grep and small scripts over `decompiled/Assembly-CSharp/`. Line numbers refer to those files. Nothing was run in-game.

Caveats on the mechanics:
- Invocation and subscription sites are matched **by member name**, not by object. Where the same name appears in several classes (for example `OnExit`, `OnPreLoad`, `deliveryAction`), the "invoked at" column lists sites for all of them, and the subscriber count is combined.
- Grep cannot see calls made through reflection, through the Unity Inspector, or through Ink story script. A count of `0` means "no call found by grep", not "never called".
- The regex for declarations only catches one-line declarations, so a few members may be missing.

For save-related hooks and the SaveData catalogue, see `docs/game/save-system.md`.

---

## 1. Static singletons

All use the `_` naming convention except `GizmoFollow.Instance`.

| Type | Member (declaration) | Backing / assignment | Assigned in | Notes |
|---|---|---|---|---|
| `AchievementManager` | `public static AchievementManager _;` (`AchievementManager.cs:160`) | `_ = this;` | `Awake` (`:197`) | Plain field |
| `MaterialTracker` | `public static MaterialTracker _;` (`MaterialTracker.cs:11`) | `_ = this;` | `Start` (`:15`) | Plain field, set later than Awake |
| `EmailHandler` | `public static EmailHandler _ => _instance;` (`EmailHandler.cs:60`) | `private static EmailHandler _instance;` (`:54`), `_instance = this;` | `Awake` (`:106`) | Property |
| `EncounterSpawner` | `public static EncounterSpawner _;` (`EncounterSpawner.cs:115`) | `_ = this;` | `Awake` (`:144`) | Plain field |
| `SaveLoadHandler` | `public static SaveLoadHandler _ => _SaveLoadHandler;` (`SaveLoadHandler.cs:41`) | `private static SaveLoadHandler _SaveLoadHandler;` (`:17`), `_SaveLoadHandler = this;` | `Awake` (`:52`), **only if null**, otherwise logs "Only one SaveLoadHandler allowed" | Property |
| `Dialogue` | `public static Dialogue _;` (`Dialogue.cs:48`) | `_ = this;` | `Awake` (`:143`) | Plain field |
| `StockMarket` | `public static StockMarket _ => _instance;` (`StockMarket.cs:33`) | `private static StockMarket _instance;` (`:21`), `_instance = this;` | `Awake` (`:43`) | Property |
| `HouseManager` | `public static HouseManager _ => _instance;` (`HouseManager.cs:174`) | `private static HouseManager _instance;` (`:18`), `_instance = this;` | `Awake` (`:324`) | Property. Its `Start` (`:335`) does the `PickupBank`/`BrainBank` `Init` calls. |
| `SequenceManager` | `public static SequenceManager _;` (`SequenceManager.cs:31`) | `_ = this;` | `Awake` (`:100`) | Plain field |
| `Feedback_Sender` | `public static Feedback_Sender _;` (`Feedback_Sender.cs:16`) | `_ = this;` | `Awake` (`:106`) | Plain field. Settings UI/PlayerPrefs owner. |
| `GizmoFollow` (namespace `WorldSpaceTransitions`) | `public static GizmoFollow Instance => m_instance;` (`WorldSpaceTransitions/GizmoFollow.cs:21`) | `m_instance = this;` (`:32`), `m_instance = null;` (`:56`) | `Awake` (`:32`); cleared in `OnDestroy` (`:56`) | Only `Instance` in the codebase; not `_` |

Usage pattern (from grep): code reads these as `X._.something` with no null guard in most places, so a mod that calls them early (before the owning object's `Awake`) gets a `NullReferenceException`.

---

## 2. Static registries and collections

| Declaration | Key -> value (meaning) | Populated / cleared | Used by |
|---|---|---|---|
| `PickupBank.pickupsDict` `public static Dictionary<string, Spawnable>` (`PickupBank.cs:19`) | prefab GameObject name -> `Spawnable` (prefab and metadata) | `PickupBank.Init()` (`:30-41`), only when `Count == 0`. Called from `HouseManager.Start` (`HouseManager.cs:357-359`) in scene `NEET_House`. | `SaveLoadHandler.cs:232`; `LiquidContainer.cs:167`; `Piggybank.cs:138-141`; `Commons.cs:213-238`; `NakedTrigger.cs:24`; `Spider_House.cs:146`; `SpraySphere.cs:66`; `HouseManager.cs:227`; `SequenceManager.cs:158`; `Cardboard_Box.cs:110`; `Dialogue.cs:278` (via `GetSpawnable`) |
| `BrainBank.brainsDict` `public static Dictionary<string, Spawnable>` (`BrainBank.cs:9`) | brain prefab name -> `Spawnable` | `BrainBank.Init()` (`:20-37`), adds if key absent. Called from `HouseManager.Start` (`:361-364`). | `SaveLoadHandler.cs:318`; `Cardboard_Box.cs:116`; `Dialogue.cs:289` (`"Player_NPC"`); `SequenceManager.cs:173`; `HouseManager.cs:430`; `ThiefHandler.cs:41` (`"Thief"`) |
| `SaveLoadHandler.loadedUsablesById` `public static Dictionary<int, GameObject>` (`SaveLoadHandler.cs:23`) | saveId -> live usable GameObject, for the current load | Reset in `LoadAll` (`:156`) | `SaveLoadHandler.cs:413, 438`; `Spawner.cs:130` |
| `SaveLoadHandler.loadedBrainsById` `public static Dictionary<int, GameObject>` (`SaveLoadHandler.cs:25`) | saveId -> live brain GameObject | Reset in `LoadAll` (`:157`) | `SaveLoadHandler.cs:419`; `HangNoose.cs:40` |
| `SaveLoadHandler` static scalars: `timeToLastSave`, `timeToLastLoad` (`:11, 13`), `idSequence` (`:19`) | Timers and id counter, see save doc | `LoadAll`, `Update`, `IdSequence` | `DeliveryGuy.cs:53`; `Spawner.cs:274`; `Toilet_Flush.cs:107`; `RefrigerationZone.cs:54, 76` |
| `Locator.rooms` `public static Dictionary<Location, GameObject>` (`Locator.cs:8`) | `Location` -> room GameObject (its `Locator` component is used via `Locator.GetRoom`) | Not checked for population point | `SaveLoadHandler.cs:471` (fire loading) |
| `EncounterSpawner.Nights` `public static Encounter[][]` (`EncounterSpawner.cs:98`) | Jagged array of encounters per night (inferred from the name and `nightId`/`encounterId` in its save data; not confirmed) | Not checked | `EncounterSpawner.cs` |
| `PaintTarget.paintColors` `public static Color[]` (`PaintTarget.cs:59`) | Paint colour palette (indexed by paint colour) | Not checked | `PaintTarget.cs` |
| `Water.liquidSpheres`, `Water.liquidSpheresGrav` `public static GameObject[]` (`Water.cs:7, 9`) | Index by liquid type -> spray-sphere prefab (ground and gravity variants) | Not checked | `Commons.cs:249` (`SpawnPaintSpray` uses `liquidSpheres[paintColor]`); `PlayerStats.cs:291` (`liquidSpheresGrav[wetWithLiquidType]`) |
| `Water.liquidColors` `public static Color[]` (`Water.cs:11`) | Index by liquid type -> colour | Not checked | Not checked |
| `Stock.StockMovementTypes` `private static Dictionary<StockProfile, Func<float>>` (`Stock.cs:56`) | `StockProfile` -> price-step function | Initialized inline | `Stock.cs` |
| `Commons.ExteriorLocations` `public static List<Location>` property (`Commons.cs:11`) | Locations considered outside the house | Computed each read (`=>`) | Not checked |
| `NormalDistribution.confidence_to_z_score` `private static float[]` (`NormalDistribution.cs:23`) | Confidence level -> z-score table | Inline | `NormalDistribution.cs` |

---

## 3. Public delegate hooks

Columns: **Class (file:line)** | **member** | **signature** | **invoked at (grep)** | **subscriber sites, count (name-based)**.

Lines marked `none found` have no invocation site in the decompiled code.

### 3a. Save and load hooks (`SaveLoadHandler` and friends)

| Class (file:line) | member | signature | invoked at | subscribers |
|---|---|---|---|---|
| SaveLoadHandler (`SaveLoadHandler.cs:27`) | `OnSave` | `private Func<SaveData>` (combined via `SubscribeSave`/`UnsubSave`) | `AllSaveDataToJSon` `SaveLoadHandler.cs:93` (DynamicInvoke, every save) | see save doc §12 |
| SaveLoadHandler (`:29`) | `OnPreSave` | `public Action` | `SaveAll` `:110` | none found |
| SaveLoadHandler (`:31`) | `OnPostSave` | `public Action` | `SaveAll` `:121` | none found |
| SaveLoadHandler (`:33`) | `OnPreLoad` | `public Action` | `LoadAll` `:154`; `EmailHandler.cs:78`; `StockMarket.cs:59` | 4: `EmailApp.cs:19`, `PaintTarget.cs:364`, `PickupPackageArea.cs:31`, `StockGraph.cs:32` (all via Delegate.Combine) |
| SaveLoadHandler (`:35`) | `OnPostLoad` | `public Action` | `LoadAll` `:192`; `EmailHandler.cs:94`; `StockMarket.cs:77` | 3: `EmailApp.cs:21`, `StockGraph.cs:34`, `StockGraph.cs:36` |
| EmailHandler (`EmailHandler.cs:48`) | `OnEmailReceived` | `public Action` | `EmailHandler.cs:160` | 1: `EmailApp.cs:17` |
| EmailHandler (`:50`, `:52`) | `OnPreLoad`, `OnPostLoad` | `public Action` | `EmailHandler.cs:78, 94` | see `SaveLoadHandler` rows (same names) |
| StockMarket (`:29`, `:31`) | `OnPreLoad`, `OnPostLoad` | `public Action` | `StockMarket.cs:59, 77` | see `SaveLoadHandler` rows |

### 3b. Usable, item and interaction hooks

| Class (file:line) | member | signature | invoked at | subscribers |
|---|---|---|---|---|
| Usable (`Usable.cs:129`) | `useEvent` | `public UnityEvent` (Inspector-wired) | `EventConnector.cs:86, 100, 128` | none in code (Inspector) |
| Usable (`:131`) | `pickupEvent` | `public UnityEvent` | `Player_Abilities.cs:1179` | none in code (Inspector) |
| Usable (`:137`) | `OnUse` | `public Action` | `Player_Abilities.cs:1204` (the `Input_Asset.cs:302` match is an interface declaration, not a call) | 1: `Phone_Interface.cs:63` |
| Usable (`:139`) | `OnPickup` | `public Action` | `Player_Abilities.cs:1173, 1208` | 1: `HangNoose.cs:67` |
| Usable (`:170`) | `OnBreak` | `public Action` | `Usable.cs:714` | 8 (for example `LiquidBomb.cs:38`, `PrefabContainer.cs:74`) |
| Brain (`Brain.cs:74`) | `OnLocationChange` | `public Action<Location>` | `Brain.cs:184` | 2: `BrainPlayer.cs:63`, `Player_Abilities.cs:446` |
| Brain (`:90`) | `OnDestroyAction` | `public Action` | `Brain.cs:130` (in `OnDestroy`) | 2: `HouseManager.cs:433`, `Player_Abilities.cs:1697` |
| Brain subclasses: `BrainNeetAI.cs:115`, `BrainTrickOrTreater.cs:25`, `BrainWithSegments.cs:24` | `OnExit` | `public Action` (each class) | `BrainNeetAI.cs:807`, `BrainTrickOrTreater.cs:111`, `BrainWithSegments.cs:139` | none found by name |
| BrainNeetAI (`:47`) | `UseHandDecider` | `public Func<int>` | `BrainNeetAI.cs:428` | none found |
| BrainAgentCop (`:27`), DeliveryGuy (`:25`) | `deliveryAction` | `public Action` (each class) | `BrainAgentCop.cs:330`, `DeliveryGuy.cs:108` | none found |
| Player_Abilities (`:140`) | `pickupAction` | `public Action<Hands>` | none found (only nulled at `:973, 1003`) | none found |
| Player_Abilities (`:142`) | `OnPreDeath` | `public Action` | `Player_Abilities.cs:1659, 1718, 1865` | 1: `LoadSceneOnDeath.cs:12` (triggers `SaveAll`) |
| Player_Abilities (`:249`) | `OnDelayedKill` | `public UnityEvent` | `Player_Abilities.cs:764` | none in code |
| Player_Movement (`:128`) | `OnCrouch` | `public Action` | `Player_Movement.cs:291` (the `Input_Asset.cs:298` match is an interface declaration) | 4: `Rest_Enabler.cs:101, 111` (name-based) |
| Player_Movement (`:130`) | `OnStand` | `public Action` | `Player_Movement.cs:296` | 4: `Rest_Enabler.cs:102, 112` |
| Spawner (`Spawner.cs:81`) | `spawnCondition` | `public Func<bool>` | `Spawner.cs:274` | none in code (Inspector/code-set) |
| Spawner (`:83`) | `applyToObjOnSpawn` | `public Action<GameObject>` | `Spawner.cs:224` | 2: `Commons.cs:245`, `Player_Abilities.cs:1895` |
| PrefabContainer (`PrefabContainer.cs:47`) | `OnSpawn` | `public Action<GameObject>` | `BreakOnImpact.cs:144`, `PrefabContainer.cs:103` | 9 (for example `NeetCloneSpawner.cs:9`, `Commons.cs:245`) |
| PrefabContainer (`:49`) | `OnEmpty` | `public Action<GameObject>` | `PrefabContainer.cs:143` | 1: `PrefabContainer.cs:77` |
| BreakOnImpact (`:54`) | `OnSpawn` | `public Action<GameObject>` | `BreakOnImpact.cs:144` (name-based, shares name with PrefabContainer) | 9 (shared) |
| BreakOnImpact (`:56`) | `OnCollAction` | `public Action<Collision>` | `BreakOnImpact.cs:196` | 1: `LiquidContainer.cs:76` |
| BreakOnImpact (`:58`) | `OnBreakAction` | `public Action` | `BreakOnImpact.cs:133` | 5 (for example `BreakOnImpact.cs:91`, `Piggybank.cs:60`) |
| Food (`Food.cs:95`) | `OnEat` | `public Action<Player_Abilities>` | `Food.cs:563` | 2: `ThreadMarker.cs:45, 64` |
| Food (`:97`) | `OnEatEvent` | `public UnityEvent` | `Food.cs:567` | none in code |
| Food (`:103`) | `OnSpoil` | `public Action` | `Food.cs:217` | none in code |
| Food (`:113`) | `onCookingChg` | `public Action` | `Food.cs:182` | 2: `Seafood_Delight.cs:29, 48` |
| Food (`:115`) | `onCooked` | `public Action` | `Food.cs:188` | 1: `BrainRat.cs:384` |
| Sectionable (`Sectionable.cs:19`) | `OnCut` | `public Action` | `Sectionable.cs:135, 156` | 1: `Cardboard_Box.cs:143` |
| Sectionable (`:21`) | `OnCutNextSegmented` | `public Action<GameObject>` | `Sectionable.cs:63, 99, 103` (and 2 more) | 2: `Food.cs:246, 477` |
| Sectionable (`:23`) | `OnCutNextSegment` | `public Action<GameObject>` | none found | 1: `Food.cs:473` |
| Cutter (`Cutter.cs:10`) | `OnDamageRegistered` | `public event Action<GameObject>` (from `IDamager`) | `Cutter.cs:36` | 1: `Contact_JauntMarker.cs:21` |
| Cutter (`:12`) | `OnDamageDealt` | `public event Action<GameObject>` (from `IDamager`) | `Cutter.cs:47` | none found |
| CollisionPainter (`CollisionPainter.cs:34`) | `OnCollidedLong` | `public event Action<Collision>` | `CollisionPainter.cs:102` | 2: `Flame.cs:117, 314` |
| CollisionPainter (`:36`) | `OnCollided` | `public event Action<Collision>` | `CollisionPainter.cs:90` | 1: `Flame.cs:118` |
| PaintTarget (`PaintTarget.cs:57`) | `OnPainted` | `public Action` | `PaintTarget.cs:373` | 1: `PaintLightSurface.cs:16` |

### 3c. Fire, light, room and door hooks

| Class (file:line) | member | signature | invoked at | subscribers |
|---|---|---|---|---|
| Flame (`Flame.cs:67`) | `OnIgnited` | `public Action` | `Flame.cs:155` | 6 (for example `EventConnector.cs:145`, `QueenOfDust.cs:28`) |
| Flame (`:69`) | `OnExtinguished` | `public Action` | `Flame.cs:191` | 6 (for example `EventConnector.cs:139`, `QueenOfDust.cs:30`) |
| FireSpreader (`FireSpreader.cs:56`) | `OnClear` | `public Action` | `FireSpreader.cs:250` | 2: `Locator.cs:118`, `Flame.cs:287` |
| Locator (`Locator.cs:22`) | `OnFireStart` | `public Action` | `Locator.cs:115` | 1: `SmokeDetector.cs:122` |
| Locator (`:24`) | `OnFireStop` | `public Action` | none found | 1: `SmokeDetector.cs:127` (and `Locator.cs:118`, which chains it) |
| HouseManager (`HouseManager.cs:176`) | `OnLightningStrike` | `public event LightningStrike` (delegate `void LightningStrike()` at `:14`) | `HouseManager.cs:505-507` (in `Thunder`) | 1: `LightSwitch.cs:86` (`Flicker`) |
| LightSwitch (`LightSwitch.cs:38`) | `OnSwitched` | `public Action<bool>` | `LightSwitch.cs:104` | 1: `Computer.cs:154` |
| LightSwitch (`:40`) | `OnPowered` | `public Action<bool>` | none found | none found |
| LightSwitch (`:42`) | `OnActivated` | `public Action<bool>` | none found | none found |
| Door (`Door.cs:149`) | `OnUnlocked` | `public event Action` | `Door.cs:128` | 1: `DoorTalking.cs:7` |
| Door (`:151`) | `OnLocked` | `public event Action` | `Door.cs:132` | none found |
| Door (`:153`) | `OnOpened` | `public event Action` | none found | none found |
| Door (`:155`) | `OnClosed` | `public event Action` | none found | none found |
| FuseBox_Button (`FuseBox_Button.cs:12`) | `OnToggle` | `public Action` | `FuseBox_Button.cs:25` | 1: `FuseBox_Slot.cs:38` |
| FuseBox_Slot (`FuseBox_Slot.cs:28`) | `OnPowerStateChg` | `public Action<Location, bool>` | `FuseBox_Slot.cs:133, 138` | 1: `FuseBox_Manager.cs:53` |
| Fuse (`Fuse.cs:27`) | `OnFusePowerChg` | `public Action` | `Fuse.cs:69` | none found |
| Fuse (`:29`) | `OnFuseLevelChg` | `public Action<Color>` | `Fuse.cs:80` | 5 (for example `FuseBox_Slot.cs:83, 100`) |
| SceneSwitcher (`SceneSwitcher.cs:11`) | `OnUnload` | `public Action` | `SceneSwitcher.cs:29` | 1: `DreamRandomizer.cs:59` |
| SceneObjSwitcher (`SceneObjSwitcher.cs:13`) | `OnPostSwitchAction` | `public Action` | `SceneObjSwitcher.cs:30` | 1: `Commons.cs:263` |

### 3d. Computer, stock and character hooks

| Class (file:line) | member | signature | invoked at | subscribers |
|---|---|---|---|---|
| Computer (`Computer.cs:58`) | `OnMoneyChanged` | `public Action<float, float>` | `Computer.cs:72` | 5 (for example `Computer.cs:121, 128`) |
| Computer (`:60`) | `OnTaxesChanged` | `public Action<float, float>` | `Computer.cs:91` | none found |
| Stock (`Stock.cs:48`) | `OnPriceChanged` | `public Action<float>` | `Stock.cs:217` | 1: `StockGraph.cs:68` |
| Stock (`:50`) | `OnBought` | `public Action<int>` | `StockMarket.cs:110` | 1: `StockGraph.cs:74` |
| Stock (`:52`) | `OnSold` | `public Action<int>` | `StockMarket.cs:128` | 1: `StockGraph.cs:79` |
| AchievementManager (`AchievementManager.cs:28`) | `OnCompleted` | `public Action<string, bool>` | `AchievementManager.cs:39` | 1: `AchievementManager.cs:120` (name-based) |
| CharacterStories (`CharacterStories.cs:77`) | `OnDestroyChar` | `public Action` | none found | 1: `EncounterSpawner.cs:177` |
| CharacterStories (`:79`) | `OnPlayerInRange` | `public Action` | `CharacterStories.cs:149` | 1: `Phone_Interface.cs:50` |
| CharacterStories (`:81`) | `OnPlayerOutRange` | `public Action` | `CharacterStories.cs:201` | 1: `Phone_Interface.cs:55` |
| Dialogue (`Dialogue.cs:82`) | `OnSwitchFlow` | `public Action` | `Dialogue.cs:387` | 1: `Dialogue.cs:137` |
| EventConnector (`EventConnector.cs:274, 276, 278, 280`) | `colEnterDeleg`, `triggerEnterDeleg`, `colExitDeleg`, `triggerExitDeleg` | `public Action` | `EventConnector.cs:345/355`, `375/385`, `405/415`, `435/445` | 1 each: `EventConnector.cs:151, 157, 163, 169` |
| EventConnector (`EventConnector.cs:73`) | `useEvent` | `public UnityEvent` | `EventConnector.cs:86, 100, 128` | none in code |
| Stat (`Stat.cs:26`) | `OnChanged` | `public Action` | `Stat.cs:84` (inside `SetVal`) | 7 (for example `BrainPlayer.cs:85`, `FluidGun.cs:101`) |
| Stat (`:28`) | `OnSubZero` | `public Action` | `Stat.cs:88` | 8 (for example `BrainNeetAI.cs:259`, `CatStats.cs:21`) |
| Stat (`:30`) | `OnNotZero` | `public Action` | `Stat.cs:92` | 1: `Player_Movement.cs:209` |
| Stat (`:32`) | `OnMaxed` | `public Action` | `Stat.cs:96` | none found |
| Stat (`:34`) | `manageEffect` | `public Stat.EffectManager` (see below) | `Stat.cs:111-113` (inside `Affect`) | assigned via `Delegate.Combine` in `CatStats.cs:18`, `HealthOnly.cs:12`, `RatStats.cs:14`, `PlayerStats.cs:213` |
| Stat (`Stat.cs:7`) | `delegate float EffectManager(Vector3 pos, float initVal, float potency, EffectType effTyp, Vector3? force = null, GameObject sourceObj = null)` | custom delegate | see `manageEffect` above | |

### 3e. Behaviour tree delegates (`BT*.cs`)

Node helpers that take delegates. These are used as constructor-injected behaviour (for example `BTAction(Func<NodeState>)`), not as events.

| Class (file:line) | member | signature | invoked at |
|---|---|---|---|
| BTAction (`BTAction.cs:6`) | `action` | `public Func<NodeState>` | `BTAction.cs:19` |
| BTActionTgt (`BTActionTgt.cs:10`) | `getTarget` | `public Func<AITarget>` | `BTActionTgt.cs:24, 113`; `BTMoveTo.cs:52` |
| BTActionTgt (`:12`) | `setTarget` | `public Action<AITarget>` | `BTActionTgt.cs:85, 103` |
| BTActionTgt (`:14`) | `actionTargeted` | `public Func<AITarget, NodeState>` | `BTActionTgt.cs:116`; `BTActionFixedTgt.cs:71`; `BTMoveTo.cs:119` |
| BTAnd (`BTAnd.cs:6, 8`) | `predicate1`, `predicate2` | `public Func<NodeState>` | `BTAnd.cs:19`; `BTOr.cs:19` (shared pattern) |
| BTOr (`BTOr.cs:6, 8`) | `predicate1`, `predicate2` | `public Func<NodeState>` | `BTAnd.cs:19`; `BTOr.cs:19` |
| BTConditional (`BTConditional.cs:6`) | `predicate` | `public Func<NodeState>` | `BTConditional.cs:16`; `BTConditionalElse.cs:19` |
| BTConditionalElse (`BTConditionalElse.cs:6`) | `predicate` | `public Func<NodeState>` | `BTConditionalElse.cs:19`; `BTConditional.cs:16` |
| BTMoveTo (`BTMoveTo.cs:14`) | `onDestReached` | `public Action` | `BTMoveTo.cs:105` |
| BTTargetRef (`BTTargetRef.cs`) | `setter`, `finder` | `private Action<AITarget>`, `private Func<AITarget>` | Not tracked |

### 3f. Other delegates

| Class (file:line) | member | signature | invoked at |
|---|---|---|---|
| Spawner (`Spawner.cs:32`, `ISpawnable`) | `OnDespawn` | `event Action` | `Usable.cs:257` (via the implementing `Usable`) |
| IDamager (`IDamager.cs:6, 8`) | `OnDamageRegistered`, `OnDamageDealt` | `event Action<GameObject>` | `Cutter` (see 3b) |
| Usable (`:201`) | `OnDespawn` | `public event Action` | see 3b |
Generic `Func<...>` / `Action<...>` members not listed above were not traced exhaustively.

---

## 4. ConsoleMethod commands (IngameDebugConsole)

Only **four** `[ConsoleMethod]` attributes exist in the decompiled code, in two classes. They use the `IngameDebugConsole` namespace (`using IngameDebugConsole;`). The console library itself is not in the decompiled tree, so how it resolves duplicate command names is not known from this code.

| Command | Method (file:line) | Arguments | Description (verbatim) |
|---|---|---|---|
| `get` | `BrainBank.SpawnbrainAtPlayer(string name)` (`BrainBank.cs:51-54`) | `name` | "Spawn a brain." |
| `spawnAt` | `BrainBank.SpawnbrainInRoom(string name, string room)` (`BrainBank.cs:57-62`) | `name`, `room` | "Spawn [brain] in [room]. Shoots it from the center of the room in a random direction." |
| `get` | `PickupBank.SpawnPickupAtPlayer(string name)` (`PickupBank.cs:68-72`) | `name` | "Spawn a pickup." |
| `spawnAt` | `PickupBank.SpawnPickupInRoom(string name, string room)` (`PickupBank.cs:74-82`) | `name`, `room` | "Spawn [pickup] in [room]. Shoots it from the center of the room in a random direction." |

**Collision warning:** `get` and `spawnAt` are registered by **both** banks. Both methods are static. Which one the console runs for a given name is up to IngameDebugConsole, which is not in this tree.

Behaviour notes:
- `GetSpawnable(name)` indexes the dictionary directly, so an unknown name throws `KeyNotFoundException`.
- `Locator.ParseLocationName(room)` (used by both `spawnAt`s) is not checked for null.

---

## 5. Interfaces declared by the game

Implementers were found with a class-declaration regex. Nested interfaces are shown with their owning class.

| Interface (file:line) | Members | Implementers |
|---|---|---|
| `IBatteryPowered` (`IBatteryPowered.cs:1`) | `Fuse Fuse { get; set; }`, `void ModifyPower(float amt);` | Radio, SmokeDetector, VacuumCleaner |
| `IBrainHijackable` (`IBrainHijackable.cs:1`) | `void Hijack();` | BrainRat |
| `IDamager` (`IDamager.cs:4`) | `event Action<GameObject> OnDamageRegistered;` `event Action<GameObject> OnDamageDealt;` | Cutter |
| `Player_Abilities.IDropTriggerable` (`Player_Abilities.cs:76`) | `void DropTrigger();` | BrainCat, BrainRat, Cardboard_Box, Door, DynamicBoneUsable, HygieneDecreaser |
| `Food.IEatable` (`Food.cs:12`) | `void Eat();` | CerealBox |
| `IElectronic` (`IElectronic.cs:1`) | `bool IsPowered { get; set; }` | Computer_PowerButton, LightSwitch, Refrigerator, Sparker, TV |
| `CharacterStories.IEncounterable` (`CharacterStories.cs:7`) | `void TriggerExit(int exitType = 0);` | TutorialJack |
| `Computer.IInputReceiver` (`Computer.cs:9`) | `void ProcessKey(string k);` | Computer_Browser_Addresser |
| `IObliterateable` (`IObliterateable.cs:1`) | `void Obliterate();` | BrainCat, BrainRat, Player_Abilities |
| `Player_Abilities.IPickupTriggerable` (`Player_Abilities.cs:86`) | `void PickupTrigger(GameObject picker);` | BrainCat, BrainRat, Cardboard_Box, Contact_JauntMarker, DynamicBoneUsable, HygieneDecreaser, Loot, Wig |
| `Player_Abilities.IPunchTriggerable` (`Player_Abilities.cs:101`) | `void PunchTrigger();` | Dumbbell, Food, Radio, TV_Remote, VacuumCleaner |
| `Player_Abilities.IReleaseTriggerable` (`Player_Abilities.cs:91`) | `void ReleaseTrigger();` | SprayBottle |
| `Player_Abilities.IStopUseTriggerable` (`Player_Abilities.cs:96`) | `void StopUseTrigger();` | FireExtinguisher, Music_Instrument, SprayBottle, TV_Remote |
| `Player_Abilities.IThrowTriggerable` (`Player_Abilities.cs:81`) | `void ThrowTrigger();` | Knife, Scissors |
| `MaskFace.MaskEffectable` (`MaskFace.cs:8`) | `void MaskConstantEffect(GameObject wearer);` | Cigarette |
| `Soaker.ISoakable` (`Soaker.cs:5`) | `void SoakUp(Soaker s);` | Butter |
| `Spawner.ISpawnable` (`Spawner.cs:30`) | `event Action OnDespawn;` | Usable |
| `Usable.IUsable` (`Usable.cs:70`) | `void Use();` | BuildDoorKit, Cardboard_Box, Cassette, CatFoodBowl, CerealBox, Cigarette, ClothRemover, Computer_PowerButton, DemonicContraption, Doorbell, DustBall, Egg_Carton, FireExtinguisher, Flashlight, FluidGun, Food, Fuse, FuseBox_Button, FuseBox_Slot, GasButton, Gun, GunMagazine, Key, Keyboard_Key, Knife, LaserPointer, LightSwitch, Lighter_BBQ, LiquidBomb, LiquidContainer, MaskFace, Mouse_Trap, Music_Instrument, PhotoCamera, Piggybank, PrefabContainer, Radio, RecallObjectButton, Remove_Clothing, Scissors, Seafood_Delight, SinkFaucet, SmokeDetector, Sparker, SprayBottle, TV_Remote, ThreadMarker, Toilet_Paper_Roll, VacuumCleaner, Wearable |
| `ISaveable` (`ISaveable.cs:1`) | `SaveData Save();` | 58 classes. See `save-system.md` §12. |
| `ISaveableObj` (`ISaveableObj.cs:3`) | `void Load(JToken jt);` (extends ISaveable) | 43 classes. See `save-system.md` §12. |
| `ISaveableStatic` (`ISaveableStatic.cs:1`) | `int SaveID { get; set; }` (extends ISaveableObj, ISaveable) | Door, EventConnector, GasButton, LightSwitch, Rest_Enabler, SinkFaucet, Sit_Enabler, SmokeDetector, Spawner |
| `ISaveableUsable` (`ISaveableUsable.cs:1`) | `Usable GetUsable();` `int GenerateSaveId();` (extends ISaveable) | Usable |
| `Input_Asset.IPlayerActions` (`Input_Asset.cs:284`) | `OnMove`, `OnLook`, `OnLeft_Hand`, `OnRight_Hand`, `OnPickup_Rotation`, `OnSprint`, `OnCrouch`, `OnZoom_Pickup`, `OnUse`, `OnDrop`, `OnEyelid_Menu`, `OnExit`, `OnLock_View`, and more (`InputAction.CallbackContext`) | Generated Input System code. No class-declaration implementers in this tree. The `Input_Asset.cs` matches are interface declarations. The generated callback wiring was not traced. |
| `Input_Asset.IUIActions` (`Input_Asset.cs:325`) | `OnNewaction(InputAction.CallbackContext)` | Same as above |

Notes: `Player_Abilities` lists its trigger interfaces as nested types (`Player_Abilities.IDropTriggerable`, and so on). Other nested interfaces: `Food.IEatable`, `Usable.IUsable`, `Spawner.ISpawnable`, `CharacterStories.IEncounterable`, `Computer.IInputReceiver`, `MaskFace.MaskEffectable`. The `Player_Abilities` trigger interfaces are invoked from the hand/interaction code in `Player_Abilities.cs`.

---

## 6. Enums declared by the game (verbatim)

Values are as written in the decompiled source. Explicit values are kept.

| Enum (file:line) | Values |
|---|---|
| `Location` (`Location.cs:1`) | `Diningroom`, `Kitchen`, `Entrance`, `Livingroom`, `BathroomFloor`, `Pantry`, `Bedroom_Neet`, `Bedroom_Parents`, `Lobby`, `Bathroom_1F`, `Attic`, `Lounge`, `Basement`, `Stairs_Main`, `Corridor_Main`, `Stairs_Basement`, `Stairs_Attic`, `Backyard`, `FrontYard`, `Other`, `Shed`, `Basement_Backroom`, `Dream` |
| `StatType` (`StatType.cs:1`) | `Health`, `Hunger`, `Bladder`, `Hygiene`, `Energy`, `Speed`, `Wetness`, `Knockout`, `Disgust`, `ImpactResistance`, `CuttingResistance`, `BodyType`, `Soul` (note: `BodyType` is a stat member, not its own enum) |
| `StatusEffectType` (`StatusEffectType.cs:1`) | `ticker`, `persistent`, `oneTime`, `amplifier` |
| `NoiseType` (`NoiseType.cs:1`) | `quad`, `circ`, `pixel` |
| `EffectType` (`EffectType.cs:1`) | `Impact`, `Cut`, `Burn` |
| `HeatLevel` (`HeatLevel.cs:1`) | `Cold`, `Low`, `Medium`, `High`, `Lava` |
| `Flammability` (`Flammability.cs:1`) | `Instant = 100`, `Quick = 10`, `Average = 5`, `Slow = 2`, `Resistant = 1`, `Unaffected = 0` |
| `NodeState` (`NodeState.cs:4`) | `None`, `Running`, `Failure`, `Success` |
| `Usable.StoreType` (`Usable.cs:10`, nested) | `Nowhere`, `Normal`, `BlackMarket`, `OccultMarket` |
| `PickupBank.PickupBankType` (`PickupBank.cs:8`, nested) | `All`, `Normal`, `BlackMarket`, `OccultMarket` |
| `BrainNeetAI.BrainType` (`BrainNeetAI.cs:32`, nested) | `Random`, `Segments`, `Party`, `TrickOrTreater`, `Hostile`, `Glutton`, `Thief` |
| `Hands` (`Hands.cs:1`) | `left = 0`, `right = 1`, `both = 2`, `none = -1` |
| `Liquid` (`Liquid.cs:1`) | `Dirt`, `Blood`, `Burn`, `Water`, `Cleaner` |
| `TextureSize` (`TextureSize.cs:1`) | `Texture32x32 = 0x20`, `Texture64x64 = 0x40`, `Texture128x128 = 0x80`, `Texture256x256 = 0x100`, `Texture512x512 = 0x200`, `Texture1024x1024 = 0x400`, `Texture2048x2048 = 0x800`, `Texture4096x4096 = 0x1000` |
| `TextureSpace` (`TextureSpace.cs:1`) | `texture2D`, `texture3D_Atlas` |
| `PaintDebug` (`PaintDebug.cs:1`) | `none`, `splatTex`, `worldPosTex` |
| `TVCommand` (`TV.cs:6`, nested) | `ToggleOnOff = -1`, `On = -2`, `Off = -3` |
| `ComputerButton` (`Computer_Button.cs:6`, nested) | `Open`, `Close`, `SelectNav` |
| `Stock.StockProfile` (`Stock.cs:29`, nested) | `Normal`, `Stable`, `Volatile`, `Doomed`, `RisingStar` |
| `StatusEffect.OperationType` (`StatusEffect.cs:8`, nested) | `overwriter`, `multiplier`, `linearDelta` |
| `Player_Abilities.Attire` (`Player_Abilities.cs:13`, nested) | `FullyClothed`, `Naked`, `ChestOff`, `ChestPantsOff`, `ChestShoesOff`, `PantsOff`, `ShoesOff`, `PantsShoesOff` |
| `Player_Abilities.KillMethod` (`Player_Abilities.cs:34`, nested) | `Destroy`, `Death`, `Obliterate`, `Skeletonize`, `SkeletonizeNoSkeleton` |
| `Door.Axis` (`Door.cs:9`, nested) | `X`, `Y`, `Z` |
| `FloorType` (`FoodDetector.cs:5`, nested) | `Wood`, `Carpet`, `Tile`, `Grass`, `Asphalt` |
| `Dialogue.TextAnimationType` (`Dialogue.cs:14`, nested) | `Simple` |
| `Screensaver.ScreenAnim` (`Screensaver.cs:5`, nested) | `Scroll`, `Mosaic`, `Zoom` |
| `NormalDistribution.ConfidenceLevel_e` (`NormalDistribution.cs:5`, nested) | `_60`, `_80`, `_90`, `_95`, `_98`, `_99`, `_998`, `_999` |
| `NormalDistribution.Direction_e` (`NormalDistribution.cs:17`, nested) | `Right`, `Left` |
| `Contact_JauntMarker.JauntType` (`Contact_JauntMarker.cs:6`, nested) | `Contact`, `Damage`, `Food` |
| `EventConnector.EventKind` (`EventConnector.cs:23`, **private**, nested) | `OnIgnite`, `OnExtinguish`, `OnCollisionEnterObj`, `OnTriggerEnterObj`, `OnCollisionExitObj`, `OnTriggerExitObj`, `External` |
| `EventConnector.EffectKind` (`EventConnector.cs:34`, **private**, nested) | `Enable`, `Disable`, `Toggle`, `Destroy`, `SetKinematic`, `RemoveKinematic`, `ToggleKinematic` |
| `DemoScript.RotationAxes` (`DemoScript.cs:7`, **private**, nested) | `MouseXAndY`, `MouseX`, `MouseY` |
| `AdvancedGizmo.GizmoAxis` (`AdvancedGizmo/Gizmo.cs:9`, **private**) | `X`, `Y`, `Z`, `XY`, `XZ`, `YZ`, `XYRotate`, `XZRotate`, `YZRotate`, `none` |
| `WorldSpaceTransitions.SurfaceType` (`WorldSpaceTransitions/FadingTransition.cs:8`) | `plane`, `sphere` |
| `WorldSpaceTransitions.FadingCentre` (`WorldSpaceTransitions/FadingTransition.cs:14`) | `gizmo`, `camera` |

Not found as enums: `BodyType` (it is `StatType.BodyType`), and `Attire` and `KillMethod` are nested in `Player_Abilities`.

---

## 7. Hard-coded magic strings

### 7a. Scene names

| Scene | Where | Context |
|---|---|---|
| `"Loading"` | `SceneSwitcher.cs:32` | `SceneSwitcher.SwitchScene` sets `SceneLoadingData.sceneNameToLoad` (`:31`), then loads `"Loading"`, which presumably loads that target (not traced) |
| `"MainMenu"` | `SceneLoadingData.cs:3` (default value), `Feedback_Sender.cs:574` (`LoadScene`) | Also the default target for `LoadSceneOnDeath` |
| `"NEET_House"` | `HouseManager.cs:339` (scene check that gates `PickupBank.Init` and `BrainBank.Init`), `DreamRandomizer.cs:47, 51`, `Nightmare1_Sequences.cs:18` (persistence target via `Commons.SetPersistOnObj`) | Main house scene |
| `"DreamZone"` | `Player_Movement.cs:325` | Dream scene |
| Ink-driven switches | `Dialogue.cs:197-203` (external functions `SceneSwitchKnockout`, `SceneSwitchInstant` take `sceneName` from the Ink story); `Dialogue.cs:245, 273` (`LoadScene(scene)`) | Scene names come from Ink, not from C# |
| `SceneSwitch.SwitchScene(int val)` | `SceneSwitch.cs:10-16` | Uses build index (`buildIndex`), not a name |
| `LoadSceneOnDeath.sceneNameToLoad` | `LoadSceneOnDeath.cs:18` | Set in the Inspector |

Equality checks on scene name: `HouseManager.cs:339` only (`GetActiveScene().name == "NEET_House"`).

### 7b. PlayerPrefs keys

| Key | Type | Where |
|---|---|---|
| `"QualityPreset"` | Int | `Feedback_Sender.cs:207-211, 322` |
| `"Framerate"` | Int | `Feedback_Sender.cs:184, 216-218, 334` |
| `"VerticalSync"` | Int | `Feedback_Sender.cs:220-222, 416` |
| `"TextureQuality"` | Int | `Feedback_Sender.cs:224-226, 429` |
| `"ShadowResolution"` | Int | `Feedback_Sender.cs:228-230, 441` |
| `"AntiAliasing"` | Int | `Feedback_Sender.cs:232-234, 453` |
| `"AnisotropicFiltering"` | Int | `Feedback_Sender.cs:236-238, 465` |
| `"Fullscreen"` | Int | `Feedback_Sender.cs:242-244, 397` |
| `"Resolution"` | Int | `Feedback_Sender.cs:255-257, 348` |
| `"Brightness"` | Float | `Feedback_Sender.cs:259-261, 478` |
| `"Bloom"` | Int | `Feedback_Sender.cs:263-265, 497` |
| `"MouseSensitivity"` | Float | `Feedback_Sender.cs:267-269, 504` |
| `"VolumeMaster"` | Float | `Feedback_Sender.cs:271-273, 511` |
| `"rebinds"` | String | `Feedback_Sender.cs:581, 595` (input rebinding JSON) |
| `"DesiredBodyType"` | Float | Written `Computer_FaceChanger.cs:123`; read `PlayerStats.cs:246` |
| (dynamic, achievement names) | Int/Float | `AchievementManager.cs:42, 74, 79, 186, 188` (key = `name` parameter) |

`Feedback_Sender.cs:279` calls `PlayerPrefs.Save()`.

### 7c. File paths

**`Application.persistentDataPath`:**

| Path | Where | Purpose |
|---|---|---|
| `neetSave_<datetime>.json` | `SaveLoadHandler.cs:113-117` | Save file |
| `neet*` prefix scan | `SaveLoadHandler.cs:146, 160`; `Feedback_Sender.cs:542-543` | Load and reset |
| `Textures/PaintTargetSplats/<saveId>.png` | `SaveLoadHandler.cs:127, 451-455`; `PaintTarget.cs:91` | Paint state |
| `Textures` folder | `Feedback_Sender.cs:545-548` | Deleted by reset |

Note: `SaveLoadHandler.cs:451` and `PaintTarget.cs:91` use `\\` separators, so this path is written for Windows.

**`Application.streamingAssetsPath` (and `Application.dataPath + "/StreamingAssets/..."`):**

| Subfolder / file | Where | Purpose |
|---|---|---|
| `Faces/Face.png`, `Faces/Face.jpg`, `Faces/Face<datetime>.*` | `Computer_FaceChanger.cs:63-128` (includes `:107` `dataPath + "/StreamingAssets/Faces/Face.jpg"`); `PlayerStats.cs:455` | Player face texture (archives old face by date) |
| `CustomDesigns/` | `CustomImageLoader.cs:35, 55`; `CustomItemShop.cs:71` | User-imported images |
| `CustomAudio/` | `CustomItemShop.cs:60` | User-imported audio |
| `<storedImagePath>.jpg` | `Picture.cs:51-53` | Photo stored on a picture |
| `photograph_<n>.jpg` under `dataPath/StreamingAssets/` | `PhotoCamera.cs:31` | Camera output |

Note: the streaming-assets code mixes `\\` and `/`. Backslash: `Computer_FaceChanger.cs:63-100`, `CustomImageLoader.cs:35`, `CustomItemShop.cs:60, 71`. Forward slash: `Computer_FaceChanger.cs:107, 128`, `CustomImageLoader.cs:55`, `PlayerStats.cs:455`, `Picture.cs:51-53`, `PhotoCamera.cs:31`. Unity accepts either on Windows.

**`Resources.Load` paths.** Resource names are relative to a `Resources` folder and have no extension:

| Prefix / name | Examples (file:line) |
|---|---|
| `Textures/` | `Textures/Smoke15Frames` (`BrainAgentCop.cs:80`, `Gun.cs:74`, `FireSpreader.cs:72`); `Textures/FootPrint_Boot1` (`FootprintPainter.cs:37`); `Textures/"+persistentTexturePath` (`CustomTextureSaveLoader.cs:29, 77`); `Textures/"+storedImagePath` (`Picture.cs:57`) |
| `Audio/` | `Audio/Steam_Burst_Short`, `Audio/Steam_Burst_Long` (`HeatHolder.cs:89-90`); `Audio/CorpseExplosion`, `Audio/Meat_Squish` (`ExtensionMethods.cs:114-115`); `Audio/"+persistentMusicPath` (`CustomMusicSaveLoader.cs:32`) |
| `Materials/` | `Materials/Burntfood` (`Usable.cs:673`), `Materials/Shitty` (`Wearable.cs:57`) |
| Prefabs / effects | `FireSpreaderSphere` (`FireSpreader.cs:67`); `FireParticle`, `FireParticle_Small` (`Flame.cs:284, 318`); `SpraySphere_*` (`Water.cs:24-39`, `Flame.cs:465`, `SkeletonParasite.cs:155`, `BrainRat.cs:369`, `Player_Abilities.cs:693`); `Particle_BloodBurst` (`Commons.cs:188`); `DarkFogParticle` (`HouseManager.cs:197, 234`); `CopVan` (`HouseManager.cs:535`); `DripEffect` (`PaintTarget.cs:779`); `Flies_Particle` (`Food.cs:202`); `Jaunt_Marker` (`Player_Abilities.cs:280`); `Particle_BloodWind` (`Player_Abilities.cs:1886`) |
| Other | `Face` (`Computer_FaceChanger.cs:108`); `splats` (`PaintExample.cs:46`, `PaintCamera.cs:22`); `WaterRainbow` material (`HouseManager.cs:271`) |

Scripted, data-driven loads: `Picture.cs:57`, `CustomTextureSaveLoader.cs:29, 77`, `CustomMusicSaveLoader.cs:32`, and `FootprintPainter.cs:222` (`texName`) all build the resource name from saved data.

### 7d. Tags and layers

**Tags** (only two found):
- `"Player"`: `Teleporter.cs:13` (`other.tag == "Player"`), `Phone_Interface.cs:182` (`CompareTag("Player")`).
- Object name checks (not tags): `"Player"` in `PlayerStats.cs:244`, `"Door"` in `Key.cs:17, 21`, `"MirrorMaterial"` in `MirrorCameraScript.cs:37`, `"SpraySphere_Wind(Clone)"` in `Candle_Small.cs:60, 68`.

**Layers** (names passed to `LayerMask.NameToLayer` / `LayerMask.GetMask`):

| Layer | Used in |
|---|---|
| `Default` | `LayerMask.GetMask` combinations; `NameToLayer("Default")` x3 |
| `Construction` | Most-used mask (for example `GetMask("Construction")`) |
| `Furniture` | Masks; `NameToLayer("Furniture")` x2 |
| `Usable` | Most-used (`NameToLayer("Usable")` x6; many `GetMask`) |
| `Player` | `GetMask("Player")` x4 |
| `NotPlayer` | `FluidGun.cs:129` (`NameToLayer("NotPlayer")`); masks |
| `Location` | `NameToLayer("Location")` x6; masks |
| `Fire`, `NotFire` | Masks (for example `GetMask("Default", "Construction", "Fire")`) |
| `Nothing` | `PaintTarget.cs:542` (`renderCamera.cullingMask = NameToLayer("Nothing")`) |
| `PostProc` | `CameraControl.cs:122` (`procVolume.gameObject.layer`) |

Layer names are defined in Unity's Tag and Layer settings, which are not in the decompiled tree. The masks above are as written in code.
