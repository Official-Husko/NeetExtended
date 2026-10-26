# AI: brains & behaviour trees

Scope: `Brain` and subclasses, the `BT*` behaviour-tree library, `NodeState`, `AITarget`, story/segment data, and the encounter/creature scripts listed in the area brief. All citations are relative to the project root and point at `decompiled/Assembly-CSharp/*.cs`. Line numbers are from the decompiled sources as they exist now. "Inferred" marks anything not directly visible in code (for example, which prefab uses which class, which lives in Unity assets that are not decompiled).

## Overview

- **Brain** (`Brain.cs:9`) is an abstract `MonoBehaviour` that every NPC/creature with AI derives from. It owns a `BTRoot bt` (behaviour tree), an `IntentHolder intents` (the same input-intent system the player uses), a `saveId`/`prefabName` pair for save/load, and location memory of rooms it has visited.
- **Subclasses**: `BrainNeetAI` (generic NPC, 7 behaviour modes), `BrainPlayer` (the human-controlled body), `BrainPlayerHijack` (a helper component, not a Brain), `BrainCat`, `BrainRat`, `BrainAgentCop`, `BrainTrickOrTreater`, `BrainWithSegments`, `DeliveryGuy`, `SkeletonParasite`, `Spider_House`. `Spider_Frontyard`, `Mouse_Hole`, `Mouse_Trap`, `CopVan`, `ThiefHandler`, `Reaper`, `Succubus`, `QueenOfDust`, `MushroomBoy`, `NeetCloneSpawner`, `Pigbuddy` and `NPCNavigationTest` are plain `MonoBehaviour`s and are not brains.
- **Trees are built in code, not in the inspector.** Each brain's `Start()` (or `BrainFactory`) constructs a `new BTRoot(...)` out of `BT*` node objects that hold delegates to private methods. The Unity prefab supplies only data fields (`brainType`, `segmentLines`, `chances`, `pathingDistCheck`, `initFoodBowl`, audio clips and so on). Serialized inspector data never describes tree shape.
- **Ticking**: each brain calls `bt.Tick()` from `Update()`. `BTRoot.Tick()` (`decompiled/Assembly-CSharp/BTRoot.cs:22`) runs `Eval()` on its children once per frame. Timers inside nodes use `Time.deltaTime` and only advance when their node is actually evaluated, which means a node skipped by an earlier composite does not advance its timer.
- **Saving**: `Brain.Start()` subscribes `Save` to `SaveLoadHandler`. Each brain writes a `SaveDataBrain` (common fields, including the serialized tree) plus its own fields. On load, `SaveLoadHandler.LoadBrains` (`decompiled/Assembly-CSharp/SaveLoadHandler.cs:302`) instantiates the prefab from `BrainBank.brainsDict`, sets `saveId` and name, loads sub-saveables, calls `Brain.Load`, then calls `Start()` manually, then `bt.Load(behaviorTree)`.
- **Tree state** is saved per node (`BTNode.Save`, `BTNode.cs:138`) as a JSON tree that mirrors the shape of the in-memory tree. It stores `nodeName`, `depth`, `treeIndex`, `nodeState` (actually `lastNodeState`), plus per-class timers and flags. The **shape of the tree in code is the schema**. Loading maps saved children to live children by index (`BTNode.cs:143-165`).

## Brain base class

Source: `decompiled/Assembly-CSharp/Brain.cs`.

### Fields

| Field | Line | Notes |
|---|---|---|
| `Location currentLocation` | 70 | `[SerializeField]`. Set from `Locator.roomId` on trigger enter. |
| `List<LocationMemory> itemLocationMemories` | 72 | Per-room snapshot of `Locator.GetRoom(loc).usables` plus a timestamp. |
| `Action<Location> OnLocationChange` | 74 | Fired on room change. `BrainPlayer` uses it for outside/thunder effects. |
| `IntentHolder intents` | 76 | Created in `Start()` (`Brain.cs:140-141`). Drives the body, the same way player input does. |
| `string prefabName` | 78 | **Not assigned anywhere in the decompiled code.** Must come from the prefab. See Modding notes. |
| `int saveId` | 80 | Assigned lazily in `GenerateSaveId()` (`Brain.cs:113-120`) from `SaveLoadHandler.IdSequence` when 0. |
| `BTRoot bt` | 82 | Set by each subclass. |
| `bool alreadyStarted` | 84 | Guard used by subclasses' `Start()`. |
| `bool pauseBT` | 86 | Read by `BrainCat`/`BrainRat` in `Update()` to stop ticking. Also set by `BrainPlayerHijack` (`BrainPlayerHijack.cs:23`). |
| `bool disableOnStart` | 88 | If true, `Start()` deactivates the object after 0.2 s. |
| `Action OnDestroyAction` | 90 | Run from `OnDestroy()`. `HouseManager.StatCheck` uses it to decrement spawn counts (`HouseManager.cs:430-437`). |

### Lifecycle

1. Unity `Awake()` (subclass-specific; for example `BrainNeetAI.Awake`, `BrainRat.Awake`).
2. `Start()`. The base version (`Brain.cs:134-155`) subscribes `Save` to `SaveLoadHandler`, creates `intents`, generates `saveId`, sets `bt.owner = this`, and applies `disableOnStart`. Subclasses build `bt` before calling `base.Start()`, but they differ in ordering. `BrainNeetAI` guards with `alreadyStarted` and builds the tree from `BrainFactory(brainType)` (`BrainNeetAI.cs:247-275`).
3. `Update()`: base version (`Brain.cs:157-164`) drops the most recent room memory after 20 s if the player is still in that room, then re-analyses the room inventory. Subclass `Update()` methods call `bt.Tick()`, sometimes gated by `pauseBT`.
4. `OnTriggerEnter` (`Brain.cs:176-187`) on the `Location` layer updates `currentLocation`, runs `AnalyzeRoomInventory` after `WaitForEndOfFrame`, and fires `OnLocationChange`.
5. `OnDestroy()` (`Brain.cs:122-132`) unsubscribes `Save` and runs `OnDestroyAction`. Destroying a brain therefore removes it from future saves.

Hiding caveat: several subclasses declare `private new void Update()` (`BrainCat.cs:128`, `BrainRat.cs:144`, `BrainAgentCop.cs:93`, `BrainTrickOrTreater.cs:73`, `BrainPlayer.cs:105`, `DeliveryGuy.cs:61`). That hides `Brain.Update`, so the room-memory housekeeping does not run for them. `BrainRat` also declares its own private `OnTriggerEnter` (`BrainRat.cs:332`), which hides `Brain.OnTriggerEnter`. I have not verified how Unity dispatches hidden private message methods, so whether `Brain.OnTriggerEnter` still runs for rats is unconfirmed.

### Save data

`Brain.SaveDataBrain` (`Brain.cs:28-67`, derives from `SaveData`):

| Field | Meaning |
|---|---|
| `type` | `SaveData.type`. Set to `GetType().ToString()` in `SaveBasicBrain` (`Brain.cs:104`), which is the concrete class name. |
| `saveId` | Stable id, used by `loadedBrainsById`. |
| `prefabName` | Key into `BrainBank.brainsDict`. |
| `instanceName` | `base.name` at save time. `LoadBrains` restores it as `gameObject.name` (`SaveLoadHandler.cs:321`). |
| `behaviorTree` | `JObject` from `bt.Save()`. |
| `position`, `rotation`, `scale`, `velocity` | Transform and rigidbody velocity. Velocity is `-1*Vector3.one` when there is no Rigidbody. |
| `subSaveData` | Save output of every `ISaveable` in the children, except this brain and `Usable` (`Brain.cs:104-106`). |
| `disableOnStart` | As above. |

