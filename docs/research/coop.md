# Co-op multiplayer: feasibility and plan

Design note for a future mod: 2 to 4 players online, sharing one house. Nothing here has been built or run yet.

Conventions:

- Code references are `File.cs:line` under `decompiled/Assembly-CSharp/`, the same as in `docs/game/*.md`. Framework references are full repo paths.
- "Guess" marks anything not confirmed in code, in a save file, or in-game.
- Numbers about saves come from a real save in the Proton prefix (`compatdata/2054460/.../LocalLow/NEET Games/NEET/`).

## Summary

Co-op is feasible, but it is the biggest mod this framework is likely to see. The game has no networking and assumes one local player everywhere. Three properties make it workable anyway:

1. **Every character is the same rig, driven by an intent object.** The player and the human NPCs share `Player_Abilities`, `Player_Movement` and `PlayerStats`, and act only through `Brain.intents`. A remote player can be a normal player rig on the host whose intents come from the network.
2. **The save system already describes the world.** It covers nearly every persistent object, with stable ids and a `Save()`/`Load(JToken)` pair per component. A save snapshot is the join payload, `saveId` is the network id, and `Save()` output can be diffed to sync discrete state.
3. **Sockets work in this Mono under Proton.** DevBridge already runs a TCP server inside the game.

Recommended setup: a listen server where the **host is authoritative over the world** and **each client is authoritative over its own body pose**. On the host, each remote player is a full player rig whose hands, needs and interactions run from streamed intents. Transport is **LiteNetLib over UDP**: connect by IP and port, and use a VPN overlay for NAT until NAT punch or a relay is added.

Biggest risks: how physics feels on clients (kinematic replicas, held-item handoff), the long tail of game code that must be switched off or redirected on clients, the single global dialogue and Ink story, scene changes (dreams, death scenes), and save safety on both sides.

Rough effort (guess): a demo where two players walk around the same house is about 5 focused weeks. Full co-op with shared items, world systems, computer and dialogue is about 20 to 30 focused weeks.

---

## 1. Facts that shape the design

### 1.1 Bodies are interchangeable rigs driven by intents

- `Brain` owns an `IntentHolder intents` (`Brain.cs:76`), created in `Brain.Start` (`Brain.cs:140-141`).
- `BrainPlayer.Update` copies the Input System actions into the intents every frame (`BrainPlayer.cs:105-133`). `Player_Abilities`, `Player_Movement` and `CameraControl` read only `mainBrain.intents`. Examples: `Player_Abilities.cs:546-688`, `Player_Movement.cs:246-283` and `:398-406`, `CameraControl.cs:137` and `:216`.
- NPCs are full player rigs. `BrainNeetAI` caches the rig's `Player_Abilities` and `Player_Movement` (`BrainNeetAI.cs:233-245`), resets its intents every frame (`BrainNeetAI.cs:398`) and steers by writing them (`BrainNeetAI.cs:502-503`). A real save holds `Player_NPC`, `Grandpa_NPC`, `Bad_Thief` and `Player_Original` as `BrainNeetAI` rigs.
- The game already moves control between rigs at runtime. `Player_Abilities.SetMainPlayer` and `SetNotMainPlayer` (`Player_Abilities.cs:1934-2006`) swap in a `BrainPlayer` and switch the rig's camera, AudioListener, EventSystem, canvases and `CameraControl` on or off. Jaunting uses them on death (`Player_Abilities.cs:1846-1851`).
- A second `BrainPlayer` is tolerated. `BrainPlayer.Awake` disables the camera and destroys the AudioListener of any `BrainPlayer` that is not `HouseManager._.player` (`BrainPlayer.cs:37-41`).
- Aiming and holding use the rig's own camera, not a global one. `Player_Abilities.mainCamera` is a per-rig field (`Player_Abilities.cs:156`). It is used for the pickup ray (`:1122-1126`), the hold pose (`:1028-1090`) and the throw direction (`:1299`).

### 1.2 The save file is close to a full world description

Measured on a real save, `neetSave_1026202642534PM.json`:

| Entry type | Count | Entry type | Count |
|---|---|---|---|
| `Usable` | 384 (128 distinct prefabs, 36 kinematic) | `Brain` | 16 (player, cat, 3 rats, NPC rigs, delivery guy) |
| `Door` | 45 | `LightSwitch` | 13 |
| `FuseBox_Slot` | 27 | `Rest_Enabler`, `SinkFaucet` | 4 each |
| `Sit_Enabler` | 18 | `Spawner`, `EventConnector`, `SmokeDetector` | 3 each |
| singletons (`Dialogue`, `EncounterSpawner`, `StockMarket`, `TaxHandler`, `TV`, ...) | 1 each | **total** | **532** |

- Size: 555,629 bytes of indented JSON. The compressed `.nsav` saves that NeetModKit writes in the same install (`src/NeetModKit/Saves/SaveCodec.cs`, minified JSON plus Deflate) are about 33 KB. The paint splats are 42 PNGs, about 260 KB on disk. The Ink state (`mainStory`) is 419 bytes in this early save, and `maxIdSequence` is 2064.
- The player's brain entry carries `Player_Movement`, `Player_Abilities`, `PlayerStats` and `StatusHandler` sub-saves. That is a complete per-player record.

What this gives co-op:

- **Join payload.** `SaveLoadHandler.AllSaveDataToJSon()` is public and builds the whole snapshot in memory (`SaveLoadHandler.cs:87-104`). Compressed with PNGs, a join is about 300 KB.
- **Identity.** Once both sides have loaded the same snapshot, every saved usable and brain has the same `saveId` on both machines. Loading sets the id before `Start()` (`SaveLoadHandler.cs:232-234`, `:319`). Scene objects (doors, switches and so on) have scene-assigned `SaveID`s.
- **Serializers.** Each component's `Save()` is a ready-made description of its discrete state.

Gaps: objects with an empty `prefabName`, `dontSave`, buttons and levers are not saved (`Usable.cs:212-247`). Transient effects (spray spheres, particles) are not saved. The thoughts and email Ink stories are not saved either (`docs/game/dialogue-and-rendering.md` §1.7).

### 1.3 Nothing is deterministic

- `UnityEngine.Random` is called 411 times in 104 files, and nothing seeds it (no `InitState` anywhere).
- Gameplay is built on Rigidbodies (63 files reference `Rigidbody`).
- Some logic depends on frame rate. `CameraControl.cs:102-103` forces `targetFrameRate = 120`. `persistent` status effects tick per frame (`docs/game/player.md`).

So lockstep, where every machine simulates the same inputs, is ruled out. Exactly one machine must simulate each system.

### 1.4 Everything assumes one local player

See §4.5 for the full list. Most of these assumptions are local reads of `HouseManager._.player`, or of the local pause menu, that should mean "the acting player" or "this machine's player".

---

## 2. Transport

### 2.1 What ships with the game

From `NEET_Data/Managed` (listed with `ls`). There is no `NEET_Data/Plugins` folder and no native DLL apart from Mono and Unity.