`SaveBasicBrain()` (`Brain.cs:97-107`) first calls `bt.GetAllChildren()` and `bt.AssignTreeIndexes()` so node indexes are fresh.

Per-class save wrappers. Each returns an object whose top-level `type` is `"Brain"`, so `GetTokensByType(jObject, "Brain")` finds it (`SaveLoadHandler.cs:165`). This string is required, not cosmetic.

| Class | Wrapper | Extra fields | Source |
|---|---|---|---|
| `BrainNeetAI` | `SaveDataBrainNeet` | `currSegmentIndex`, `currentSegmentLineName`, `dontLoadWig` | `BrainNeetAI.cs:12-29`, `131-133` |
| `BrainPlayer` | `SaveDataBrainPlayer` | none | `BrainPlayer.cs:8-17` |
| `BrainCat` | `SaveDataBrainCat` | `dead`, `pauseBT`. (`canSpawnRatGang` exists on the class but `Save` never passes it.) | `BrainCat.cs:9-26`, `66-69` |
| `BrainRat` | `SaveDataBrainRat` | `dead`, `skinned`, `pauseBT`, `canSpawnRatGang` | `BrainRat.cs:12-33`, `77-80` |
| `BrainAgentCop` | `SaveDataBrainAgentCop` | `enabled` (= `activeInHierarchy`) | `BrainAgentCop.cs:11-23`, `57-60` |
| `BrainTrickOrTreater` | `SaveDataBrainTrickOrTreater` | `hasAnswer` | `BrainTrickOrTreater.cs:9-21` |
| `BrainWithSegments` | `SaveDataBrainTrickOrTreater` (copy-paste name) | none. **`currSegmentIndex` is not saved.** | `BrainWithSegments.cs:10-20`, `39-42` |
| `DeliveryGuy` | `SaveDataBrainDeliveryGuy` | `enabled` | `DeliveryGuy.cs:8-21` |
| `SkeletonParasite` | `SaveDataSkeletonParasite` | `navEnabled` | `SkeletonParasite.cs:11-24` |
| `Spider_House` | `SaveDataBrainSpiderHouse` | `enabled` | `Spider_House.cs:7-20` |

`SaveLoadHandler.LoadBrains` (`SaveLoadHandler.cs:302-358`) runs in this order: destroy all existing `Brain` objects, sort so the `Player` instance loads first, instantiate `BrainBank.brainsDict[prefabName].obj` at the saved position, set `saveId`, scale, name and velocity, load `ISaveableObj` children by type-name match, call `Brain.Load(token)`, call `Start()`, call `bt.Load(...)`, then register `loadedBrainsById`. Duplicate `saveId`s are skipped and logged.

### Movement and navigation

- Brains use `UnityEngine.AI.NavMeshAgent` for some subclasses (`BrainCat`, `BrainRat`, `BrainAgentCop`, `BrainTrickOrTreater`, `DeliveryGuy`, `SkeletonParasite`). They call `nav.SetDestination(...)` directly.
- `BrainNeetAI` does not use the NavMeshAgent to drive the body. It computes its own path with `NavMesh.CalculatePath` (`BrainNeetAI.cs:540`), stores corners, and writes `intents.MoveForward`, `LookLeftRight`, `LookUpDown` and `Crouch` each frame (`BrainNeetAI.cs:501-528`). It also opens doors it meets on the path with `Door.Slam()` (`BrainNeetAI.cs:508-515`).
- `Brain.GetValidNavmeshDestination(Vector3 tgt, float randomOffset = 0f, Location roomBoundsToClampNavPosWith = Location.Other, string callerName = "")` (`Brain.cs:227-259`) projects a point onto the navmesh (`NavMesh.SamplePosition`, radius 2 m). With a room, it clamps to the nearest box-collider bounds of that room, trying 5 steps.
- `Locator.GetRandomPositionInRoom(...)` supplies random room points (used by `FindRandomDestination*`).
- `Brain.GetRandomItemInCurrentRoom()` (`Brain.cs:199-207`) uses `Random.Range(0, Count - 1)`, which excludes the last element.

### Targets: AITarget and BTTargetRef

`AITarget` (`decompiled/Assembly-CSharp/AITarget.cs:124`) is a `[Serializable]` holder for one of:
- a `GameObject` (`Obj`), in which case `Position` reads the live transform,
- a fixed world position (`tgtPos`) when no object is set, or after the object is destroyed (`AITarget.cs:165-173`).

Gotchas:
- Setting `Position` while `Obj` is set **moves that GameObject** (`AITarget.cs:177-179`). It does not just update the target.
- `Rigid` setter (`AITarget.cs:157-162`) dereferences the argument. Passing null throws.
- `Obj = null` leaves the old `tgtPos` in place. `Position` then returns that stale value.

`BTTargetRef` (`BTTargetRef.cs:210-233`) is a leaf that calls a `Func<AITarget>` finder and passes a non-null result to an `Action<AITarget>` setter, returning Success. It is the usual way to refresh a brain's field, for example `new BTTargetRef(FindRandomDestInroom, SetPickupTgtObj)`. The finder runs as a side effect on every evaluation, so finders such as `FindRandomDestination` also call `InitiateFirstWaypoint` (`BrainNeetAI.cs:283-288`).

### Hijacking

- `IBrainHijackable` (`decompiled/Assembly-CSharp/IBrainHijackable.cs:1-4`) is a one-method interface, `void Hijack()`.
- Only `BrainRat` implements it (`BrainRat.cs:9`, `440-452`). `Hijack()` creates a `HijackCamera` child with a `Camera` and `AudioListener`, disables the NavMeshAgent, sets `HouseManager._.player` and `mainCamera` to the rat, and adds a `BrainPlayerHijack`.
- `BrainPlayerHijack` (`decompiled/Assembly-CSharp/BrainPlayerHijack.cs`) pauses the original brain (`pauseBT = true`, line 23) and feeds its `intents` from the player's input (`Update`, lines 27-47). `FixedUpdate` applies force and snaps the body to the floor with a 0.1 m ground ray (lines 49-71).
- **No caller of `Hijack()` exists in the decompiled code.** Nothing un-hijacks, either. Whatever triggers it is outside this area or dead code (see Open questions).

## Behaviour tree library

Every class below is in `decompiled/Assembly-CSharp/`. "Category" is composite (multiple children, picks among them), decorator (one child, changes or times its result), or leaf (no children, does the work or tests a condition).

Common contract: `Eval()` returns a `NodeState` (`NodeState.cs:8-14`): `None`, `Running`, `Failure`, `Success`. `BTNode.NodeState` also keeps `lastNodeState` and a colour for the optional debug overlay.

| node | category | semantics | constructor |
|---|---|---|---|
| `BTNode` (abstract) | base | Holds `nodes`, `nodeName`, `depth`, `treeIndex`, `parentNode`. Implements `Save`/`Load` and index assignment. `BTNode.cs:41`. | n/a |
| `BTRoot` | root | Runs every child every tick in order. Always returns Success, or Failure if there are no children. Owns `owner` (the Brain) and `Tick()`. `BTRoot.cs:8-45`. | `BTRoot(params BTNode[] newNodes)` |
| `BTSequence` | composite | Runs children in order. Failure resets and returns Failure. Running saves `lastRunningIndex` and resumes there next tick. `BTSequence.cs:6-54`. | `BTSequence(params BTNode[] newNodes)` |
| `BTSelector` | composite | Returns the first child that is Success or Running. Otherwise Failure. Stateless, so it restarts from child 0 every tick. `BTSelector.cs:5-30`. | `BTSelector(params BTNode[] newNodes)` |
| `BTReactSequence` | composite | Like a sequence, but no memory. Each tick it re-evaluates from child 0. Failure or Running returns immediately. Saves `lastRunningIndex` but never reads it. `BTReactSequence.cs:6-51`. | `BTReactSequence(params BTNode[] newNodes)` |
| `BTParallel` | composite | Evaluates all children every tick. Always returns Success. `BTParallel.cs:6-36`. | `BTParallel(params BTNode[] newNodes)` |
| `BTParallelOneSuccess` | composite | Evaluates all children. Success if any returned Success, otherwise Failure. `BTParallelOneSuccess.cs:6-36`. | `BTParallelOneSuccess(params BTNode[] newNodes)` |
| `BTRandomPick` | composite | Each evaluation picks one child by cumulative `chances[i]` against a uniform roll, then returns that child's result. `BTRandomPick.cs:7-43`. | `BTRandomPick(float[] newChances, params BTNode[] newNodes)` |
| `BTInverter` | decorator | Success becomes Failure, Failure becomes Success, Running passes through. `BTInverter.cs:5-23`. | `BTInverter(BTNode newNode)` |
| `BTSucceed` | decorator | Evaluates its child if non-null, then always returns Success. `BTSucceed.cs:5-24`. | `BTSucceed(BTNode newNode)` |
| `BTRng` | decorator | Each tick, with probability `chance`, evaluates the child. Otherwise Failure. This is a per-tick roll, not a one-shot. `BTRng.cs:7-39`. | `BTRng(float newChance, BTNode newNodes)` |
| `BTCooldown` | decorator | While cooling down, returns `cdState` without running the child. When the timer expires, runs the child and re-arms to `timer ± randOffset`. Without a child, returns Success on expiry. `startMaxedCooldown` starts it cooling. `BTCooldown.cs:7-63`. | `BTCooldown(float newTimer, float randOffset, NodeState newCdState, BTNode newNode, bool startMaxedCooldown = false)` |
| `BTSustainer` | decorator | Runs the child, then holds its last result for `timer ± randOffset` seconds before running it again. `BTSustainer.cs:7-63`. | `BTSustainer(float newTimer, float randOffset, BTNode newNode)` |
| `BTSustainerOn` | decorator | Runs the child until it returns `sustainOn`. Then holds `sustainOn` for the timer, then re-runs the child. Any other result means it keeps re-running the child every tick. `BTSustainerOn.cs:7-73`. | `BTSustainerOn(NodeState sustainOn, float newTimer, float randOffset, BTNode newNode)` |
| `BTAccumulator` | decorator | The child must return Success. While it does, accumulates `Time.deltaTime`. Once the accumulated time reaches `timer ± randOffset`, returns Success and resets. Any non-Success resets and returns Failure. Throws if it has no child. `BTAccumulator.cs:7-58`. | `BTAccumulator(float newTimer, float randOffset, BTNode newNode)` |
| `BTWait` | decorator (child optional) | Returns `waitState` (default Running) while counting. On expiry, runs the child (or returns Success if none) and re-arms. `BTWait.cs:7-69`. | `BTWait(float newTimer, float randOffset = 0f, NodeState newWaitState = NodeState.Running, BTNode newNode = null)` |
| `BTConditional` | leaf | Returns the predicate's `NodeState`. `BTConditional.cs:4-19`. | `BTConditional(Func<NodeState> newPredicate)` |
| `BTConditionalElse` | leaf (else branch is outside `nodes`) | Returns the predicate result. If the predicate is Failure, also evaluates `elseNode`, but still returns Failure. `BTConditionalElse.cs:4-26`. | `BTConditionalElse(Func<NodeState> newPredicate, BTNode elseNode)` |
| `BTAnd` | leaf | Success only if both predicates return Success. Short-circuits. `BTAnd.cs:4-29`. | `BTAnd(Func<NodeState> p1, Func<NodeState> p2)` |
| `BTOr` | leaf | Success if either predicate returns Success. Short-circuits. `BTOr.cs:4-29`. | `BTOr(Func<NodeState> p1, Func<NodeState> p2)` |
| `BTState` | leaf | Returns a fixed `NodeState`. `BTState.cs:18-32`. | `BTState(NodeState st)` |
| `BTAction` | leaf | Returns the delegate's result. With `alwaysSucceed`, forces Success. `BTAction.cs:4-26`. | `BTAction(Func<NodeState> newAction, bool alwaysSucceed = false)` |
| `BTTargetRef` | leaf | Calls a finder for an `AITarget`. If non-null, passes it to the setter and returns Success, otherwise Failure. `BTTargetRef.cs:210-233`. | `BTTargetRef(Func<AITarget> newFinder, Action<AITarget> newSetter)` |
| `BTActionTgt` | leaf | Gets an `AITarget`. Null gives Failure, otherwise returns `actionTargeted(target)`. Saves `tgtObjId` (Brain or Usable `saveId`, or -1) and `tgtPos`. `BTActionTgt.cs:6-120`. | `BTActionTgt(Func<AITarget,NodeState> newActionTargeted, Func<AITarget> newGetTarget, Action<AITarget> newSetTarget)` |
| `BTActionFixedTgt` | leaf (coroutine) | Like `BTActionTgt`, but runs the action every `FixedUpdate` in a coroutine started on the agent. Returns Running until the action returns Success or Failure. Used by `BrainRat` to drag food. `BTActionFixedTgt.cs:5-76`. | `BTActionFixedTgt(Brain newAgent, Func<AITarget,NodeState> newAction, Func<AITarget> newGetTarget, Action<AITarget> newSetTarget)` |
| `BTMoveTo` | leaf | Gets the target. Null or invalid gives Failure. Calls `actionTargeted(tgt)` as the "drive" step. Failure aborts. Then checks distance from the agent (plus `verticalOffset`). Within `minSuccessDist` it runs `onDestReached` and returns Success, otherwise Running. `BTMoveTo.cs:6-126`. | `BTMoveTo(GameObject newAgent, Func<AITarget,NodeState> newActionT, Func<AITarget> newGetTarget, Action<AITarget> newSetTarget, float minSuccDist = 1f, float verticalOffset = 0f, Action onDestReached = null)` |

Notes on the table:
- `BTAnd`/`BTOr` store predicates as `Func`, not child nodes, so they have no saved state of their own.
- `BTConditionalElse.elseNode` is **not added to `nodes`**. That means it is missing from `GetAllChildren`, `AssignTreeIndexes`, `Save` and the debug UI. The else subtree's state is never saved.
- Every timer node (`BTWait`, `BTCooldown`, `BTSustainer`, `BTSustainerOn`, `BTAccumulator`) saves its counters. Note that `BTWait.isSetuped` is not saved, so it re-randomises after a load.