| Item | Relevant for networking? |
|---|---|
| `System.dll` (Mono BCL) | Yes. `System.Net.Sockets` (`Socket`, `TcpListener`, `UdpClient`). DevBridge uses `TcpListener` (`mods/DevBridge/Server/BridgeServer.cs`). |
| `netstandard.dll` | Yes. The facade is present, so `netstandard2.0` libraries load. |
| `System.Net.Http.dll`, `UnityEngine.UnityWebRequest*.dll`, `UnityEngine.UnityCurlModule.dll` | HTTP only. Useful at most for a lobby or introducer web API. |
| `System.IO.Compression.dll` | Deflate/GZip for the join snapshot. NeetModKit's `SaveCodec` already uses it. |
| `Unity.Services.Core.*.dll`, including `.Networking` | Unity Gaming Services plumbing. No Relay or Lobby package, and no project configuration. Not usable. |
| Mirror, Netcode for GameObjects, Unity Transport, Photon, Steamworks.NET, Facepunch.Steamworks, `steam_api.dll` | Not present. |

The runtime is MonoBleedingEdge (`MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll`), 32-bit. Under Proton, Mono's sockets go through Wine's Winsock, which maps onto Linux sockets. DevBridge shows that TCP listen and accept work on loopback. Binding `0.0.0.0` and using UDP should work the same way (guess; check in Phase 0). Mono believes it is on Windows, so libraries take their Windows code paths.

### 2.2 Options

| Option | For | Against | Verdict |
|---|---|---|---|
| Raw TCP, like DevBridge | No dependencies. Already proven in this game. | Head-of-line blocking: one lost packet stalls every later message, including position updates. Bad on Wi-Fi and the internet. Needs `NoDelay`. | Fine for Phase 0 experiments, not for play. |
| Raw UDP (`UdpClient`) | Lowest latency. | Reliability, ordering, fragmentation (the snapshot is about 300 KB), connections and keepalive would all have to be written. | Not worth writing. |
| **LiteNetLib** | MIT licence. Pure C#, with a `netstandard2.0` build or usable as source. Reliable-ordered, reliable-unordered, sequenced and unreliable delivery on one socket, with channels. Fragments large reliable messages. Measures RTT, does LAN discovery by broadcast, has a NAT punch module, and can simulate latency and packet loss (debug builds). Common in Unity mods. | One more DLL. Its Windows-only socket code runs under Wine: check the `SIO_UDP_CONNRESET` ioctl and IPv6 dual-mode sockets, and switch IPv6 off if needed (guess). | **Recommended.** |
| kcp2k (KCP) | MIT, pure C#, reliable and unreliable UDP. | Fewer features and less documentation. | Fallback. |
| ENet-CSharp | Proven. | Needs a native `enet.dll` (x86). | No. |
| Steam networking (Steamworks.NET or Facepunch.Steamworks, plus `steam_api.dll`) | Steam relays (SDR) solve NAT, and friends can invite each other. | The demo ships no `steam_api.dll` and never starts the Steam API. We would ship the x86 DLL and pick an AppID. The Proton prefix id suggests the demo's AppID is 2054460 (guess). Shipping `steam_api.dll` in a mod is a grey area. | Optional later transport. |
| Unity Relay or Lobby, Epic Online Services, Photon | Hosted relays. | Need accounts, keys and SDKs. The UGS packages are missing. EOS and Photon need native SDKs or service keys. | No. |

### 2.3 Getting players connected