### How BTRoot evaluates

`BTRoot.Eval()` (`BTRoot.cs:34-45`) loops over its direct children, calls `Eval()` on each, and always returns Success. It does not stop on Failure or Success. Multiple top-level children therefore all run every frame. `BrainNeetAI` relies on this for the Thief tree, whose first child is an action that sets crouch and sprint each frame (`BrainNeetAI.cs:204-208`).

`BTRoot.Tick()` (`BTRoot.cs:22-32`) first calls `ResetAndDrawLine()` on `allNodes`. That list is only populated by `GenerateUITree()`, which is used for the debug overlay (`showDebugUI`). Without the overlay, `Tick()` is just `Eval()`.

### Composing a tree in code

The constructors above take `params BTNode[]` for composites. Method groups and lambdas stand in for `Func<...>`/`Action<...>`. This snippet is illustrative and uses only the real constructor signatures. The helper methods are placeholders.

```csharp
bt = new BTRoot(
    new BTSelector(
        new BTSequence(
            new BTConditional(HasNeetTarget),                              // Func<NodeState>
            new BTMoveTo(base.gameObject, MoveTo, GetTgt, SetTgt, 1.5f),  // drive, then distance check
            new BTAction(Punch)),
        new BTCooldown(5f, 2f, NodeState.Failure, new BTAction(Dance))));
```

Setup conventions used in the code:
- Build the tree in `Start()` before `base.Start()` (for example `BrainCat.cs:96-115`), then set `bt.owner = this`.
- Store the target in a field, and let `BTTargetRef` refresh it each tick, as `BrainNeetAI` does.
- Use a `BTSustainerOn`/`BTCooldown` around expensive or flappy checks.

## NPCs & creatures

### BrainNeetAI (generic NPC)

Source: `decompiled/Assembly-CSharp/BrainNeetAI.cs`.

The NPC is a full player body. It has `Player_Abilities`, `Player_Movement`, `PlayerStats`, and an `IntentHolder`. It acts by writing to intents, so it moves, picks up and uses things through the same code paths as the player. `Awake` caches those components (`BrainNeetAI.cs:233-245`). `Update()` calls `base.Update()`, then `intents.ResetIntents()`, then `bt.Tick()` (`BrainNeetAI.cs:395-400`).

`BrainType` (`BrainNeetAI.cs:32-41`) is a serialized enum choosing the tree built by `BrainFactory` (`BrainNeetAI.cs:153-230`). The tree is built once in `Start()` (`BrainNeetAI.cs:255`).

| BrainType | Behaviour (from the tree) | Location |
|---|---|---|
| `Random` | Wanders. Sometimes picks up a random object in the room, sometimes uses or throws it. Sustained 15 s cooldown and a 30 s accumulator gate pickups. Chooses a random room destination. | `BrainNeetAI.cs:157-164` |
| `Hostile` | If `spottedNEET` (see below), chases within 3 m and punches on a `1 ± 0.5 s` cooldown, raising a hand (`Punch`, `KeepRaisedHand`). Otherwise looks for a Cutter or Impacter (`FindWeapon`) and uses it. | `BrainNeetAI.cs:166-172`, `758-763` |
| `Glutton` | Goes for `Food` first. `UseHandDecider` (`BrainNeetAI.cs:174-195`) picks a held food item to eat. Otherwise drops an empty container. | `BrainNeetAI.cs:173-202` |
| `Thief` | Crouches while not spotted, sprints once spotted. Hunts items priced over 250 (`FindExpensiveItems`, `BrainNeetAI.cs:390-393`). When spotted or holding items it moves to the curb and calls `DeactivateWithHeldPickups`, which destroys the NPC. | `BrainNeetAI.cs:203-215`, `765-774` |
| `Segments` | Walks to a scripted segment target, faces the segment's rotation, and rings the doorbell with a 1e8 s sustain. Used for story NPCs, driven by `segmentLines` (see Character stories). | `BrainNeetAI.cs:216-217`, `649-650` |
| `Party` | Wanders and dances (`Dance`, a random `Dancing` animation value) on a 20 s sustain. | `BrainNeetAI.cs:218-225` |
| `TrickOrTreater` | Walks to a door target, rings the doorbell, and waits for an answer (`hasAnswer`). On answer it walks to the target, calls `NotifyEncounterSpawner` and `Deactivate`. Its health `OnSubZero` also calls `EncounterSpawner._.SetupNextEncounter()` (`BrainNeetAI.cs:256-263`). | `BrainNeetAI.cs:226-227`, `634-638`, `803-812` |

Shared behaviour:
- `MoveTo` (`BrainNeetAI.cs:493-536`) recalculates its path every 0.2 s. It fails when a locked door blocks the path (`510-512`) and when `Stuck()` exceeds 1.5 s (`530-534`). `Stuck()` (`579-601`) counts time when the body is more than 0.35 m from the goal and its velocity is under 1 m/s.
- `pathingDistCheck` is 0.3 m by default (`BrainNeetAI.cs:59`). Tree branches scale it, for example `×10` for chase and `+0.5`/`+1` for item fetch.
- Waypoint reach distance is 0.3 m (`BrainNeetAI.cs:554`).
- Doors on the path are found with a raycast per corner (`GetDoorsToNextWaypoint`, `565-577`) and slammed when within 2.3 m (`508-515`).
- `CheckForNeet` (`BrainNeetAI.cs:694-717`) overlaps a box 2.5 m to the NPC's left, raycasts to each player, and sets `spottedNEET` if the player is moving fast (sqrMagnitude > 1.5) or hit an `ActorStats`.
- `HasAnswer`/`hasAnswer` is set by `CharacterStories.Leaving` (`CharacterStories.cs:268-277`).
- Save state: `currSegmentIndex`, `currentSegmentLineName`, `dontLoadWig`, plus the tree.
- Needs: this class does not read hunger, energy, or other `PlayerStats` needs. Stat-driven spawns are a separate system (`HouseManager.StatCheck`, see Encounters below).
- `killCount` is read in `KilledTarget` but never incremented in this file.
- `RecordSegmentData` (`BrainNeetAI.cs:126-129`) is a context-menu helper that appends the current position to the last segment line.

### BrainPlayer

Source: `decompiled/Assembly-CSharp/BrainPlayer.cs`. The human-controlled body. `Update()` (`BrainPlayer.cs:105-133`) copies every intent from `HouseManager._` input actions. `Awake` (`33-48`) claims `HouseManager._.player` and `mainCamera` if none is set, and disables the camera and audio listener on any other BrainPlayer. Its `Load` only deserialises and does nothing else (`BrainPlayer.cs:28-31`).

### BrainPlayerHijack

Source: `decompiled/Assembly-CSharp/BrainPlayerHijack.cs`. A helper that drives another brain from player input. See Hijacking above.

### BrainCat

Source: `decompiled/Assembly-CSharp/BrainCat.cs`. Stats: `CatStats` (`CatStats.cs:4-34`).

- `CatStats`: `health = new Health(500f)`. `hunger` and `bladder` decay at `-0.1` and `-0.09` per second (`CatStats.cs:26-27`). Health reaching zero calls `brainCat.Die` (`CatStats.cs:21`). Incoming damage is ignored (`DamageCat` returns `potency`, lines 30-33).
- Tree (`BrainCat.cs:96-113`), in priority order:
  1. If a rat is detected (`DetectRats`, box overlap ahead on `Usable`/`NotPlayer`, and the rat is an enabled `BrainRat` or in a LaserPointer), chase it and `KillRat` (`196-205`): health loss of `6–17`, plus `+10` hunger.
  2. If bladder is below 5, meow and poop. If below 30, walk to the litter box, meow, and poop. `Poop` adds 100 bladder and drops a turd prefab (`338-343`).
  3. If hunger is below 25, walk to the food bowl (`HouseManager._.catFoodBowl`), meow, eat (`EatFromBowl` adds 80 hunger and decrements `FoodUnits`), and purr.
  4. If the player is near for 4 s (`DetectPlayer`, an overlap box with half-extent 1.5 m in front of the cat), purr, walk within 1.8 m, stop, wait 10 ± 4 s, and meow.
  5. Otherwise wander: a random room point on a 10 ± 2 s cooldown, then wait 6 ± 1 s.
- `Update()` only ticks while `!pauseBT`. While held (`pauseBT` set by `PickupTrigger`), it applies torque and bites the holder (`BrainCat.cs:128-153`).
- Save: `dead`, `pauseBT`. On load, a dead cat is killed again with `noSound`.
- `initFoodBowl` is set to `HouseManager.mouseHoles[0]` (`BrainCat.cs:92-94`). That is the cat's "home" for the mouse-hole branch. The separate `catFoodBowl` is used for eating.
- The methods `StoreFoodInHole`, `DragObject`, `FindFoodOnFloor`, `FoodNotMoving`, `KillSelf`, `GetHomeObj` and `GetTgtPos` are not referenced by the tree in this file. They look copied from `BrainRat`.

### BrainRat

Source: `decompiled/Assembly-CSharp/BrainRat.cs`. Stats: `RatStats` (`RatStats.cs:4-24`), `health = new Health(7f)`, so one good hit kills it.

Tree (`BrainRat.cs:123-129`):
1. If the player is within the overlap box, run to `initMouseHole` and `KillSelf` (despawn).
2. If it has food: wait until the food stops moving for 10 s (`FoodNotMoving` accumulator), run to the hole, then spawn a rat gang (`SpawnGangOfRat`: 2–4 rats, `BrainRat.cs:285-299`) or despawn.
3. If it has food: walk to it, drag it with `BTActionFixedTgt(DragObject)` for up to 8 s of stillness, carry it to the hole, store it (`StoreFoodInHole`, within 0.8 m, `foodCount++`, destroys the food), then roll `BTRng(0.1)` for a `KillSelf`.
4. Otherwise wander on a 10 ± 2 s cooldown.
5. Otherwise go home and `KillSelf`.

- Dying: a `Cutter` collision skins the rat (turns it into `Food`, raw 5, cooked 18, price 5, `Offering` with meat/blood/shit flags) and kills it (`BrainRat.cs:345-362`, `364-394`). `Die()` gibs it.
- `Update()` ticks the tree unless `pauseBT`. A rat that is held (`usable.player != null`) bites the player for 4–7 damage every 3–6 s (`BrainRat.cs:144-162`).
- Spawns: `Mouse_Hole` spawns rats (see Encounters).
- Hijacking: `IBrainHijackable` (see Hijacking above).
- Save: `dead`, `skinned`, `pauseBT`, `canSpawnRatGang`.
- `OnTriggerEnter` (`BrainRat.cs:332-343`) reads `foodTgt.Obj` (the food target is set by `Mouse_Hole.SpawnRat`).

### BrainAgentCop

Source: `decompiled/Assembly-CSharp/BrainAgentCop.cs`. Spawned by `CopVan` (see Encounters).

- Starts with `leaveTimer` in 120–240 s (`BrainAgentCop.cs:72`), decremented every frame (`96`). When it hits zero, or when `killCount > 0`, the cop heads back to the van and `Deactivate`s.
- Tree (`BrainAgentCop.cs:86`): mumble on a 10 ± 4 s cooldown. Patrol rooms by `PickNextRoomToExplore` (`202-221`), walking through adjacent rooms on a 9 ± 2 s cooldown. Break doors in front (`AttackNearbyDoor`, `BreakOnImpact.Impact` with 10000 force, on a 2 ± 1 s cooldown). Detect a NEET with `CheckForNeet` (`223-245`), which uses the same check as `BrainNeetAI`.
- Once a NEET is spotted, the cop checks line of sight (`HasNEETLineOfSight`, `248-267`), moves to within 1.5 m, and shoots (`ShootNeet`, `269-282`) on a 1 ± 0.5 s cooldown. Each shot has a 60% chance of dealing 15 health damage (`275-278`), fires a visual muzzle light, and paints a smoke splat.
- `CheckTargetDead` (`291-302`) increments `killCount`, calls `SetVanDestination`, and tells the van (`CopVan.InformAgentsOfKill`, `CopVan.cs:50-67`).
- `Deactivate` (`326-338`) removes the cop from its van's list and destroys it.
- `Save` stores `base.gameObject.activeInHierarchy`. `Load` calls `SetActive` with it.

### BrainTrickOrTreater

Source: `decompiled/Assembly-CSharp/BrainTrickOrTreater.cs`. A separate class from the `BrainNeetAI.TrickOrTreater` mode, which does a similar job.

- `Start()` (`BrainTrickOrTreater.cs:52-65`) builds the tree. The NPC walks to `moveTgt` (a door position set by `CharacterStories.SetDoorDestination`), rings the bell (`HouseManager._.doorbell.Use()`), and waits. When `hasAnswer` is true it walks to the target, calls `EncounterSpawner._.SetupNextEncounter()`, and calls `Deactivate` (which destroys it).
- It drives a `NavMeshAgent` with `SetDestination`. The `MoveTo` action returns Failure only if the agent is not on the navmesh (`BrainTrickOrTreater.cs:88-96`).
- `Load` restores `hasAnswer` only.
- `MushroomBoy` requires this brain (see Encounters).

### BrainWithSegments

Source: `decompiled/Assembly-CSharp/BrainWithSegments.cs`. Requires `CharacterStories` (`BrainWithSegments.cs:7`).

- `segments` is a serialized `List<StorySegment>` (`BrainWithSegments.cs:30-32`). `Start()` sets `moveTgt` from `segments[currSegmentIndex]` and builds a simple tree: if near the target (0.2 m), apply the segment's rotation (`SetDestinationReachedRot`). Otherwise `MoveTo` (`BrainWithSegments.cs:64`).
- `MoveTo` always returns Running (`BrainWithSegments.cs:107-111`). The walk never completes. Success comes only from the `NearTarget` branch.
- `IncrementSegments()` advances `currSegmentIndex`, sets `CharacterStories.OverrideKnot` to the segment's `storyKnot`, and sets `moveTgt` (`BrainWithSegments.cs:119-127`).
- `RingBell` (`BrainWithSegments.cs:145-151`) uses `Random.Range(0, 1)`, which is the int overload and always returns 0. The bell is always `AudioSource[0]`.
- **Save does not persist `currSegmentIndex`.** After a load, progress resets to 0.

### DeliveryGuy