| Method | Code work | Who it works for |
|---|---|---|
| Direct IP, host forwards one UDP port | A text field. | Hosts who can forward a port. Not behind CGNAT. |
| LAN | Broadcast discovery (LiteNetLib). | Players on the same network. |
| Overlay VPN (Tailscale, ZeroTier, Radmin VPN) | None. | Everyone, but each player installs it. On Linux the VPN runs natively, and Wine uses the host's interfaces, so the game sees the VPN address (expected; test). |
| UPnP port mapping (Open.NAT, MIT, pure C#) | Small. | Routers with UPnP enabled. Not behind CGNAT. |
| NAT punch-through (LiteNetLib NatPunchModule plus a small public introducer) | Medium, plus hosting a tiny server. | Most home NATs. Fails on symmetric NAT and CGNAT. |
| Relay (UDP forwarder on a VPS) | Medium, plus hosting cost. | Everyone. Adds latency. |
| Steam relays | See §2.2. | Everyone with Steam. |

On a Linux host, the UDP port must also be open in the host firewall (ufw or firewalld). Wine shows no Windows Firewall prompt.

### 2.4 Recommendation

- **LiteNetLib**. One UDP port, default something like 7777, configurable. The host is a listen server. Clients join by IP and port. The connection key carries the co-op mod version, plus an optional password.
- Document "use Tailscale or ZeroTier if you can't forward a port" from day one. Add UPnP later, then NAT punch or a relay only if people need them.
- Hide the transport behind a small interface (`Send(peer, channel, bytes)`, connect, disconnect and receive events) so a Steam or relay transport can be added without touching game sync.

Channel layout (LiteNetLib delivery methods):

| Channel | Delivery | Traffic |
|---|---|---|
| 0 | Sequenced (unreliable, drops stale) | Poses and transform snapshots |
| 1 | ReliableOrdered | Events: spawn, destroy, intents' button edges, sequence verbs, dialogue, requests |
| 2 | ReliableOrdered | Discrete state diffs (JSON) |
| 3 | ReliableOrdered | Join snapshot (fragmented). Kept apart so it does not block channel 1. |

### 2.5 Packaging and testing

- Mods target net472. A `netstandard2.0` DLL works because `netstandard.dll` ships in `Managed`. NeetLoader resolves extra DLLs in a mod's folder by simple name (`src/NeetLoader/Mods/AssemblyResolver.cs`), so `LiteNetLib.dll` can sit in the mod folder. Vendoring the source (MIT) is the alternative.
- Hot reload (`src/NeetLoader/Mods/AssemblyRewriter.cs`) does not suit a live session, which holds sockets, threads and puppet components of the old types. Either set `"hotReload": false`, or end the session cleanly on unload.
- Testing on one machine: two instances share `persistentDataPath`, so client save protection (§6.3) is critical even in testing. They also share DevBridge's fixed port, so the second instance cannot bind it; make the port configurable. Whether Unity lets a second instance start is unknown, because the "force single instance" player setting is not visible to us (guess: allowed). Two PCs, or one PC plus a VM, avoid both problems.

---

## 3. Architecture

### 3.1 Models considered

| Model | Assessment |
|---|---|
| Lockstep: send inputs, everyone simulates | Impossible. Randomness is unseeded, physics is not deterministic, and some logic depends on frame rate (§1.3). |
| Everyone simulates, with periodic corrections | Side effects happen twice: each machine spawns its own rats, thieves, tax bills and gibs. Corrections fight constantly. |
| Client-authoritative interactions (each player owns what they touch) | Every interaction runs game code locally. `IUsable.Use` can spawn, damage, cook or change money. Every consequence would have to be found and replicated by hand, and conflicts resolved. |
| Host-authoritative, pure input streaming | Reuses all game logic. But the player's own movement lags by a full round trip unless we write prediction and reconciliation for a Rigidbody controller, and Unity physics has no rollback. |
| **Host-authoritative world, client-owned body pose** (chosen) | The client moves its own body locally, so movement and look are instant, and sends pose plus intents. The host runs a full rig for that player from those intents, so hand, need and interaction code runs unchanged. Everything else belongs to the host, and clients render replicas. |

Trust model: friends playing co-op. The host clamps obviously bad input (speed, reach) but there is no anti-cheat.

### 3.2 Who runs what

| Thing | Host | Client |
|---|---|---|
| Own body movement and look | Local | Local and authoritative. Sent to the host. |
| Remote players' bodies | Logic puppet (§4.3): pose from the network; hands, needs, stats and damage run here | Visual puppet (§4.4) |
| Interactions (pickup, use, drop, throw, punch, kick, buttons, levers, doors) | Run for everyone | Sends intents and a target hint. Predicts its own held items. |
| Player stats, status effects, death | Simulated for all players | Display only (replicated) |
| NPC brains, spawners, encounters, thief, rats, cops, taxes, stocks, card game, email, timers, thunder | Run | Disabled. Results are replicated. |
| Physics of items, doors, NPC bodies | Run | Kinematic replicas, interpolated |
| Fire, power, water, heat | Run | Replicated state, with local visuals |
| Ink and dialogue | Run | Text and choices shown. Choices sent back. |
| Paint, particles, sounds | Run | Cosmetic, from events. Paint arrives as PNGs on join. |
| Saving | Host only | Never |

### 3.3 Messages

| Direction | Message | Channel | Rate |
|---|---|---|---|
| Client to host | Pose: position, velocity, yaw, pitch, flags (crouch, sprint, sit, rest, slipped), analog intents (move, look and zoom strengths) | Sequenced | 30 Hz |
| Client to host | Button edges: press or release per intent, with the client frame number | ReliableOrdered | On change |
| Client to host | Target hint: net id of what the local pickup ray hits | Sent with the edge | On press |
| Client to host | Requests: computer actions, dialogue choice, eyelid-HUD buttons | ReliableOrdered | On action |
| Host to client | World snapshot: players, NPC rigs, awake rigidbodies, door rotations | Sequenced | 20 to 30 Hz |
| Host to client | Spawn and destroy (prefab key, id, transform, initial state) | ReliableOrdered | On change |
| Host to client | State diffs (changed `Save()` JSON per object) | ReliableOrdered | 2 to 5 Hz, only when changed |
| Host to client | Events: sequence verbs, lightning, sounds and effects, dialogue lines, death, respawn, teleport and impulses on the owner's body | ReliableOrdered | On event |
| Host to owner | Player state: stats, held ids, clothing, status effects, knockout | ReliableOrdered or Sequenced | 5 to 10 Hz |
| Host to joiner | Join snapshot (§6.4) | ReliableOrdered, channel 3 | Once |

Use LiteNetLib's `NetDataWriter` and `NetDataReader` (binary) on hot paths. Use Newtonsoft (already in `Managed`) for cold paths: snapshots, `Save()` diffs, requests.

### 3.4 Rates, interpolation and time

- Clients send their pose at 30 Hz. The host sends snapshots at 20 to 30 Hz, sampled after `FixedUpdate` (the physics step is unknown, guess 0.02 s). Sending runs on its own accumulator, independent of the 120 FPS target (`CameraControl.cs:103`).
- Remote entities are drawn about 100 ms in the past from a buffer of three snapshots: position by lerp or hermite, rotation by slerp. Extrapolate up to about 200 ms, then hold. Snap when the error exceeds about 2 m.
- Time: every snapshot carries a host tick number. Clients estimate the offset from the RTT that LiteNetLib reports.
- Compression: send only bodies that moved (`Rigidbody.IsSleeping()` or a delta threshold), positions quantised to millimetres, rotations as smallest-three quaternions.
- Bandwidth (guess): about 20 bytes per entity update. With 4 players, about 16 NPC rigs and about 20 awake items at 20 to 30 Hz, that is roughly 20 to 40 KB/s per client, with spikes during explosions. The host uploads that once per client, so under about 1.5 Mbit/s for 4 players.

### 3.5 Network identity

| Kind | Id | Notes |
|---|---|---|
| Saved usables | `Usable.saveId` (`Usable.cs:262-268`) | Same on all machines after loading one snapshot. |
| NPC brains | `Brain.saveId` (`Brain.cs:113-120`) | Same. |
| Static scene objects (Door, LightSwitch, SinkFaucet, GasButton, Sit_Enabler, Rest_Enabler, EventConnector, Spawner, SmokeDetector) | `ISaveableStatic.SaveID` from scene data | Same on all machines (`docs/game/save-system.md` §8). |
| Scene objects without an id (buttons, levers, usables with an empty `prefabName`, fuse slots and buttons, computer, TV) | Hierarchy path: scene root, then child names and sibling indexes | Computed on both sides after load. |
| New spawns | Host allocates from `SaveLoadHandler.IdSequence`. Clients set `saveId` before `Start`, as `LoadUsables` does (`SaveLoadHandler.cs:232-234`). | |
| Client-local objects | On clients, patch `IdSequence` to count down from -1 | `IdSequence` is a static `++` counter (`SaveLoadHandler.cs:43`). Without this, a client-side `Usable.Start` (`Usable.cs:312` calls `SubSaveUsable`, which calls `GenerateSaveId`) would collide with host ids. |
| Players | Session slot (byte) plus a persistent GUID per install | The GUID keys per-player save data (§6.2). |

Unsaved scene usables get their ids from `IdSequence` in `Start` order on each machine. Do not trust those ids across machines; use the hierarchy path.

### 3.6 Three kinds of replication

1. **Transforms.** High rate, unreliable (§3.4).
2. **Discrete state.** On the host, call each tracked component's own `Save()` at a low rate, compare with the last sent string or hash, and send only what changed. On the client, apply it with a small hand-written applier per type. Calling the game's `Load(JToken)` directly is risky because many `Load`s re-run `Start()`. For example, `Door.Load` calls `Start()`, which subscribes to saving again (`Door.cs:162-171`, `:193-196`). Audit each type before reusing its `Load`.
3. **Events.**
   - Spawn and destroy.
   - `SequenceManager.TriggerSequence` verbs (`SequenceManager.cs:122`). Every Ink and timer-driven scene change goes through this one method.
   - Lightning: `HouseManager.Thunder` raises `OnLightningStrike` (`HouseManager.cs:488`).
   - Sounds and effects, dialogue lines, death and respawn.

---

## 4. Remote players

### 4.1 Can a second rig be spawned and driven? Yes

Evidence is in §1.1. Each NPC is already a second, third or fourth player rig driven through its intents. `TradeSoulForClone` spawns a `Player_NPC` clone at runtime (`Dialogue.cs:289`). `SetMainPlayer` and `SetNotMainPlayer` already turn a rig into "the player" or "a body" at runtime.

`HouseManager.CreateAndSetNewPlayer` (`HouseManager.cs:441-467`) is **not** the way to add a remote player. It replaces `HouseManager._.player`, moves the old body up 600 units, rewires the local post-processing (`Feedback_Sender._.postVolume` and others), and increments the Ink variable `deathRespawns`. It is a local-respawn routine.

### 4.2 Intents over the network

- `Intent.UpdateState(held, str)` sets `wasHeld = isHeld` and `isHeld = held` (`IntentHolder.cs:55-68`). `Started` and `Ended` compare the two (`IntentHolder.cs:31-53`). An edge therefore lasts exactly one `UpdateState` call.
- The `Active` getter looks as if it schedules a reset for the next frame. It does not: `Commons.CoroutineWrapOneFrame` only builds an `IEnumerator`, and nothing starts it (`IntentHolder.cs:23-26`, `Commons.cs:75-79`). Intent state is plain data, so writing it from the network is safe. This corrects the gotcha in `docs/game/player.md`.
- The puppet driver must call `UpdateState` on every intent once per host frame, repeating the last state, just as `BrainPlayer.Update` does. Queued button edges are applied one per frame, so a press and release inside one network tick still produce `Started`, then `Ended`.
- Double-tap punch and kick use 0.25 s windows timed by host frames (`Player_Abilities.cs:1008-1010`, `:1021`). Network jitter can break them. Send explicit punch and kick events, or replay edges using the client's frame timing.
- Look strengths are raw mouse deltas multiplied by the host's `Feedback_Sender.mouseSensitivity` (`Player_Movement.cs:259`, `CameraControl.cs:216`, `Door.cs:309`). Yaw and pitch travel as absolute values, so this only affects item rotation (`Player_Abilities.cs:1336`) and door dragging. Scale incoming strengths by client sensitivity divided by host sensitivity.

### 4.3 Host side: the logic puppet

Recipe (all on the host):

1. Instantiate `HouseManager._.playerPrefab`, or `BrainBank.brainsDict["Player"]` (the key the save uses for the player brain), at a spawn point. `BrainPlayer.Awake` sees an existing `HouseManager._.player` and disables the new camera (`BrainPlayer.cs:37-41`).
2. Add a `RemotePlayer { slot }` marker component and call `SetNotMainPlayer(rig)` (`Player_Abilities.cs:1980-2006`). That disables the rig's canvases, EventSystem, `CameraControl` and HUD symbols.
3. Add a Harmony prefix on `BrainPlayer.Update`: if the rig has `RemotePlayer`, write its intents from the network buffer and return `false`. Otherwise the puppet reads the host's keyboard (`BrainPlayer.cs:112-132`).
4. Pose: make the Rigidbody kinematic and move it with `MovePosition`/`MoveRotation` in `FixedUpdate`, toward the interpolated client pose. Kinematic bodies still push dynamic objects and still fire triggers (rooms through `Brain.OnTriggerEnter`, `Brain.cs:176-187`; dialogue range; the computer). Set the camera child's pitch directly, because `CameraControl` is off. Prefix `Player_Movement.Update` and `FixedUpdate` for puppets so they skip yaw, forces and ground snapping (`Player_Movement.cs:256-261`, `:382-391`, `:396-420`). Copy crouch, sit and rest flags from the client so the capsule, animator and energy regeneration stay right.
5. Keep it out of the game save: after `Start`, call `SaveLoadHandler._.UnsubSave(brain.Save)`. `Brain.Start` subscribes it (`Brain.cs:136-139`). Child components are saved only through `SaveBasicBrain` (`Brain.cs:104-106`), so this one call is enough. See §6.1 for why this matters.
6. Death: the death paths call `HouseManager._.CreateAndSetNewPlayer` for any `BrainPlayer` (`Player_Abilities.cs:1666`, `:1855`, `:1927`). For puppets, prefix it to respawn that remote player instead.
   - `Die()` also runs `GetComponentInChildren<AudioListener>().enabled = false` (`Player_Abilities.cs:1843`). On a second `BrainPlayer` that listener was destroyed in `Awake` (`BrainPlayer.cs:40`). Without a patch, a puppet's death throws a `NullReferenceException` there and never respawns. Patch `Awake` to disable the listener instead of destroying it.
7. Forces the world applies to a remote body do nothing on a kinematic puppet, and the client owns the pose anyway. Forward them to the owner as events: explosions (`Commons.ExplosionDamage`), kicks (`Player_Abilities.Kick`), teleports (`KillPlane.cs:15-20`, `Teleporter.cs:13-22`, `RecallObjectButton`). Unity's `Rigidbody.AddForce` itself cannot be patched, so the known callers are patched instead.

Why a `BrainPlayer` plus a marker, rather than a custom `Brain`: many checks use `GetComponent<BrainPlayer>()` to mean "a player is here".

- `Computer.cs:164-180`, `CharacterStories.cs:155-195`, `Sit_Enabler.cs:90`, `Rest_Enabler.cs:88`, `KillPlane.cs:18`, `NakedTrigger.cs:19`, `ClosedEyeTrigger.cs:34`, `Frontyard.cs:11-28`, `Locator.cs:47` and `:59`, `Usable.cs:591`, `Commons.cs:179`, `MaskFace.cs:86`, `VertexShakeA.cs:56`.
- The death paths in `Player_Abilities`.

Keeping `BrainPlayer` makes remote players count as players everywhere, and the marker separates local from remote where it matters. A subclass of `BrainPlayer` would also satisfy `GetComponent<BrainPlayer>()`. But `BrainPlayer.Update` is a private `new` method, and how Unity dispatches messages on subclasses of it is unverified. The marker and prefix avoid that question and make hot reload simpler.

### 4.4 Client side: own body and visual puppets

**The client's own body** is that machine's normal `HouseManager._.player`. `BrainPlayer`, `Player_Movement` and `CameraControl` run unchanged, so movement, look, crouch, HUD and cursor are all local.

- `Player_Abilities` keeps running for hand motion and hold visuals, but its world-changing calls are blocked by prefixes on the local rig: `Pickup`, `Drop`, `Throw`, `Use`, `PickupUseOnly`, `Punch`, `Kick`, `Shit`, the breath spray (`:688-718`) and `RemoveClothing`. The intents still go to the host.
- `pickups[]` is mirrored from the host. `CenterPickup` (`Player_Abilities.cs:1028`) then moves the held item on the client from the client's own camera. The item is non-kinematic locally while this client holds it, and host transforms for it are ignored until it is released. This is owner prediction.
- `PlayerStats.FixedUpdate` decay is off (`PlayerStats.cs:258`) and `invincible = true`, so contacts in the client's copy of the world cannot hurt the client. Values arrive from the host.
  - Health uses `ForceSetVal`, plus an explicit death event.
  - Knockout, disgust and wetness use `SetVal`, so the local effects fire: eyes shut (`CameraControl.cs:110`), slip (`Player_Movement.cs:209-211`), vomit check.

**Visual puppets** (the host's body, other clients, every NPC rig):

- Destroy or disable `Brain`, `Player_Abilities`, `Player_Movement`, `PlayerStats`, `StatusHandler`, `CameraControl` and the `FluidGun` logic. Make the Rigidbody kinematic.
- Remove `BrainPlayer` from player puppets so client-local triggers (computer, dialogue range) ignore them.
- Drive: transform, camera pitch, animator (`isWalking`, `isDead`, `Kick` trigger, speed 1.0 or 1.5), crouch capsule and camera height, blend shapes (fat, body type, muscle), clothing (the `PutOnClothes` path, `Player_Abilities.cs:1428`) and attachments (held items, masks).

### 4.5 Single-player assumptions

| Assumption | Where | Effect in co-op | Workaround |
|---|---|---|---|
| `HouseManager._.player` and `mainCamera` are the only player | `HouseManager.cs:76`, set in `BrainPlayer.cs:33-48` | Keep them as "this machine's player" on every machine. | Redirect the call sites below that mean "a player" or "the actor". |
| Stat-driven spawns read only the host's stats | `HouseManager.cs:387-398` | Skeletons and spiders spawn only from the host's hunger and energy. | Prefix `Update` or `StatCheck` to evaluate each player. |
| Spawn-room exclusion uses one player's room | `HouseManager.cs:483` | Spawns can land in a client's room. | Exclude every player's room. |
| Thief avoids only the host's room | `ThiefHandler.cs:40` | Same. | Same. |
| Skeleton parasite hunts only `HouseManager._.player` | `SkeletonParasite.cs:99`, `:125`, `:138-154` | Ignores clients. | Target the nearest player. |
| Hostile/trick-or-treat NPCs face `HouseManager._.player` | `BrainNeetAI.cs:645` | Face the wrong player. | Use the nearest player or the NPC's target. |
| Microwave keypad range check uses `HouseManager._.player` | `Microwave_Interface.cs:67`, `:90`, `:105` | Remote players cannot use the microwave. | Check the player who pressed, or the nearest player. |
| Corpse reaping checks the host's masks | `Player_Abilities.cs:1693` | Wrong player's glasses decide. | Check the dying player. |
| Dialogue externals use `HouseManager._.player` | `TradeSoulForClone` `Dialogue.cs:283-293`; `SceneSwitchKnockout` `:269-273`; `AddPlayerSceneSwitchOnDeath` `:371-379` | Affect the host instead of the speaker. | Use `currSpeaker.playerRef`, as `KillPlayer` already does (`Dialogue.cs:301-314`). |
| Autosave condition uses the host's room | `SaveLoadHandler.cs:80` | Fine (the host saves). | Optional: "any player in `Bedroom_Neet`". |
| Persist-to-house moves items to `HouseManager._.player` | `Commons.cs:265-266`, `DreamRandomizer.cs:44` | Dream items. | Part of the dream policy (§6.5). |
| Face and body changes apply to `HouseManager._.player` | `Computer_FaceChanger.cs:86`, `:111`, `:122` | Local only. | Replicate appearance per player. |
| Body type from PlayerPrefs only for an object named "Player" | `PlayerStats.cs:244-247` | Puppets get the prefab default. | Send each player's body type and fat. |
| Face texture loaded from the local StreamingAssets | `PlayerStats.cs:455` | Every rig shows the local player's face. | Send face PNGs at join (later phase). |
| Any `BrainPlayer` toggles computer interactability | `Computer.cs:170-184` | A remote player walking away locks the host's computer. | Only the local player. |
| Any `BrainPlayer` leaving an NPC's range closes the dialogue and clears `playerRef` | `CharacterStories.cs:184-198` | Player B walking past ends player A's conversation. | Only when `other == playerRef`. |
| Every rig's abilities and movement stop while the local pause menu is open | `Player_Abilities.cs:544`; `Player_Movement.cs:246`, `:299`, `:396`; `Dialogue.cs:514`; `CharacterStories.cs:247` | The host opening Escape freezes remote puppets. | Apply the gate only to the local rig. The game never pauses time (no `timeScale` change outside the dream), so nothing else stops. |
| `Exit` intent opens the global options menu | `CameraControl.cs:137-139` | Only matters if a puppet's `CameraControl` runs. | Keep it off on puppets (`SetNotMainPlayer`). |
| Global cursor lock and frame rate | `CameraControl.cs:103`, `:206-218` | Same. | Same. |
| Death of any `BrainPlayer` rebuilds the local player | `Player_Abilities.cs:1666`, `:1855`, `:1927`; `HouseManager.cs:441-467` | A remote death would replace the host's body. | Prefix for puppets (§4.3). |
| Second `BrainPlayer` loses its AudioListener | `BrainPlayer.cs:40` vs `Player_Abilities.cs:1843` | `NullReferenceException` in the puppet's death coroutine. | Disable instead of destroy. |
| Achievements count any `BrainPlayer` death | `Player_Abilities.cs:1832-1834`, `MaskFace.cs:86` | Remote deaths count toward the host's PlayerPrefs achievements. | Skip for puppets. |
| `Computer.Money` setter raises taxes and achievements | `Computer.cs:62-77`, `:121-142` | Replicating money through the setter on clients double-counts. | Write the field and refresh the label on clients. |
| Sleeping on any rig saves and loads `DreamZone` | `Player_Movement.cs:308-326` | A remote player resting with eyes closed sends the **host** to the dream. | Block, or apply the dream policy (§6.5). |
| Scene switches load scenes directly | `Dialogue.cs:243-273`, `LoadSceneOnDeath`, `SceneSwitcher.cs:19`, `Feedback_Sender.LoadMainMenu` | Splits the session. | Intercept in co-op. |
| `LoadBrains` destroys every brain and clears the player | `SaveLoadHandler.cs:304-310` | Puppets vanish on any load. | Re-create puppets after load. Avoid loads during a session. |
| `Brain.Start` subscribes every brain to saving | `Brain.cs:136-139` | Puppets saved as extra "Player" brains. | Unsubscribe (§4.3 step 5). |
| One id counter per machine | `SaveLoadHandler.cs:43` | Id collisions. | Client range (§3.5). |
| One `Dialogue` (one story, one speaker, one UI) | `Dialogue.cs:48-56`, `:512-530` | One conversation at a time, house-wide. | Accept it, and show the conversation to everyone (§5.3). |
| `Feedback_Sender.mouseSensitivity` is global | `Player_Movement.cs:259`, `CameraControl.cs:216`, `Door.cs:309` | Remote look deltas scaled by the host's setting. | Scale on send (§4.2). |
| `Camera.main` | `BrainBank.cs:54`, `PickupBank.cs:71` (console spawns), `PaintTarget.cs:71-86` and `:236-242` (debug paint), `RenderingModeSwitch.cs:45-51` | Debug paths only. | None needed. Puppet cameras are disabled. |
| `EventSystem.current` | `Computer.cs:164` | One per machine. | Puppets' EventSystems are already disabled by `SetNotMainPlayer`. |
| Per-rig HUD (eyelids, stat symbols, thought text) | Child canvases of each rig; `CameraControl.SetSymbolRenderers` | Works: the local HUD reads local `PlayerStats`, which the host fills on clients. | Puppets' canvases off. |

---

## 5. State to synchronise

| State | Owner | How | Difficulty |
|---|---|---|---|
| Player pose and animation | Owning client | Pose packets to puppets | Low |
| Player stats, needs, status effects | Host | Stat values at 5 to 10 Hz (§4.4). `StatusHandler.Save` only if effects need showing. | Low to medium |
| Clothing, masks, body shape | Host | Clothing ids from `Player_Abilities` save data, blend shapes, mask attach and detach events | Medium |
| Held items | Host, owner predicts | Held ids in player state. The owner runs `CenterPickup` locally (§4.4). Two players grabbing one item is already refused: `component.player == null` in `Player_Abilities.cs:1136`. | Medium to high |
| Loose items and physics | Host | Transform stream for awake bodies. Kinematic replicas on clients. | High (feel, see §5.1) |
| Item spawn and destroy | Host | Postfixes on `Usable.Start` and `OnDestroy` (`Usable.cs:276`, `:249`) and `Brain.Start` and `OnDestroy`. Sent as `prefabName` plus `saveId`. Known `Resources` prefabs by path (`FireSpreader`, `CopVan`, effects). | Medium. Objects spawned from serialized prefab references without a `prefabName` have no name to send; handle them case by case or treat them as cosmetic. |
| Item internals (cooked, filled, lit, broken, battery, contents) | Host | `Save()` diffs for `Food`, `LiquidContainer`, `Flame`, `Flashlight`, `Fuse`, `Lighter_BBQ`, `Piggybank`, `Cardboard_Box`, `Egg_Carton`, `PrefabContainer`, `Toilet_Paper_Roll`, `VacuumCleaner`, `Gun`, `HeatHolder` (beware its save bug, `HeatHolder.cs:100`), plus `Usable` damage, broken and active | Medium: many types, mostly mechanical |
| Spray spheres, gibs and other high-rate debris | Host | Never replicate the spheres. Replicate the emitter (FluidGun on or off, spray bottle use) and let each machine spawn cosmetic spheres. Gibs and organs are `PickupBank` items, so they replicate as spawns. | Medium |
| Doors | Host | Rotation in the transform stream. `Door.Save` (closed, locked, active) as a diff. Dragging works on the host through the holder's intents (`Door.cs:281-330`). The drag locks the holder's camera (`Door.cs:287`), so mirror that lock to the owning client. | Medium |
| Lights and power | Host | `LightSwitch.Save` (`switchedOn`, `isPowered`), fuse slot contents, fuse battery. Apply on clients through the setters, which refresh lamps (`docs/game/world-environment.md`). | Low to medium |
| Water (sinks, toilets, gas stoves) | Host | `SinkFaucet.Save`, `GasButton.Save`, `WaterVolume` level | Low to medium |
| Fire | Host | `FireSpreader.Save` (spheres, scales, particle positions; `FireSpreader.cs:100-106`) at 1 to 2 Hz. Clients run visuals only, with `FireSpreader.FixedUpdate` off (`FireSpreader.cs:121`). Flame lit state as a diff. Burn damage stays on the host. | High: many objects, heavy on randomness |
| Paint and decals | Host | PNGs at join. Afterwards, cosmetic local paint from replicated events, or periodic PNGs for changed targets. Exact paint sync is not planned. | Medium |
| NPC brains | Host | Same visual-puppet code as players. Brains disabled on clients. `CheckForNeet` already sees any rig on the `Player` layer (`BrainNeetAI.cs:694-717`). | Medium (reuses the player puppet) |
| Encounters and visitors | Host | `EncounterSpawner` runs only on the host (`EncounterSpawner.cs:164`). Visitors are brains. | Low once NPCs work |
| Cops, thief, rats, spiders, skeletons | Host | Brains plus spawns. Fix their single-player targets (§4.5). | Low to medium |
| Computer money | Host | Value replicated. Client UI actions become requests (§5.4). | Medium |
| Card game | Host | `AnimeCardGenerator.Save` (crystals, cards) at 1 to 2 Hz. `AnimeCard.Update` exp ticks off on clients (`AnimeCard.cs:214`). Buy, promote, click and redeem as requests. | Medium |
| Stocks, taxes, email | Host | Prices every 0.9 s tick (`StockMarket.cs:87`), `TaxHandler` bills (they are items), email inbox | Medium |
| Orders and deliveries | Host | `Computer.OrderItem` spawns a box, which replicates as a spawn. `DeliveryGuy` is a brain. | Low once items work |
| Dialogue and Ink | Host | Presentation events and choice requests (§5.3) | Medium to high |
| Timed sequences | Host | `TimerSequence.Update` off on clients (`TimerSequence.cs:47`). `TriggerSequence` verbs as events. | Low |
| Thunder, lightning, church bell | Host | Event. The thunder coroutine is off on clients (`HouseManager.cs:343`, `:488`). | Low |
| Smoke detectors and alarms | Host | `Save` diff plus alarm start and stop events | Low |
| TV, radio, music | Host | `TV.Save`, playback events | Medium |
| Scene changes (dream, nightmare, death scenes) | Host policy | Blocked in co-op at first (§6.5) | High |

### 5.1 The hard part: physics

- **Ownership.** The host owns every Rigidbody except an item held by a client, which that client owns while it holds it. On release (drop or throw), ownership returns to the host. The host's throw uses the puppet camera's forward (`Player_Abilities.cs:1299`), which equals the client's aim. Expect a small pop on release; blend over about 100 ms.
- **Clients cannot push replicas.** A kinematic replica stops the client's body, so a chair the client walks into does not move on the client. On the host the puppet never reaches it either, so the chair never moves. Options:
  1. Accept it (Phase 2 and 3).
  2. Make light items near the local player dynamic locally, with soft correction.
  3. Send contact pushes to the host.
- **Collision side effects run only on the host.** Paint from `CollisionPainter`, `BreakOnImpact`, `Impacter` and `Cutter` damage, sticky joints (`Usable.cs:591-603`) and impact sounds. Clients need them as events: sounds and effects at least. Paint is optional.
- **Bursts.** Explosions and gib bursts (`Commons.Obliterate`, `BreakOnImpact`) can wake dozens of bodies at once. The send filter must cap entities per snapshot and spread the rest over later snapshots.
- **Disabled components still get physics callbacks.** Unity sends collision and trigger messages to disabled MonoBehaviours. Disabling client-side components stops `Update` but not callbacks. Kinematic replicas avoid most collision callbacks, but triggers still fire. For example, a `Flame` trigger zone can damage the client's local `ActorStats` (hence `invincible`, §4.4). Food entering a `RefrigerationZone` also changes counters. Each callback that changes state needs a client guard.

### 5.2 Randomness

Do not try to synchronise RNG state. Make sure exactly one machine rolls the dice for each system. Any system accidentally left running on a client rolls its own, and the worlds drift apart without any error.

Safety net for testing: a DevBridge command on both machines that hashes `AllSaveDataToJSon()` per entry type. Positions are excluded or rounded. Comparing the hashes shows which system diverged. Cosmetic randomness (audio pitch, particles) can stay local.

### 5.3 Ink and dialogue

- There is one `Dialogue` with one `masterStory`, one `currSpeaker` and one on-screen UI (`Dialogue.cs:48-56`). The conversation pump reads `currSpeaker.playerBrain.intents.Use.Started` (`Dialogue.cs:514`), so on the host a puppet can talk with no changes. Most Ink externals already target `currSpeaker.playerRef` (`Dialogue.cs:301-314`). Three exceptions are listed in §4.5.
- `CharacterStories` keeps one `playerRef`, set by the first `BrainPlayer` to enter range (`CharacterStories.cs:155-161`).
- Plan:
  - Ink runs only on the host.
  - The host broadcasts each displayed line (speaker id, text, the tags that matter for visuals) and the choice list. Clients show them through a patched display path in their own `Dialogue` UI and do not advance the story.
  - The speaking player sends the choice index, and the host calls `MakeChoice`.
  - One conversation at a time, house-wide (inherent). Everyone sees it.
- The thoughts story (`ThoughtLoader`) is per machine and cosmetic. The email story runs on the host.
- Saved Ink state (`mainStory`) travels in the join snapshot automatically (`SaveLoadHandler.cs:101`, `:188`).

### 5.4 The computer

- It is a world-space canvas. The cursor comes from the local `Mouse.current` and `HouseManager._.mainCamera` (`Computer.cs:194`). The trigger checks accept any `BrainPlayer` (`Computer.cs:170-184`).
- Each machine shows its own copy of the computer. Screen navigation (`Computer_Button`) can stay local.
- Every action that changes state becomes a request that the host executes on its own computer: `OrderItem`, app purchase, card buy, promote, click and redeem, stock buy and sell, tax payment, power button. Results come back through replication.
- A "one user at a time" lock is optional. It avoids confusion but is not required, because the actions are serialised on the host.
- Apps that read local files (`CustomItemShop`, `CustomImageLoader`, `Computer_FaceChanger` with SimpleFileBrowser) would need file transfer. Defer them.

---

## 6. Saving and joining

### 6.1 Who saves

- Only the host saves. The co-op world is the host's world, and the game's `SaveAll` runs there as usual (autosave in `Bedroom_Neet`, `SaveLoadHandler.cs:80`).
- Puppets must never appear in the save as brains. If they did, the next load would instantiate several `Player` brains. `LoadBrains` sorts `instanceName == "Player"` first (`SaveLoadHandler.cs:311-313`), and every one of those `BrainPlayer`s reads the local keyboard (`BrainPlayer.cs:112-132`). The player would control several bodies at once. §4.3 step 5 prevents it.
- Items a remote player is holding are ordinary usables in the save, so they are kept.

### 6.2 Per-player persistence

- Store each remote player's body as the output of `Brain.SaveBasicBrain` (includes the `Player_Movement`, `Player_Abilities`, `PlayerStats` and `StatusHandler` sub-saves; §1.2), plus held item ids and a display name.
- Store it in NeetModKit's mod data entry (`src/NeetModKit/Saves/ModSaveData.cs`, type `NeetModKit.ModData`, which the game ignores on load), keyed by the player's GUID.
- On rejoin, the record becomes that player's body (§6.4). Without a record, the player spawns fresh at `GetValidSpawnPointForBrain()` or near the host.

### 6.3 Protecting saves

On clients:

- Block `SaveAll` from every caller: autosave (`SaveLoadHandler.cs:80-83`), dream (`Player_Movement.cs:315`, `:324`), `LoadSceneOnDeath`, and mod or DevBridge callers. A client save is a new `neetSave_*` file. It becomes the client's newest single-player save, because the loader takes the newest file whose name starts with `neet` (`SaveLoadHandler.cs:145-162`). `SavePaintSplats` would also delete every PNG in the client's own `Textures/PaintTargetSplats` (`SaveLoadHandler.cs:125-141`).
- Never write the join snapshot to `persistentDataPath` under a `neet*` name. Load it from memory.
- `LoadPaintTargets` reads the PNGs from `persistentDataPath` (`SaveLoadHandler.cs:443-463`). Replace it with an in-memory version on clients.

On the host, optionally:

- Keep a separate co-op slot. Its prefix must not start with `neet`, or single-player will load it and "reset save" will delete it (`Feedback_Sender.cs:538-562`).
- The slot needs its own splat folder.
- Simplest first step: co-op uses the host's normal save.

### 6.4 Joining, including mid-game

1. The client connects. The connection key checks the co-op mod version, and the host also checks that both sides run the same mod list and versions.
2. The host builds the snapshot:
   - `AllSaveDataToJSon()` (`SaveLoadHandler.cs:87-104`). No disk write, and `OnPreSave` has no game subscribers (`docs/game/hooks-index.md` §3a).
   - Replace the `"Player"` brain token with the joining player's stored body (§6.2), or a fresh body at a spawn point. Remove held-item links that point at other players.
   - Add body records for the host and the other clients, from which puppets are built.
   - Encode with `SaveCodec`, then add the PNGs, captured into memory the same way `PaintTarget.SaveSplat` does. That uses `ReadPixels` per target, so expect a short hitch on the host (guess: tens of ms for about 42 targets).
3. The client loads `NEET_House` through the `Loading` scene. It sets `SceneLoadingData.sceneNameToLoad`, then calls `LoadScene("Loading")` (`docs/game/core-systems.md`). `LoadAll` is fed from memory. NeetModKit already redirects `File.ReadAllText` inside `LoadAll` with a transpiler (`src/NeetModKit/Patches/SaveFormatPatches.cs`), so extend that patch.
   - Also bypass the early return when no `neet*` file exists (`SaveLoadHandler.cs:146-150`), for example by redirecting `DirectoryInfo.GetFiles()` in the same transpiler.
   - Players without a single-player save could not join otherwise.
4. After NeetModKit's `GameEvents.HouseReady`, the client:
   - switches off world simulation (§3.2);
   - builds its identity map (§3.5);
   - tells the host it is ready.
5. Reconcile. Then the session starts streaming and puppets spawn.
   - The host sends the full list of networked objects (id, prefab, transform, active state).
   - The client destroys objects it has that the host does not, and spawns the missing ones.
   - This also covers everything that changed while the client was loading, so no event log is needed.
6. Mid-game join is the same flow, and the host does not pause. LiteNetLib keeps the connection alive on its own thread while the client's main thread is busy loading (guess). Raise the disconnect timeout during joins.

### 6.5 Leaving, reconnecting, dreams

- **Leaving:** the host drops the leaving player's held items, stores their record (§6.2) and removes the puppet. If the host leaves, the session ends; there is no host migration. A reconnect is a join.
- **Dreams and scene changes.** Sleeping runs `SaveAll` and `LoadScene("DreamZone")` (`Player_Movement.cs:308-326`). Ink can switch scenes (`Dialogue.cs:243-273`), and death can switch scenes (`LoadSceneOnDeath`). Policy:
  - Phase 1 to 5: block all of them in co-op.
  - Later, solo dreams:
    - The dreamer's client loads `DreamZone` locally, and their body stays asleep in bed on the host.
    - On waking, the client re-joins through the normal join flow.
    - Items carried back (`Commons.SetPersistOnObj`, `DreamRandomizer.cs:44`) become spawn requests to the host.
  - Shared nightmare scenes need all players to move scenes together. That is an open design question.

---

## 7. Phased plan

Sizes are focused dev-weeks for one person who knows this framework (guess).

| Phase | Goal | Main work | Size |
|---|---|---|---|
| 0. Groundwork | Two games connect | LiteNetLib wrapper with a transport interface; host and join page in the mod menu (name, IP, port, password); session state; message registry; dispatch to Unity's main thread (`NeetLoader.Runtime.MainThread.Post` exists); ping display; DevBridge `net.*` commands; a two-PC or VM test setup; check UDP under Proton (bind `0.0.0.0`, LAN broadcast) | 1 |
| 1. Ghosts | Players see each other walk around | Visual puppet from the player prefab; pose stream with interpolation; animator, crouch, sprint, nameplates; disconnect cleanup. Each machine still runs its own world. | 1 to 2 |
| 2. Same house (**first milestone**) | Two players walk around the same house and see each other; the world runs only on the host | Join snapshot with the Player token rewritten; in-memory `LoadAll` and paint; client save protection; switching off client simulation (§3.2); NPC rigs as visual puppets; door rotations; read-only transform stream for awake Rigidbodies; divergence checker (§5.2) | 2 to 3 |
| 3. Hands | Remote players pick up, use, throw and eat; items are shared | Host logic puppets (§4.3); intent stream with an edge queue and pickup hints; spawn and destroy replication with id ranges; owner-predicted held items; player-state replication into the client HUD; remote death and respawn; force and teleport forwarding; item state diffs | 4 to 6 |
| 4. World systems | Everything in the house is shared | Doors and locks; lights, fuses, power; sinks, gas, heat; fire; smoke detectors; sequence verbs; timers; thunder; encounters; cops, thief, rats and spiders targeting all players; sound and effect events; the remaining §4.5 patches | 4 to 6 |
| 5. Computer and dialogue | Shared economy and story | Money and request pipeline; card game; stocks, taxes, email; orders; dialogue presentation and choices; one-conversation rule | 3 to 4 |
| 6. Persistence and polish | Sessions survive restarts | Co-op save policy; per-player records; rejoin; dream policy; player faces; UPnP, then NAT punch or relay if needed; 4-player performance pass | 3 to 5 |

Exit criteria for the first milestone (end of Phase 2):

- A client with **no** local save joins a host mid-game over a VPN.
- The client sees the host's house as it is at that moment: items, NPCs, doors and paint.
- Both players see each other move smoothly at about 100 ms latency.
- NPCs move on the client exactly as on the host.
- The client's own single-player save and splats are untouched.
- The divergence checker shows no drift except item positions, which are expected to drift until Phase 3.

The game is free on Steam, so every player can install it. No game files need to be distributed.

---

## 8. Risks

| Risk | Why | Mitigation |
|---|---|---|
| Long tail of client-side game code | About 330 game classes. Any `Update`, coroutine or trigger left running on a client can spawn, roll dice or change state. | Default to "client does nothing": disable whole component types after load, guard the known callbacks, and run the divergence checker in every test. |
| Physics feel | Clients cannot push replicas. Held-item handoff pops. Impact sounds and paint arrive only through events. | Owner prediction for held items. Local dynamic light items later. Accept some stiffness at first. |
| Save corruption | Client saves hijacking single-player saves and wiping splats (§6.3). Puppets saved as extra players (§6.1). | Hard block on client `SaveAll`. Puppet unsubscription. An integration test that loads the host's save after a session and counts `BrainPlayer`s. |
| Single dialogue and Ink story | One conversation, one UI, one global story. | One conversation at a time, shown to everyone. |
| Scene changes | Dream, nightmare and death scenes split the session. | Block them first. Solo dreams through re-join later. |
| Input edges over the network | `Started` and `Ended` last one frame; punch and kick double-taps are timed by frames. | Edge queue, explicit punch and kick events. |
| Hidden single-player references | `HouseManager._.player` appears in 15 files, 39 times (grep). Prefab and inspector wiring is invisible in code. | §4.5 list. Grep again for `HouseManager._.player`, `BrainPlayer` and `Feedback_Sender._` when adding systems. |
| Prefab-reference spawns without `prefabName` | No name to send for some spawned objects. | Treat as cosmetic, or map case by case. Measure how many there are in Phase 3. |
| Proton/Wine networking quirks | LiteNetLib takes Windows code paths. IPv6 dual-mode and the UDP connection-reset ioctl are unverified. Linux firewall. | Test in Phase 0. Disable IPv6. Document the firewall. |
| Hot reload during a session | Old types and sockets linger. | `hotReload: false` for the co-op mod, or tear the session down on unload. |
| Performance | Up to 3 extra full rigs on the host (hand physics, stats, status effects), more replicas on clients, one 32-bit process | Profile in Phase 6. Puppets on clients run no logic. |
| Scope creep | Paint, fluids and fire are emergent systems that are expensive to sync exactly. | Sync authoritative state, and treat effects as cosmetic, per machine. |

---

## 9. Open questions

1. Does UDP on `0.0.0.0`, plus LAN broadcast, work under this Proton version? Does LiteNetLib's Windows path need its IPv6 or ioctl options changed? (Phase 0)
2. Can two instances of the game run on one machine? If so, both use the same `persistentDataPath` and DevBridge port.
3. Which components are enabled by default on the player prefab and on `Player_NPC`? In particular `CameraControl`: `BrainNeetAI.Awake` calls `CameraControl.Start()` by hand (`BrainNeetAI.cs:236-239`), which suggests it is disabled on NPCs. Also which layers they use.
4. Physics timestep (`Time.fixedDeltaTime`). How many Rigidbodies are awake in normal play, and during fires and explosions? (Measure with DevBridge.)
5. What do the eyelid-HUD buttons (toilet, hunger, hygiene, sleep; `CameraControl.cs:159-162`) call? They are wired in the inspector, and it decides whether they become requests.
6. How often is something spawned from a prefab reference without a `prefabName` (§5)?
7. How long do `AllSaveDataToJSon()` and PNG capture take on a late-game save, and how big is that snapshot?
8. Do kinematic, network-driven door replicas break anything client-side, such as the keyhole cameras (`Door.cs:409-443`) or door trigger logic?
9. Shared nightmare and Ink scene switches: move every player, or only the speaker?
10. How should Unity treat `DefaultExecutionOrder` on types from mod assemblies loaded at runtime? Unknown. Drive puppets from `LateUpdate` and Harmony postfixes rather than relying on execution order.
11. Is a Steam transport worth it later? It would need `steam_api.dll` (x86) and an AppID, and it is unclear whether initialising the Steam API from a mod works under Proton's Steam bridge (guess: yes).

## Appendix: measure first (DevBridge)

Before Phase 2, collect from a running game:

- `FindObjectsOfType<Rigidbody>()`: total and awake, sampled each second during normal play and during a fire or explosion.
- `AllSaveDataToJSon()`: time, raw size and `SaveCodec` size; PNG capture time.
- Enabled components and layers on a fresh player rig and a `Player_NPC` rig.
- `Time.fixedDeltaTime`.
- A list of `Usable`s with an empty `prefabName` or `dontSave`, and of scene buttons and levers. These need hierarchy-path ids.