Source: `decompiled/Assembly-CSharp/DeliveryGuy.cs`. A Brain that walks to a point (`moveTgt`, set by `PickupPackageArea`, `PickupPackageArea.cs:60-63`, `115-118`) and, when `hasPackage` is set, calls `deliveryAction` and deactivates itself (`DeliveryGuy.cs:104-113`). On first start it deactivates itself unless the game was just loaded (`DeliveryGuy.cs:53-55`).

### SkeletonParasite

Source: `decompiled/Assembly-CSharp/SkeletonParasite.cs`. A Brain with a single `BTMoveTo` toward the player's navmesh point (`SkeletonParasite.cs:89`, `97-104`, `106-116`).
- `InitAnimation` (`167-185`) scales it up, then `SetupSkull` (`187-197`) enables the navmesh agent and sets `readyToChase`.
- `Update` only ticks and pulls tentacles while `readyToChase && !possesses` (`118-133`). Tentacles apply force toward the player.
- `CheckCanPossess` (`136-165`): within 2.3 m it sets nav speed to 2. Within 1.1 m it knocks the player out for 10 s, swaps the player's mask for its own `mask`, spawns a blood spray, moves itself to y=500, sets `possesses`, unsubscribes saving, and destroys itself after 8 s.
- `SetPlayerNavPos` is empty (`102-104`).
- The save stores `navEnabled`.

### Spider_House

Source: `decompiled/Assembly-CSharp/Spider_House.cs`. A Brain.
- Tree (`Spider_House.cs:59`): every 3.1 ± 1 s, `ScanForTargets` (an overlap sphere of radius 4 m, `89-102`) looks for an enabled `Brain` that is not a spider. If one is found, a cooldown of 3 ± 1 s runs, then `Leap` (`119-133`), which jumps at the target if the line of sight is clear within 2.5 m.
- A collision with the target deals 5–8 cut damage and removes 10 Energy (`Spider_House.cs:75-86`).
- Death (`Die`, `144-151`) explodes into bones and disables the component.
- Spawned by `HouseManager.StatCheck` on low energy (see Encounters).

### Spider_Frontyard

Source: `decompiled/Assembly-CSharp/Spider_Frontyard.cs`. Not a brain. `Update` pulses the alpha of two eye sprites with `Mathf.Sin(Time.time)` (`Spider_Frontyard.cs:13-17`).

### ThiefHandler

Source: `decompiled/Assembly-CSharp/ThiefHandler.cs`.
- `thiefCountdownActive` is the on/off switch. `thiefCountdown` starts at 2 s. When it expires, it is reset to `Random.Range(2700, 5400)` and `SpawnThief` runs (`ThiefHandler.cs:16-26`). The unit is seconds because `Update` uses `deltaTime`.
- `SpawnThief` (`29-43`) picks a random room from six locations, excluding the player's current room. It instantiates `BrainBank.brainsDict["Thief"]` at a random point in that room (+1.9 m up), then breaks a window in the room (`BreakWindowInRoom`, `45-57`).
- The Thief itself is a `BrainNeetAI` with `brainType = Thief` (inferred; the prefab mapping is not in code).
- `Awake` calls `Random.Range(1700, 3600)` and discards the result (`ThiefHandler.cs:13`). It has no effect.

### Reaper

Source: `decompiled/Assembly-CSharp/Reaper.cs`. Not a brain. `Start` runs a coroutine that waits 2 s, then sets the `Reap` animator trigger (`Reaper.cs:14-37`). `Update` makes it float and tumble using sine functions. It is spawned by `HouseManager.SpawnReaper` (`HouseManager.cs:192`).

### Succubus

Source: `decompiled/Assembly-CSharp/Succubus.cs`. Not a brain. It bobs with a sine (`Update`, `23-38`), removes one audio source after 2 s, and exposes `Anim(string)` for the `Doggy`, `Doggy_Standing` and `Idle` animator triggers (`40-55`). The callers of `Anim` are not traced here.

### QueenOfDust

Source: `decompiled/Assembly-CSharp/QueenOfDust.cs`. Not a brain. On `Start` it binds the Ink external function `AbsorbDusts` to `AbsorbDustsAnimation` (`QueenOfDust.cs:31`). That collects `DustBall` colliders within 3 m (`60-62`), floats them for 4 s, pulls them to the queen over 0.2 s, destroys them, and spawns money from their total `Level` via `Piggybank.SpawnMoneyValue` (`98-109`). The flame's ignite and extinguish events toggle its emission (`28-30`, `34-48`).

### MushroomBoy

Source: `decompiled/Assembly-CSharp/MushroomBoy.cs`. Requires a `BrainTrickOrTreater` on the same object (`MushroomBoy.cs:17`). When it collides with another `Brain`, it disables its NavMeshAgent for 1.5–4 s and rolls, then re-enables the agent, lifts 0.3 m, and picks a random yaw (`MushroomBoy.cs:22-48`).

### NeetCloneSpawner

Source: `decompiled/Assembly-CSharp/NeetCloneSpawner.cs`. A `PrefabContainer`. Each spawned clone copies the player's body-type blend shape (index 3) into its `PlayerStats` `BodyType` stat (`NeetCloneSpawner.cs:9-12`).

### Pigbuddy

Source: `decompiled/Assembly-CSharp/Pigbuddy.cs`. Empty `Start` and `Update`. Nothing to document.

### NPCNavigationTest

Source: `decompiled/Assembly-CSharp/NPCNavigationTest.cs`. A navigation test harness, not part of the game. It sets a NavMeshAgent's destination to `targObj` and steers a rigidbody toward the agent's steering target (`NPCNavigationTest.cs:24-35`).

### Encounters and spawn triggers (for context only)

These are owned by the EncounterSpawner/HouseManager area, but these are the AI-relevant triggers:

- **Encounters**: `EncounterSpawner` instantiates `encounter.character` (a prefab with `CharacterStories`, usually a Brain) at `initPosition` or the roadside point (`EncounterSpawner.cs:174`). It sets `OverrideKnot` to the encounter's `specifiedStitch` (`179`). `SetupNextEncounter` (`204`) advances the sequence. `nightId` is static (`75`).
- **Stat-driven NPCs**: `HouseManager.Update` (`HouseManager.cs:380`) runs `StatCheck` every 10 s after a 120 s first delay. Low hunger spawns `"SkeletonNeetNPC"` (the class of that prefab is not confirmed). Low energy spawns `"Spider_House"`. `StatCheck` (`417-438`) increments a fail counter when a stat is below 20 and spawns with a 10% roll once the counter reaches 15. It caps total spawns at 4 and sets `OnDestroyAction` to decrement the counter.
- **Cops**: `CopVan` is created once by `HouseManager.SpawnCopVan` (`HouseManager.cs:531-537`, at fixed position `-13.02, -0.13, 6.3`). Callers: `Computer.cs:245-247` (black market or occult purchase, 20% chance), `Cardboard_Box.cs:249-251` (black market or occult box, with chance scaling with price), and `TaxBill.cs:60`. `CopVan` spawns `Random.Range(1, 4)` (1–3) cops after `Random.Range(30, 120)` seconds (`CopVan.cs:16`, `24`, `37-48`). It self-destructs 5 s after spawning when no agents are left (`CopVan.cs:31-34`).
- **Rats**: `Mouse_Hole` (`Mouse_Hole.cs:3-61`) registers itself on `Awake` (`HouseManager.mouseHoles`). Every `Random.Range(35, 65)` seconds (`51`), it spawns a rat with a food target at 4% per food item older than 1 s, or a rat with no target at 3% (`38-47`). Gang spawns come from `BrainRat`.
- **Traps**: `Mouse_Trap` (`Mouse_Trap.cs:5-80`) kills a `BrainRat` on contact (`BrainRat.Die()`) and damages an `ActorStats` for 8–12 impact. Snapping costs the owner 3–6 health (`Snap`, `61-79`).
- **Spider snapping**: `Spider_House` spawns from the energy stat check above.

## Character stories

Source: `decompiled/Assembly-CSharp/CharacterStories.cs`, `StorySegment.cs`, `StorySegmentLine.cs`, `Dialogue.cs`.

### Data model

- `CharacterStories` (`CharacterStories.cs:5`) is a component on each dialogue NPC. It has `characterName` (the Ink flow name, used for dialogue), `overrideKnot` (a stitch name appended to the knot path), `useNightId`, `useFullName`, `talkOnTriggerEnter`, `canBeIgnored` with `timeToIgnored`, and optional `bucket`, `dialogueAudio`, and callbacks `OnPlayerInRange`/`OnPlayerOutRange`. It implements `ISaveableObj`, so it saves `playerIdRef`, range/leave flags, `talkInput`, `timeToIgnored`, and `overrideKnot` (`CharacterStories.cs:105-126`).
- Registration: `Start()` (`CharacterStories.cs:222-243`) adds the object to `HouseManager._.characterStoriesDict` by name. Clones strip the `(Clone)` suffix. Duplicates are logged and ignored, so the dictionary is keyed by `characterName`.
- `StorySegment` (`StorySegment.cs:5-12`) is a struct: `AITarget target`, `Vector3 rotation`, `string storyKnot`.
- `StorySegmentLine` (`StorySegmentLine.cs:17-22`) is a struct: a `segmentLineName` and a `List<StorySegment> segments`.
- `BrainNeetAI.segmentLines` holds the lines for a `Segments`-mode NPC. `GetCurrentSegmentLine` finds the line by name with `Single` (`BrainNeetAI.cs:119-124`, so it throws if the name is missing). `InitSegments` (`614-621`) copies the current segment's `storyKnot` into `CharacterStories.OverrideKnot` and its `target` into `pickupTgt`. `IncrementSegments` (`623-632`) advances the index.
- `BrainWithSegments` keeps its own `List<StorySegment>` and does the same thing without a segment line name (see above).

### How a line is picked

1. Triggering. In `OnTriggerStay` (`CharacterStories.cs:128-151`), a dialogue starts when the player is in range and either `talkInput` is set (set from `intents.Use.Started` in `Update`, `247-250`) or `talkOnTriggerEnter` is set, and there is a line of sight (`HasSpeakerLineOfSight`, `166-182`).
2. Knot path. `Dialogue.InitCharacterDialogue` is called with `flow = characterName` and `charKnot`, built as `characterName + ("Night" + EncounterSpawner.nightId if useNightId) + ("." + overrideKnot if non-empty)` (`CharacterStories.cs:142`). With `useFullName`, `charKnot` is just the name.
3. Ink. `Dialogue.cs:144` creates `masterStory = new Story(masterScript.text)` from Ink.Runtime. `InitCharacterDialogue` (`Dialogue.cs:397-`) switches to the speaker's flow. The first time a flow is seen (not yet in `aliveFlowNames`), it also calls `masterStory.ChoosePathString(charKnot, true)`. Later visits only switch flows, so the NPC continues from where it was.
4. Reactions. `OnCollisionEnter` (`CharacterStories.cs:205-220`) opens `characterName + "_Harm"`/`"Harm"` knots when hit by a cutter, impacter or flame, and `"_Disgust"`/`"Disgust"` for meat, shit or wet offerings. Those calls use `forceChoosePath`.
5. External hooks. Ink can call C# through `BindExternalFunction` (for example `SwitchSegmentLine`, `SetOverrideKnot`, `EncounterIncrement`, `GiveItem`, `KillPlayer`, `Dialogue.cs:155-233`). Segment lines are switched by `Dialogue.SwitchSegmentLine` (`Dialogue.cs:340-358`), which also resets `currSegmentIndex = 0` and calls `InitSegments`. The line-advance path can call `BrainNeetAI.IncrementSegments` (`Dialogue.cs:750-762`).

Gotcha: `Dialogue.SwitchSegmentLine` sets `brainType = Segments` on the `BrainNeetAI` (`Dialogue.cs:349-351`), but `bt` is built only in `Start()` (`BrainNeetAI.cs:255`). **Changing the mode at runtime does not rebuild the tree.** The NPC keeps the behaviour it was spawned with, and only the segment fields change.

## Modding notes

### Adding a new NPC by cloning

- Get the template from `BrainBank.brainsDict` (`BrainBank.cs:9-42`). Each entry is a `Spawnable` struct with `obj` and `locked` (`Spawnable.cs:5-10`). The key is the prefab's `name` (`BrainBank.cs:24-26`).
- To register a new kind of NPC: clone the template, set the clone's `Brain.prefabName` to the new key, and add `BrainBank.brainsDict[newKey] = new Spawnable(clone)`. Keep the clone inactive so its `Start()` does not run before you configure it. `prefabName` must equal the key, because `LoadBrains` looks the prefab up by `saveData.prefabName` (`SaveLoadHandler.cs:318`), and `Cardboard_Box` uses the same lookup (`Cardboard_Box.cs:92-116`).
- `saveId`: `GenerateSaveId` only assigns an id when `saveId == 0` (`Brain.cs:113-120`). Clones must have `saveId` reset to 0, or they will collide on load and be skipped (`SaveLoadHandler.cs:316-355`). The sequence is saved as `maxIdSequence` (`SaveLoadHandler.cs:102`).
- `instanceName` is the GameObject's name at save time. `CharacterStories` uses the name as its dictionary key and as its Ink flow name. A new dialogue NPC needs a unique name and a matching Ink flow. The compiled story text is `masterScript.text` (`Dialogue.cs:144`). I have not found where the Ink source lives, so adding flows may require editing the story asset. This is open.
- Spawn: `Object.Instantiate(BrainBank.brainsDict[name].obj, position, rotation)` (as in `HouseManager.cs:430`, `SequenceManager.cs:173`). Choose a position with `HouseManager.GetValidSpawnPointForBrain(bool)` (`HouseManager.cs:469`).

### Writing a custom tree for a new NPC

- Subclass `Brain`. In `Start()`, build `bt = new BTRoot(...)`, set `bt.owner = this`, then call `base.Start()`. Guard with `alreadyStarted` (see `BrainCat.cs:83-117`).
- Override `Update()` to call `bt.Tick()` (use `new` to hide the base if you need to, but then the base room-memory housekeeping stops running, see the caveat above).
- Implement `Save()` and `Load(JToken)`. The top-level `type` must be `"Brain"`, or `LoadBrains` never sees the token (`SaveLoadHandler.cs:165`). Put your own state in the wrapper object, as the other brains do.
- Tree shape is the save schema. Keep node order stable across versions. `BTNode.Load` maps saved children to live children by position, and silently skips anything that fails (`BTNode.cs:143-165`).
- Delegates are not saved, only state. Anything a node needs must be reconstructed in `Start()`, which `LoadBrains` calls before `bt.Load` (`SaveLoadHandler.cs:340-344`).
- New `ISaveable` children: `SaveBasicBrain` writes them (`Brain.cs:103-106`), but `LoadBrains` only loads children that are `ISaveableObj` (`SaveLoadHandler.cs:328-337`). If you add a component, implement both interfaces. I did not read the interface definitions, so this is inferred from the call sites.

### Harmony patch targets

Harmony rewrites method bodies, so patches on named methods normally also affect the delegates the trees hold to those methods. That has not been tested here. Lambdas created inside `Start()` are compiler-generated closures, so they need their display class or a transpiler.

| Goal | Target | Notes |
|---|---|---|
| Cop spawn chance or frequency | `HouseManager.SpawnCopVan` (`HouseManager.cs:531`) and the call sites in `Computer.cs:245-247`, `Cardboard_Box.cs:249-251`, `TaxBill.cs:60` | Prefix to force or block. |
| Cop group size and timing | `CopVan.Start` (`CopVan.cs:14-18`), `CopVan.Update` (`20-35`), `CopVan.SpawnAgent(int)` (`37-48`) | Group size is `Random.Range(1, 4)`. First spawn is `Random.Range(30, 120)`. |
| Cop lifetime and damage | `BrainAgentCop.Start` (`72`: `leaveTimer = Random.Range(120, 240)`), `ShootNeet` (`275-278`: 60% chance, -15 damage) | |
| Cat need decay | `CatStats.Update` (`CatStats.cs:24-28`) | Replace the method or set the constants. |
| Cat feeding and litter | `BrainCat.EatFromBowl` (`327`), `Poop` (`338`), `BrainCat.KillRat` (`196`) | These private methods are reached via delegates from the tree. Thresholds (5, 25, 30) are inside `Start()` lambdas (`BrainCat.cs:96-113`), so patch the methods or `CatStats`, not the thresholds. |
| Rat spawn rate | `Mouse_Hole.Update` (`Mouse_Hole.cs:20-52`) | Constants `0.04f`, `0.03f`, and interval `35–65`. |
| Rat gang size | `BrainRat.SpawnGangOfRat` (`285-299`) | `Random.Range(2, 5)`. |
| Thief frequency | `ThiefHandler.Update` (`16-26`), `ThiefHandler.SpawnThief` (`29-43`) | Interval `Random.Range(2700, 5400)`. Turn on with `thiefCountdownActive`. |
| Need-driven spawns | `HouseManager.StatCheck` (`417-438`) | Threshold `< 20`, fail counter at `15`, 10% roll, max `4`. |
| Swap an NPC's behaviour mode | `BrainNeetAI.BrainFactory` (`153-230`) | Prefix can return a different tree. The mode is read once, in `Start()` (`255`). |
| Hostile NPCs and punch rate | `BrainNeetAI.Punch` (`758`), `CheckForNeet` (`694`) | |

### Spawning and despawning from a dev tool

- Spawn: `Object.Instantiate(BrainBank.brainsDict[name].obj, pos, rot)` as above. The `Brain` `Start` then runs and subscribes saving.
- Find brains: `Commons.CrawlSceneForType<Brain>()` (`Commons.cs:52`) walks the active scene. `SaveLoadHandler.loadedBrainsById` (`SaveLoadHandler.cs:25`) maps `saveId` to objects for loaded brains.
- Despawn: `Destroy(gameObject)`. `Brain.OnDestroy` unsubscribes saving (`Brain.cs:122-132`), so the NPC is removed from future saves. Brains such as `BrainNeetAI` also destroy themselves in their tree (for example `Deactivate`, `DeactivateWithHeldPickups`), so only remove them from a parent counter via `OnDestroyAction`.
- Before a spawn, check `HouseManager.characterStoriesDict` if the NPC uses `CharacterStories` (`CharacterStories.cs:222-243`), since duplicate names are rejected.

### Gotchas

- `BTRng.Load` reads `chance` with `Value<int>()` (`BTRng.cs:28`), but `Save` writes a float. After a load, `BTRng(0.1f, ...)` is likely to have `chance` 0 (inferred from Newtonsoft conversion). Patch it to `Value<float>()`.
- `BTConditionalElse` keeps its else branch outside `nodes` (`BTConditionalElse.cs:13` vs `BTNode.GetAllChildren`). That subtree is not saved or indexed.
- `BrainWithSegments` does not save `currSegmentIndex` (`BrainWithSegments.cs:39-42`).
- `BrainWithSegments.MoveTo` never succeeds. The tree depends on `NearTarget` to finish.
- Runtime `brainType` changes do not rebuild the tree (`Dialogue.cs:349-351`).
- `prefabName` is never assigned in code. A prefab that does not set it will fail on load with a `KeyNotFoundException` from `BrainBank.brainsDict[prefabName]`.
- Hidden `Update()` and `OnTriggerEnter` methods (see the Brain section) change which base logic runs.
- `Brain.GetRandomItemInCurrentRoom` never returns the last item (`Brain.cs:204`).
- `AITarget.Position` setter teleports a bound object (`AITarget.cs:177-179`).
- `BTWait.isSetuped` is not saved, so a waiting node re-randomises after load.
- Node timers only advance when the node is evaluated. A sequence that fails earlier pauses the later timers.

## Open questions

- Which prefab uses which class, `BrainType`, and `prefabName`? These are in Unity assets, not in the decompiled code. The keys "Thief", "SkeletonNeetNPC", "Spider_House", "Player_NPC", "Grandpa_NPC" (seen in `SeqSpawnThiefIntro.cs:21`) and the others need mapping to prefabs.
- What class does the `"SkeletonNeetNPC"` prefab use? `HouseManager.cs:390` spawns it, but `SkeletonParasite` is the only skeleton class I read.
- What calls `IBrainHijackable.Hijack()`? Nothing in the decompiled code does. The caller may be in another area, or the feature may be dead code. Nothing removes `BrainPlayerHijack` either.
- Does Unity dispatch private, hidden `Update`/`OnTriggerEnter` methods on subclasses? This decides whether `Brain`'s room tracking runs for `BrainCat`, `BrainRat` and others.
- Where is the Ink master script source? It determines whether a mod can add flows for a new dialogue NPC without editing the story asset.
- `IntentHolder` and the individual intent types were not read. How NPCs drive the body beyond what is written here is unverified.
- `Usable.cs` and `Player_Abilities.cs` were not traced, so the semantics of `DropHand`, `Throw`, `Use` and `Punch` used by `BrainNeetAI` are assumed from their names.
- `CopVan`'s spawn sets `component.van`, but who sets `moveTgt` before a cop's first tick is not traced beyond Unity serialization. `AITarget` fields are default-constructed by Unity serialization, so this is probably fine.
- `Succubus.Anim` and the `Reaper` trigger: which code calls them is not traced.
- `EncounterSpawner`, `HouseManager.GetValidSpawnPointForBrain`, `Locator` and `Commons` were only read at the call sites documented here. Their internals are owned by other areas.
- The exact `Health`/`Hunger`/`Bladder` defaults are not in this area, so the cat's and cop's effective values beyond what is written in `CatStats.cs` and `BrainAgentCop.cs` are unknown.
