# Co-op

The Co-op mod (`mods/Coop`) lets several games share one house over the network. The host's house is the world: its items, money, NPCs and story. Every player keeps their own body: health, needs, status effects and clothes. The earlier feasibility study is in [docs/research/coop.md](../research/coop.md); the implementation differs from it where noted below.

## Playing

- **Host:** load a game, open the mod menu (F1), **Co-op** tab, **OPEN MY HOUSE**. Others connect to your address on UDP port 8889 (setting **Port**). The port has to be open in your router (forwarded to your PC) and in your firewall. On Linux with firewalld: `sudo firewall-cmd --add-port=8889/udp` (add `--permanent` to keep it). Without a forwarded port, a VPN like Tailscale or ZeroTier works: join the host's VPN address.
- **Join:** **Co-op** tab, type the host's address (`203.0.113.5` or `203.0.113.5:8889`), **JOIN**. You don't need a save of your own; yours are never read or written while you're in someone else's house.
- **Leave:** **LEAVE**, or go back to the main menu. In someone else's house the pause menu's **Get out** button reads **Disconnect** and leaves straight away. A client goes back to the main menu when the game ends. When the host leaves, the game ends for everyone.
- **The host's Players tab:** pick a player, then **Help** (heal: needs full and back up if they're down; bring them to you; go to them; kick, for a player who's stuck or bugged; give them any item) or **Trouble** (send the hitman, no charge and no wait for the line; slap; yeet; spin them around; freeze; starve; piss alarm; make them shit; knock down; set their room on fire; a spider, a rat or Poopman next to them). The host can pick themselves too. Players are told when the host did something to them.
- **Cheats are the host's:** the cheat menu doesn't open in someone else's house. While the house is shared the host's game speed stays normal.
- While in co-op you can rest with your eyes shut, but nobody dreams, and the story's scene changes wait (they'd take one player, or the whole house, away from the others).
- **The host decides the mods:** the mods that shape the shared house (all but Cheat Menu, Performance Fixes, Open Saves, No Feedback, the dev bridge and Co-op over Steam), their features and their shared settings (prices, chances, timings) are the host's. A client's mod menu shows them locked. When the host loads or unloads one of those mods, switches a feature or changes a shared setting, every client follows within a second; leaving gives a client its own setup back (nothing of the host's is saved on the client).
- The mod menu and the cheat menu don't pause the game in co-op: the house keeps going for everyone while you're in a menu.
- **Your files go to the others:** custom photos and tapes you buy in the custom shops, photos you take with the camera, a wallpaper you pick on the computer and your face (the computer's face changer) are sent to the other players, and theirs to you. Everyone's TV stations and mixtapes are in the game for everyone: the host's and every player's own, each file once. A player who joins gets the files already in the game before their house loads (setting **Get files before joining**); later ones come in the background, and until one has arrived you see what the game shows without it (a station shows its please-stand-by card). A note in the corner shows what's being sent and received, and the Co-op tab too.
- **The TV plays in step:** a video on the TV waits on its stand-by card until every player has it, starts for everyone at once, and stays in step with the host's.
- Notes in the corner also say when a player is joining, joins or leaves (setting **Show players joining and leaving**).
- **The hitman line:** in co-op, call 448-626 on either phone. For $69 the hitman takes a job on any player in the game, yourself included: about a minute later the cop van comes, its agents go straight for that player wherever they are, and they leave once the target is down or dead. One job at a time, and after an order the line won't take another for a while (setting **Hitman line cooldown**, 10 minutes of play by default; the host's setting counts). Outside co-op the line is busy.

Settings (Mods page, Co-op): **Your name** (shown over your head), **Port**, **Max players** (2 to 4), **Hitman line cooldown**, **Physics hands on other players** (experimental), **Biggest file to share** (512 MB), **File upload speed** (4096 KB/s), **Get files before joining** (on), **Show file transfers** (on), **Show players joining and leaving** (on), **File cache size** (2048 MB).

### Over Steam

The **Co-op over Steam** mod (`mods/CoopSteam`) adds Steam next to direct IP. Nobody forwards a port: Steam connects the games directly when it can and through its relay network when it can't, and it vouches for who is connecting.

- **Once:** fetch Valve's `steam_api.dll` and `steam_api64.dll`, which the pack can't include. The installer asks; later, with the game closed, `neet steam` (`neet steam --remove` takes them out). They're downloaded from the Steamworks.NET release on GitHub and checked against known hashes. The game uses the one for its player: `steam_api64.dll` on the 64-bit player the installer sets up, `steam_api.dll` after `use-32bit`.
- Start Steam, then the game from Steam, signed in to the account that has NEET Demo.
- **Host:** **OPEN OVER STEAM**. The Co-op page shows your SteamID64 (17 digits starting with 7656119); send it to your friends.
- **Join:** type the host's SteamID64 (or `steam:` and the number) and **JOIN**. A 17-digit Steam ID goes over Steam; anything else is a direct address.

## How it works

### Transport

- LiteNetLib 1.3.1 (MIT) over UDP, behind `Coop.Net.ITransport`, so another transport (Steam's relay) can be added without touching the rest. The Steam option is researched in [docs/research/coop-steam.md](../research/coop-steam.md).
- Four channels: `Stream` (sequenced, for poses and transforms), `Events`, `State` and `Bulk` (reliable and ordered; the join snapshot goes on `Bulk` so it never holds up anything else).
- Every message starts with a `ushort` id (`Coop.Net.MessageIds`: 1-19 session, 20-39 join, 40-59 players, 60-79 files, 100-199 world). Handlers are registered with `Messages.On(id, handler)` and run on the main thread. Sending: `Outbox.Begin(id)`, fill the writer, then `Outbox.ToPlayers`, `ToHost` or `ToSlot`. Clients only talk to the host; the host passes on what the others need.
- The connection key carries `Protocol.Version`, and the hello carries the mod's version; mismatched games are turned away with a message.

### Session

`Coop.Session.CoopSession` holds the role (`Hosting`, or `Connecting`, `Joining`, `Playing` for a client), the players (slot 0 is the host) and the events everything else listens to: `PlayerArrived`, `HouseWanted`, `PlayerReady`, `PlayerLeft`, `Welcomed`, `WorldReady`, `Resumed`, `Ended`.

Handshake: the client connects and sends Hello (protocol, version, player id, name); the host gives it a slot (Welcome, with the player list) and raises `PlayerArrived`: it sends its mod setup, the session's media list and the list of files in the house (see Files). The client sends its own media folders, and once it has the files (`Join/JoinFiles`) asks for the house (SendHouse); the host raises `HouseWanted` and sends the snapshot; the client loads it and sends Ready; the host tells the others (PlayerJoined) and raises `PlayerReady`, which is where full state goes out.

`SessionNotices` puts notes in the corner (the framework's `Notices`) for a player joining, joined or gone, the house a client walked into, and why a client's game ended.

### Mod setup

`Session/ModSync`: the host sends its setup (ModSetup: the shared mods with versions, their features' states, the `[Shared]` settings) to a player when they're ready, to everyone half a second after something in it changes, and to a client that asks (after joining or a hot reload). A client takes the framework's `ModControl` lock for the shared mods (`ModList.IsShared`), loads and unloads mods to match, and applies the features and settings as overrides (`ModFeatures.Override`, `SharedSettings.Apply`), which are never saved. A shared mod the host runs in a version the client doesn't have gets 10 seconds to catch up (a hot reload reaches every game a moment apart) before the client leaves with the reason. Leaving undoes it all: the lock goes, the overrides are cleared, and mods loaded or unloaded for the host are put back.

### Joining: the host's house, loaded from memory

1. When the client asks for it (after its files, see Files), the host takes a snapshot with `NeetModKit.Saves.SaveCapture.Take()`: the game's own save data (mod data included) and the paint, without saving (about 200 ms and 130 KB on a typical house).
2. `SnapshotSanitizer` gives the joiner a body of their own: the `Player` entry keeps its place in the save but loses its sub-saves, so the game starts it like a new player (full needs, empty hands, its default clothes), in front of the host. The host's open conversation is closed.
3. The client holds `SaveLock` (no `SaveAll`, no "reset save"), hands the snapshot to `SaveSource.Use` and loads `NEET_House` through the `Loading` scene. The game's own `LoadAll` reads the snapshot instead of the save folder.
4. On `HouseReady` the client sends Ready. Paint made while they loaded follows right after (see Paint).

Returning players: every 10 seconds a client sends its body as the game would save it (`BrainPlayer.Save()`: position, stats, status effects, hands and clothes). The host keeps the latest one per player id in its own save (`BodyBook`, mod data), and the next join puts it into the joiner's `Player` entry instead of a fresh body (`BodyRestore`), with empty hands, and with the default outfit if a worn item is gone (the game's load stops on a missing item id).

### Players

- Each game sends its player's pose 30 times a second: position, the body's full rotation (so a slipped or downed body lies on everyone's screen), camera pitch, the body animator's bool parameters as a bitmask, a kick counter, and for each raised hand where it is (millimetre shorts, relative to the body) and which shared item it holds. A hand that isn't raised isn't sent. The layout is in the `PlayerPose` summary.
- Remote players are `PlayerPuppet`s: a copy of the player prefab's model (`Neet`: animator, skinned mesh, bones, hands) under a kinematic capsule. None of the game's player scripts are on it, so it reads no input, has no stats and is never saved. Poses play back 100 ms in the past from an 8-pose buffer.
- Arms: a body's arms follow the player's hands (`PuppetArms`, in LateUpdate after the animator). A hand is "up" when the game shows its first-person hand (holding something, reaching with the hand button, punching); the pose then carries where that hand is. The body's shoulder-elbow-hand bones get a two-bone solve towards it, blended in and out over about 0.15 s so the arm goes back to the animation when the hand is let go. The elbow keeps the side the animation had it on, a little out and down. The first-person arms are about 20-40% longer than the body's, so an arm that can't reach stretches along its bones (up to 1.4 times) and then just points at the target. A hand holding a shared item reaches the item itself as this game has it (`PuppetHeld` finds it from the pose's item name), so the hand meets what it holds even when the item is a little off from the player's own hand; only when the item is more than 0.9 m from the hand does the arm go to the hand's place instead. A body that is down, carried or lying (slipped) keeps its pose, with no reach and no head tilt.
- Looking: `PuppetLook` shares the camera pitch (capped at 70 degrees up and 55 down, so about 63 and 50 in total) between the chest (15%), neck (30%) and head (45%), each turned around the body's right-hand axis after the animator.
- Kicks: the game's `Player_Abilities.Kick` (a double tap of sprint) fires the animator's Kick trigger. A postfix (`KickSignal`) counts this game's player's kicks into the pose, and another game's body plays the kick when the count it shows goes up, so it plays on the same pose timeline as the rest of the body. A counter instead of a message, so a lost packet makes a kick late and never missing.
- Physics arms: the player model also carries the first-person arms (`NeetHandL`/`NeetHandR`: a jointed chain of bones from the shoulder to a hand end, skinned to the arm mesh, which the player's own scripts pull with forces). By default a body's copy drops their joints, bodies and colliders and hides them, so it shows the body mesh's own (reaching) arms. With the experimental setting **Physics hands on other players**, the copy keeps the chains (joined to its own body, without colliders, so they can't push anything in the house) and `PuppetHands` moves each raised hand's end to where that player's hand is, and shrinks the body's own arm (blend shapes 0 and 1, as the game does) while its physics arm shows; there is no reach solve then.
- `AppearanceSync` sends each player's look when it changes (clothes by item name, the outfit's UV set, the fat, body type and muscle blend shapes). Bodies get their own mesh copy, because the game swaps clothing UVs on the shared mesh.
- **Going down** (`Downed`): in co-op a player who would die goes down instead, whatever kills them: the game's `DieAccessor` (health running out, the story's KillPlayer, the kill timer), `SkeletonizeAccessor` (the skeletonizer trap, the story's SkeletonizePlayer) and `Obliterate` (the fire extinguisher's blast, a gibbing wig) all have a prefix for this game's own player (`DownedPatches`). Their effects are held back, not dropped: the `Ending` (death, skeleton, no-skeleton skeleton, gibbed; a worse one replaces a milder one while down) is remembered and runs the game's own method when time is up, so the loot, `OnPreDeath` hooks (the nightmare's scene switch), achievements, skeleton and new body all happen as in single player, just later. The downed body falls like a slip into the death pose (the animator's `isDead`) and can't get up, health stays at 1, nothing hurts it, and its eyes stay open, so it sees the house rather than the death screen; a line on screen counts down 15 seconds. Each game tells the others when its player goes down or gets up; the name over a downed body says so, and `Downed.IsDown(slot)` lets enemies leave them alone.
- **Reviving** (`Reviving`, from any game, for any player, even the host from a client; a `Revive` message with the health to stand up with goes to the player's game through the host): use food while looking at them or carrying them (prefix on `Food.Use`; the food is used up; 30 health), give them a sip from a cup of water or blood (prefix on `LiquidContainer.Use`, same aim; 20 or 35 health; dirt and cleaner stay yours), or hold a hand button on them with that hand empty for 3 seconds (15 health; a bar shows the progress, looking away or letting go stops it). A tap on the same button, shorter than a third of a second, carries them instead (`Carrying`).

### Paint

Blood, poop, piss, dirt, water, scorch marks, footprints and cleaner all go through `PaintTarget.PaintSplat`, and that's where `World/Painting` shares them: as splats, not pictures.

- The game that draws a splat sends it: the surface's save id (`PaintTarget.saveId`, set in the scene, the same id the join snapshot's pictures use), the brush's channel, texture (by name) and atlas cell, and the splat's box relative to the surface (so a splat on a door lands on the door wherever it is), packed into 20 bytes (`PaintCodec`). Splats go out in batches every 0.1 s on the reliable State channel (paint stays, so a lost splat would never be fixed); clients send to the host, which passes them on.
- The others queue the same splat on the same surface (`PaintApplier`), skipping `PaintObject`, so their copy has the sender's exact rotation, size and atlas cell, no drip effect of its own (the sender's drips paint and are sent like any splat), and nothing is sent back. A surface takes at most 100 waiting splats; what a busy surface can't take yet waits (`SurfaceQueues`).
- Who paints is who simulates the cause: the player's own body (bleeding, peeing, footprints), the host's NPCs and fires, whoever simulates a thrown or held item. Exceptions: a client's copy of a room fire doesn't scorch (the host's scorch arrives), and the taps (sinks, the bath) run their own water in every game, so their splats aren't sent (`PaintSources`).
- A room fire scorches under every burning sphere on every physics step. Those splats are merged before sending: two per spot and second (`PaintRepeats`). Painting a channel raises it to the splat's value rather than adding up, so repeats add little. Splats that can't reach their surface (a footprint's sphere cast goes on through the floors below) aren't sent.
- The host sends when a surface's water dries (clients have the game's drying off), and keeps the paint made while someone joins (up to 1 MB) for when they're in (`PaintCatchUp`).
- `Coop.World.Painting.PaintTestApi` (counters, `Trace` of who paints, `Drop` the game's blood or water blobs, `Splat`, `Dry`, `View`) and `PaintProbes` (`Coverage`, `Info`, `Unbound`) are the test hooks.

### Files

Custom pictures and tapes from the custom shops, camera photos, the computer's wallpaper, faces, and everyone's TV stations and mixtapes come from files on one player's disk. `Assets/` sends them to the others (`FileSharing` wires the parts up):

- A file is known by its content (`AssetId`: SHA-256, size, type) and kept in each game's cache, `Mods/Coop/cache` (`cache-2` for a second game on the same PC), as `hash.ext`. It's sent once per pair of games and kept across sessions. When the cache is over its size setting, the files used longest ago go; files used since the game started stay. `HashIndex` (`cache/hashes.json`) remembers hashes by path, size and write time, so unchanged files aren't read again.
- Things in the house keep a **shared path**: one whose file name is `hash.ext` (`SharedPaths`). When a file from a player's disk goes into the house during a session (a custom shop order, a photo, a picked wallpaper; on the host, when it starts hosting, the custom items, photos and wallpaper already in the house), it's copied into that player's cache, and the item, box or computer keeps the shared path from then on, in the host's saves too. The game's own loaders (`CustomTextureSaveLoader.CustomizeItem`, `CustomMusicSaveLoader.CustomizeItem`, `Picture.loadImage`, `WallpaperPicker.LoadWallpaper`) are hooked (`Hooks/LoaderPatches`, always on): a shared path becomes this game's copy right before the file is read, or the load waits and runs again once the file is here. On a client, a plain full path in the house is the host's and isn't read from the client's disk; the host drops plain paths that come with another game's item or box.
- What changes after a thing was shared (a new photo, a picked wallpaper, the host's items when it starts hosting) goes out as `AssetPath`: kind, item, shared path.
- Faces (`Faces/`): each game offers its face file (`StreamingAssets/Faces/Face.png` or `.jpg`, which the face changer replaces) and sends its id; the others put it on that player's body, on the body's own copy of the skin material (`PuppetFace`). A face changed on the computer goes out again.
- TV stations and mixtapes (`Media/`): every game hashes its loaded mods' `tv` and `mixtapes` folders in the background from the start (`LocalMedia`; again when a folder changes). A client sends its folders (each file's id, the list files' text) to the host right after Welcome and whenever they change (AssetMedia `Mine`, numbered). The host merges everyone's (`MediaMerge`: its own first as they are, then each player's by slot, leaving out files an earlier folder of the same kind has, their entries in the list files, list files it already has, and folders with nothing new), fetches the players' files, and sends the list to everyone with the last `Mine` number of each player that's in it (a joiner waits for its own). Every game, the host too once someone else's folders are in it, shows the list in place of its own folders (`MediaStandIn`, the framework's `ModContent.Substitute` with virtual folders: its own files where they are, everyone else's in the cache), so channel numbers and tape items are the same everywhere. A station or tape whose file isn't here yet shows the stand-by card or stays silent, and plays once it arrives (`ModContent.ReportArrived`; the folders don't load again). Leaving gives a game its own folders back.
- Joining (`HouseManifest`, `Join/JoinFiles`): the host sends a joiner the files in the house right away (custom pictures and tapes, photos, the wallpaper, everyone's faces). The joiner gets those and the session's media files before it asks for the house, so the house loads with everything in it; a file nobody can send, or one over its size limit, is skipped. With **Get files before joining** off it asks for the house at once.
- TV playback (`World/Ambience/TvPlayback`, WorldTv): any TV channel that plays a `VideoPlayer`. When a video comes on screen the host holds it on its first frame (a new "showing" number) until every player in the house reports it has it loaded, or can't get it; after 3 minutes it starts anyway. Then the host sends the video's time once a second (and on every change); a client more than 0.5 s off jumps to the host's time plus half the round trip and lets it settle for 2 s.
- Radio playback (`World/Ambience/RadioPlayback`, WorldRadio): what each radio plays (nothing, its tape, or another song by clip name and length, like the Mixtapes station's) and where it is, from the host once a second and on every change. A client plays the same (looping for a tape) and jumps to the host's time when it's more than 0.15 s off, then lets it settle for 1.5 s. On a client the Mixtapes station doesn't pick songs (`WorldRole.RunsWorld`), and Mixtapes names every song's clip after its file in the folder, so the names match between games.
- Notices (`Transfer/TransferNotices`): one note for what's coming in and one for what's going out, each with a bar for the whole batch, then "Received ..." or "Sent ..." for a moment (setting **Show file transfers**).
- Transfers (`Transfer/`): whoever needs a file asks for it (a client always asks the host; the host asks whoever offered it). Clients only talk to the host, so the host fetches every file a client puts in the house straight away and then passes it on. Chunks of 16 KB go on `Bulk`, at most 256 KB ahead of the receiver's acknowledgements per transfer, all of them together under the upload setting, smallest file first, to any player in the session (joiners too). The receiver writes `hash.ext.part`, checks the hash on a background thread, and only then uses the file. A transfer that goes quiet for 20 s is asked for again from where it stopped, and so is one cut by a hot reload: the part file stays, and what waited for a file waits again in the next copy of the mod.
- Only pictures, songs and videos the game or the mods load (and small JSON lists) are sent or taken, each up to the size setting. A game only ever reads, for another, a file it offered itself or has in its cache, never a path that came over the network, and only writes `hash.ext` names inside its cache.
- `Coop.Assets.AssetsApi` is the bridge's way in: `Status`, `Item`, `Items`, `Wallpaper`, `FaceOn`, `Media`, `Tv`, and test hands that go through the game's code (`OrderCustom`, `OpenCustomBoxes`, `TakePhoto`, `PickWallpaper`, `UseFace`). For a real transfer between two games on one PC, `ReceiveOnly(true)` (and `ClearCache`, `AskMedia`) makes a game use only what it was sent, since both see the same disk, and `AddMedia(folderName, path)` gives one game a media folder of its own. `TestNotice` shows a note to look at.

### Steam transport

- `SteamTransport` (CoopSteam) implements `ITransport` with `ISteamNetworkingSockets` peer to peer: the host listens (`CreateListenSocketP2P`, virtual port 0), clients `ConnectP2P` to the host's SteamID. Steam encrypts the traffic and authenticates each SteamID; only owners of the game can connect.
- Two send lanes: Bulk (the snapshot) on a lower-priority lane, everything else on the other. Messages over 256 KB are cut into pieces and put back together (Steam's limit is 512 KB). Stream packets carry a 16-bit counter so stale ones are dropped. The send rate is raised to 2 MB/s and the send buffer to 8 MB.
- Framing: the first byte is the channel (bit 6 marks a piece of a bigger message, followed by a flags byte: 1 first, 2 last); Stream adds the counter.
- Buffers are built and read in place in Steam's own message structs (`Native/MessageLayout.cs`), so nothing allocates per message. The struct layout is checked against a real message when Steam starts; if it doesn't match, the transport refuses to run.
- The Steam API starts lazily, the first time Steam is used (retried every 5 seconds after a failure). Callbacks are pumped from `Poll()`.
- `Steamworks.NET.dll` (MIT, release 2024.8.0, Steamworks SDK 1.60) is committed in both Windows builds with its licence: `lib/x64/` goes to the mod folder's root and `lib/x86/` to its `x86/` subfolder, which the loader uses instead on the 32-bit player ([per-bitness libraries](loader.md#modjson-fields)). The two builds differ only in the DLL they call, `steam_api64` or `steam_api`; `Native/SteamLibrary` loads the one for the game's bitness by full path and refuses to start when the Steamworks.NET in use calls the other one (a loader too old for `x86/`). The NuGet package has no x86 build, and both builds must match the DLLs that `neet steam` fetches from the same release. SDK 1.60 is on purpose: SDK 1.64's `steam_api.dll` asks for interface versions (Apps 009, RemotePlay 004) that GE-Proton 10's Steam bridge doesn't have, and Steamworks.NET won't start without every interface. If start-up fails anyway, the message names the missing interfaces (`Native/InterfaceCheck.cs`). Update them together (`lib/x86`, `lib/x64`, and the URL, size and hashes in `tools/NeetTool/Game/SteamApiSource.cs`). `steam_api.dll` and `steam_api64.dll` are never committed or packaged; `build/package.sh` stops if one ends up in `dist/`. Licence reasoning: [coop-steam.md](../research/coop-steam.md#licensing).
- `CoopSteam.SteamProbe.Run()` returns one line (init result, the game's bitness and which Steam DLL Steamworks.NET calls, SteamID, persona name, app id, overlay, relay status), for checking the Steam side through the dev bridge without a session.

### The host's Players tab

`Menu/PlayersPage` and `Admin/`: `PlayerAdmin.Do(slot, action, item)` runs on the host only. What happens in the house (an item in front of them, a room fire, a spider, rat or Poopman, the hitman with a 5 s wait) is done in the host's world and shared like anything else. What happens to a body (heal, bring here, slap, yeet, spin, freeze, starve, piss alarm, shit, knock down) goes to that player's game as PlayerAdmin (action, point, label) and `AdminEffects` does it to their real body there, with a note saying who did it; for the host's own body it's done right away. Kick is the transport's kick with a reason. Clients take PlayerAdmin only from the host.

`Menu/DisconnectButton` swaps the pause menu's Get out button's click and text on a client and puts the game's back when the session ends. The Cheat Menu checks the framework's `WorldRole.RunsWorld` before opening, and hides its game speed slider while `WorldRole.IsShared`.

### Co-op content

Calls and emails can be written for co-op; the hitman line is `mods/Coop/phone/hitman.jsonc`. Conversations run in the host's game, and the framework's rules about the player (`stat`, `holding`, `room`, `knockout`) are about whoever is talking or answering (`World/RemoteRules`: the client sends its stats and held items with every request, `Players/PlayerFacts`). Co-op adds (`Content/`):

- conditions: `"coop": true` (or `false`) while playing co-op; `"otherPlayer": 1`, that slot is in the house and isn't the one on the line (0 is the host); `"hitman": "busy"`, `"free"` or `"caller"` (he's on a job, he isn't, he's after the one on the line); `"hitmanWait": "> 0"`, minutes until the line takes another job;
- the action `{ "do": "hitman", "target": 1, "price": 69 }`: sends the hitman for that slot (`"me"` for the one on the line) and takes the price only if he takes the job;
- text values: `{{player:1}}` or `{{player:me}}` (a name), `{{hitmanWait}}`, `{{hitmanTarget}}`;
- for hand-written Ink: `CoopCaller()` (the slot on the line), `CoopHere(slot)`, `CoopName(slot)`, `CoopHitmanBusy()`, `CoopHitmanWait()`, `CoopHireHitman(slot)`;
- for code: `Coop.World.Contracts.SendHitman(slot, delaySeconds)`, `HitmanBusy`, `HitmanTarget`.

### Hot reload

The mod reloads mid-session without dropping anyone, which keeps the build-and-try loop fast:

- `mod.json` lists only `Coop.dll`, so the loader treats `LiteNetLib.dll` as a library and never reloads it.
- On unload the `NetManager` and its listener are parked in `ReloadState` with the session (as JSON); the next copy picks them up, re-subscribes, and raises `Resumed`. A parked connection nobody picks up within 10 seconds is closed.
- Everything the mod puts in the scene is destroyed on `CoopLoop.Unloading` and rebuilt from the next poses and a resync.

## Known limitations

Everything co-op doesn't do yet, or does differently from single player. Each line is something to look into; when one is fixed, remove it here. Lines marked (world) belong to world sync (`mods/Coop/World/`).

### Connecting

- Direct IP: the host needs the UDP port forwarded in their router and open in their firewall (firewalld on Linux blocks it by default). There is no UPnP, NAT punch-through or relay; a VPN (Tailscale, ZeroTier) is the workaround.
- Over Steam (no port forwarding) needs the Co-op over Steam mod, which has hosted under Proton but never had a second account join; see the Steam list below.
- No password or allow list: anyone who has the address and the same mods can join. Traffic isn't encrypted.
- At most 4 players. No host migration: when the host leaves, the game ends for everyone.
- A joiner needs every shared mod the host runs installed, in the same version; it's turned away otherwise, with the missing mods named. Switched on or off doesn't matter (mods off by default, like the Randomizer, too): the host sends its mod setup right after Welcome, so the joiner loads them before the house loads, and unloads shared mods the host doesn't run, for the session. Mods that only change your own game (Dev Bridge, Cheat Menu, Performance Fixes, No Feedback, Open Saves, Co-op over Steam) can differ.
- A connection carried over a hot reload keeps the channel layout it was opened with; after a change to the channels, leave and join again.

### Files

- Files come as fast as the upload setting (4 MB/s by default) and the connection allow. A joiner waits for the files already in the game (a 300 MB video takes over a minute); files added later come in the background, and until one has arrived its thing shows what the game shows without it: a blank picture or photo, a silent tape, a station's stand-by card, the default face.
- A file over a player's **Biggest file to share** setting isn't sent (or taken). The TV and tape lists still name it, so channels and tapes match, but it stays empty there.
- The TV waits for everyone before a video starts: a big video someone is still getting holds the TV on its stand-by card (3 minutes at most, then it starts without them, and they line up once it arrives).
- A player's TV stations and tapes are only in the game while they're in it. When they join or leave, the station list changes for everyone (the TV stays on the same channel number, which may now be another station), and their tapes left in the host's house are dropped the next time the host loads that save without them (the game can't load an item it doesn't know).
- A player's list files (`stations.jsonc`, `tapes.jsonc`) count for everyone, hidden game channels included.
- A file a client put in the house is kept in the host's cache once it has it, and the host's save points at it there. The cache drops files used longest ago when it's full, so a save not played for a long time can lose a client's picture; nobody else may still have it.
- A save made in co-op keeps shared paths. On the PC that saved, they're real files and work without Co-op; a save copied to another PC needs Co-op (and the files in its cache).
- Custom items bought before hosting are shared once the host starts hosting, a moment later (they're copied into the cache in the background); a player who joins in that moment gets them a second later.
- The custom shop's page background (random pictures from its own folder) stays each player's own. NPC faces come from each game's own files, so a soul-trade clone made from a client shows the face the drawing game has.
- Untested: over Steam, between two real PCs (only two games on one PC, loopback), and with three or more players (a file from one client reaches another through the host, which gets it first). The upload limit is per game, shared by all its transfers.

### Steam

- **Never joined over Steam yet.** The Steam API starts under GE-Proton10 (`SteamProbe.Run()`), the relay gets ready and a game hosts over Steam, but nobody has joined: that needs a second Steam account on another machine.
- One Steam account probably can't connect to itself, so two games on one PC can't test it (use direct there).
- No lobbies, no invites, no "Join Game" in the friends list, no rich presence: friends type the host's SteamID64. The overlay only exists when Steam started the game.
- No Steam auth tickets: the identity Steam gives each connection is trusted, but owner id under Family Sharing and VAC status aren't checked.
- Needs Steam running and online. Right after Steam starts, hosting or joining over Steam is refused for a few seconds until its relay network is ready. With `neet launch --direct` the game gets its app id from the tool; expected to work, untested.
- Needs a Proton whose Steam bridge has SDK 1.60's interface versions: checked for GE-Proton10-34, GE-Proton 11, Proton 11.0 and Experimental. An older Proton may fail to start Steam; the error names what's missing.
- On the 64-bit player (the default) the Steam API starts, the relay gets ready and a connection attempt runs its course (tried with an ID nobody hosts on: it times out cleanly), but hosting over Steam has only been tried on the 32-bit player.
- Reloading CoopSteam on its own mid-session leaves its old copy running the connection until Co-op reloads. A message in pieces that's in flight during a reload is lost.
- Unreliable messages over about 1.2 KB are split by Steam, and losing one piece loses all of it: the world streams should stay under one packet.
- Ping is refreshed every 2 seconds and allocates a little each time.

### Joining

- A first join gets a fresh body (full needs, empty hands, default clothes) in front of the host. After that, the host's save keeps each player's body (sent every 10 seconds) and a returning player gets it back, with these gaps:
  - up to the last 10 seconds of changes are lost when a player leaves;
  - hands come back empty, and what they held stays where it fell;
  - if a piece they wore isn't in the house any more, they get the default outfit;
  - bodies are kept per install (a player id in the Co-op config), so the same person on another PC gets a new body;
  - a body is only kept if the host saves after the player was there.
- Joining costs the host a hitch of about 200 ms per joiner (the snapshot), plus the time to send about 130 KB. Both grow with the paint in the house (one picture per painted surface): with 467 painted surfaces it was 1.7 s and 2.5 MB.
- If the host is in a conversation when someone joins, the joiner's copy of the story may start inside it (only the dialogue box is closed). Untested.

### Players

- Physics hands on other players (experimental setting) has only been checked by eye: an arm reaching for a held item and two-handed holds look right (they show the sender's own floppy arms, which hang low when both hands hold something); fast moves haven't been looked at.
- Going down only happens in co-op and to a game's own player. A death that just destroys the body (the game's "destroy" kill method, which no player body uses in the demo) is still instant. A player who is down and gets more damage just stays down; a worse death (gibbed over a plain one) replaces the pending one, but nothing hurts them or shortens the 15 seconds.
- Reviving (`Reviving`) takes food, a pill that heals, a cup of water or blood, or bare hands (hold a hand button for 3 seconds). A slap doesn't do anything. Anything else in a cup (dirt, cleaner) or other pills are used on yourself as in single player. The health a revive gives is fixed per way (food 30, healing pills 60, water 20, blood 35, bare hands 15), not read from the item. Reviving someone from a game that is not the host's or theirs goes through the host (one more hop).
- Carrying a downed player (`Players/Carrying`): aim at them with an empty hand and tap that hand's button (holding it revives, see above), press either hand's button to put them down. The downed player's own game moves its body onto the carrier's shoulder (as that game sees the carrier, so others see it a moment behind) and decides who carries them, one carrier at a time. The hand that picked them up can't pick things up or punch while carrying (the other hand is free, and can feed or give a drink to the one carried).
- Other players' hands are mittens with no finger bones to close on a grip, and the wrist doesn't turn: a hand holding something just meets it. A reaching arm stretches up to 40% when the hand is far from the shoulder, so a big reach looks long. Sitting, crouching and the other body poses keep the animator's arms when a hand isn't up, and a body that is down, carried or lying doesn't reach or tilt its head.
- Remote bodies are on the Player layer and can be seen and hurt: hits, explosion pushes and gibbing are passed on to the player's own game (see World). A kill plane or teleporter that a remote body touches here sends that player to the same spot in their own game (`World/Hazards/BodyHazards`); it checks bounds, once per physics step, so a body that only grazes a hazard's edge may be sent or not. If their own game already moved them (they touched it there too) the second move is skipped, but on a slow connection a body can be pulled back to the hazard's spot a moment after they walked off. The demo's only teleporter is switched off and its offset is zero, so that path has only been tried with a changed offset.
- Clothes show by item name, so a dyed or custom piece shows as the plain item.

### Players tab

- Kick doesn't ban: a kicked player can join again straight away.
- Freeze slows the player to a tenth of their speed for 8 seconds rather than stopping them (the game's speed stat has a floor).
- Items and critters for a player appear in front of their body as the host sees it, a moment behind where they really are.
- The hitman from the tab takes one job at a time, like the line.

### Mod setup

- Which mods only change your own game is a fixed list in Co-op (`Session/ModList.cs`), not a mod.json setting.
- A client can't follow a mod it doesn't have: when the host turns one on, the client leaves with the reason.
- Only settings marked `[Shared]` follow the host; every other setting stays each player's own.

### Co-op content

- The hitman line is listed on Phone Expansion's Phone page outside co-op too; dialing it there gets a busy line.
- Its cooldown counts play time in the host's save, so another host's house has its own.

### Watch list

- MushroomBoy was found once at y -84162 on the host, fallen through the floor; probably its rolling physics, seen before any test touched it.
- A cop knocking an item out of a remote player's hand: the forwarding is tested (the item drops in their game and goes back to the host), but the trigger, the game's own (something that isn't a door blocks the shot, under 1.7 m, while chasing), hasn't been seen in play yet; in tests the cop's ray passed under the held box and they shot instead.
- Seen once, after a long session with dozens of hot reloads: doors stopped taking clicks in both games until both restarted. The game marks what a hand grabs with `Usable.player` (always together with `Player_Abilities.pickups`) and refuses to pick up anything whose `player` is still set, so a drag that ends without the game's own release (a reload mid-drag, a forced hand-back) leaves a door nobody can grab. `HoldRepair` now releases such holds (checked once a second, released the game's way after two checks, logged once per object); tested with a faked stuck door, and a real hold isn't touched. If doors or items stop taking clicks again, `CoopApi.LookAt`, `RayAll`, `LayerOf` and `Patched` show what the click meets.

### Scenes and story

- Nobody dreams in co-op: resting with your eyes shut restores energy but never loads the dream.
- The story's scene changes (the nightmare, the soul trade, story deaths that load a scene) are skipped, so story beats that need them can't happen in co-op.
- Going back to the main menu leaves the game (for the host, it ends it for everyone).

### On a client

- A client never saves. Their progress in the host's house exists only in the host's save (and see "Bodies aren't kept" above).
- Timed content from mods (Phone Expansion's incoming calls, Email Expansion's mail) only happens on the host.
- If the Co-op mod is switched off (not reloaded) while in someone's house, the game goes back to the main menu after 10 seconds.

### World

Shared today: items and NPCs appearing and going away, windows and other breakables breaking, where every item, door and NPC is (and whether it's shown), who holds what, door locks, light switches, the fuse box (breakers, which fuse sits in which slot, how charged each fuse is), smoke detector batteries, the TV, the radio, taps, stove knobs, flashlights, BBQ lighters, cardboard boxes (opened, taped, what's frozen inside), room fires, paint (blood, dirt, water, scorch, footprints, cleaning, and water drying), the cop van, and on the computer the money, store orders, apps, stock market, tax bills, the anime card game and the inbox. The lines below are what isn't, or only partly.

State of things:

- Other item state (cooked, filled, burning, a glass's drink) is only sent when the item spawns; later changes stay on the machine that made them.
- The stove's knob is shared, but what the gas does afterwards (lighting, exploding) happens on each machine on its own.
- Fire: the host spreads it and clients get its flames once a second, so a client sees a growing fire in steps. A flame a client puts out goes out on the host too (the nearest one). Candles and other flames a fire lights on a client stay lit only there.
- The inbox is the host's: every email that arrives there reaches everyone (a joining player gets the whole list). Which emails you've read is your own. Email Expansion's conversations are shared: anyone can open and answer them, the host's game runs them, and everyone sees the same conversation. What an email does (money, deliveries) happens once, on the host.

Picking things up and pushing them:

- What a client's game frees by itself without a hand (clothes the game takes off, what a dead player had falling where they died) becomes that client's to simulate (`World/Ownership/FreedReplicas`: a replica that turned dynamic is claimed within a quarter second), so the host and everyone else see it fall and land in the same place.
- Clients can pick up rats, the cat and dead NPCs: the host pauses the animal while someone else holds or throws it and wakes it once it lands. A rat held by a client bites and wriggles in its hand like in the host's (`World/Ownership/HeldAnimals`: the client's game does what BrainRat.Update does for a held rat, since the rat's brain only runs on the host).
- A player takes over what their body, what they hold, or (on a client) what they threw runs into, so it can be pushed. Things only knocked on by other knocked things move on the host first and reach the others a moment later.
- Breakables that aren't items (window panes, the glass cases and coffee table, the walls that can be knocked down) are decided by the host (`World/Breakables`): what a client throws or runs into one with becomes a request, and the host counts the hit and breaks it with the game's own settings. Everyone hears the thud, the break and sees the pane go; the shards come as ordinary spawns. A pane broken on the host stays broken for players who join later (the host lists which panes are still whole when someone is ready), and again after a client's Co-op reload. The thief breaking in and explosions go through the same path. A pane's id is its names from the root down plus its position in centimetres, so a game that reloads after a window is gone still matches. A client's pane breaks a ping after the hit (a few milliseconds on a LAN), and its hit count only changes when the host answers.
- Items that break (plates, mugs, the piggy bank, doors, corpses that gib) break where they're simulated, as before; the others now also hear the break and see its particles. The shards, gibs and bones arrive as spawns.
- Item logic that replaces, wears down or uses up the item runs only on the machine that simulates it: damage (burns, cuts, hits, so liquid bombs and lighters going off too), food spoiling, cooking and soaking, breaking, ice melting, glasses tipping and filling, fuse and lighter explosions, fuses snapping into the fuse box or a smoke detector, dust balls merging (only between two balls simulated on the same machine), the cat bowl, the piggy bank and baths soaking things.
- Toilet handles move for everyone: the game where one is pulled sends its angle 15 times a second while it's held and springs back, and the others show it (kinematic, with their own flush check off, playing the flush sound). What a flush takes away is gone everywhere (`World/Ambience/ToiletSync`).
- The one thing the game makes with a body and no item name is the explosion's fireball (a fuse or a BBQ lighter going off, `FireParticle_Explosion`): it burns only in the game where it went off, while the blast's damage and push reach the others. The log names it once ("has no item name"). Everything else the game makes while playing has an item name and is shared: shards, gibs, organs, bones, rat skins, crushed food and door kits. Blood, pee, puke, poop and sprays are made by every game on its own (paint is shared). `Coop.World.Breakables.UnnamedTestApi` lists these again after a game update.

NPCs:

- NPCs think only on the host. Clients see their position and rotation (corpses included), the walking, sitting, crouching (the body's height too, so hits and grabs match) and death animations, dancing, speed, raised hands, kicks (`World/Npcs/NpcRig`, a kick counter in the pose), taking clothes off (the same `NpcClothes` message as dressing, with an empty slot) and the blood of being skeletonized or obliterated (`NpcEffects`, message 155; the NPC, skeleton, gibs and paint are shared already). A player stripping a corpse in a client's game goes through the host's NPC (message 156). Not shown on clients: the dying groan, the skeleton's red flush while it forms, masks taken off an NPC.
- What hurts a player hurts their real body, whoever's game it happened in: NPC punches, cop shots, player punches and kicks, knives, guns, traps and explosions (with their push). Burns come from each game's own copy of the fire. A client's hits on an NPC go to the host's real NPC.
- Enemies go after the nearest player who's up, host or remote: the cop van's agents, the hostile NPC, house spiders (within their 4 m reach) and the skeleton parasite. A downed player counts as dead for them. Cops shoot their one target until it's down or dead and then leave, like the game's raid after its one kill. The hostile NPC goes after the next player, anywhere in the house. A cop up close knocks things out of a remote player's hands like out of the host's. The number of cops doesn't grow with more players.
- `Coop.World.Contracts.SendHitman(slot, delay)` (host) sends the cop van after one player only, wherever they are; the agents leave once that player is down, dead or gone. One at a time, and a raid's van already in the street goes first. A hot reload of Co-op turns a hitman in progress into an ordinary raid.

Conversations and the phones:

- Anyone can talk to NPCs and use the phones. The story runs only in the host's game, so what's said and what it does (items, money, NPCs leaving, story flags) happens once, there. A remote player's conversation doesn't use or block the host's dialogue box.
- One conversation per speaker. Anyone else who tries, the host included, gets a "busy" line naming who's talking. Hurting a speaker or grossing them out cuts the other conversation off, like the game closing its dialogue first.
- Only the player who's talking sees the lines and choices and hears the voice. Other players nearby see and hear nothing of it.
- What the story knows about the talker (what they hold, fat, muscle, woman, naked) comes from their own game with each step. KillPlayer, SkeletonizePlayer and KnockoutPlayer happen to the talking player, and so do the content rules about the player: the `stat`, `holding` and `room` conditions read the talker's stats, hands and position (their game sends its stats and held items with every request), and the `stat` and `knockout` actions happen to the talker's body in their game. Trading your soul for a clone takes the talker's soul; the clone appears at the speaker with their body type. Email Expansion's clone hate mail only follows the host's own trade. Email Expansion's replies work the same way: a client's reply carries their stats and held items, and the rules in it are about whoever answered.
- Offerings work in everyone's conversations: the candy bucket comes out on every machine, and what lands in it reaches the host's story for that conversation (the host takes the item; the story keeps, gives back or destroys it). The bucket is one object, so two offering conversations at once share it, as in the game. Items that run a bit of story when picked up or used run it in the host's story, whoever picked them up (untested: no such item found in the demo yet).
- Both handsets are the host's: a client's button press is pressed on the host's handset, and the screen (caller ID included), ring and busy tone come back within a quarter second. Answering or dialing makes the call that player's conversation. The host walking past a handset doesn't hang up someone else's call. Prank calls and Phone Expansion's incoming calls are scheduled by the host and ring on every machine.
- A remote player's conversation open during a hot reload of Co-op closes; walking back up to the speaker picks it up where the story stopped.

Money and the computer:

- One shared wallet, kept by the host. Everything a client buys on the computer is a request the host carries out with the game's own checks: store orders (every store, the custom shops, and items mods put in the stores), apps, stocks, tax bills and the card game. So the money check, the demo block on the black and occult markets, the cop van's roll and the delivery box happen once, on the host, and two players can't spend the same money twice. Mods that change orders (Randomizer's mystery deliveries) do it on the host's run.
- A client hears its order or app go through or get refused once the host answers (a few milliseconds on a LAN). A stock trade or card upgrade the host turns down still played its "done" sound on the client, and the card game shows a client's clicks and upgrades at once, then the host's numbers half a second later.
- A custom photo or tape from the custom shops is unpacked by whoever opens its box, wherever it was ordered; its file is shared like any other (see "Files" under How it works).
- Card game experience and crystals come from the host twice a second, so the bars move in small steps on clients.

Paint:

- A room fire's scorch is merged before it's sent (two splats per spot and second), so a client's scorch covers a few percent less than the host's (2 to 9% measured), and a growing fire sends about 8 KB/s to each client.
- Taps (sinks, the bath) run their own water in every game, so each game paints its own wet spots under them; they differ a little.
- Paint made during a hot reload of Co-op (the batch not yet sent, about 0.1 s) is lost, and so is paint made while someone joins past the first 1 MB.
- Splats travel rounded (to a millimetre and about a sixth of a degree), so the games agree to within about 1% of the painted area per surface; a splat's edge can be a texel off.
- Paint already in the house when someone joins only shows for them with Unofficial NEET Patches and its `LoadedPaint` fix on while their game loads the house: without it the game sets those surfaces up before they're ready and draws nothing on them (see [known-bugs.md](../game/known-bugs.md)).

Other:

- The sun and moon turn a fixed step per frame, so they'd drift apart between games at different frame rates: clients ease toward the host's twice a second. Thunder strikes when the host's does (`World/Ambience/SkySync`). A client's first strike of a session can still be its own, since its thunder was scheduled before co-op started.
- During a hot reload of the Co-op mod (about 0.2 s), changes aren't sent. The resync afterwards fixes items, but something a client used up or destroyed during its own reload comes back.
- A few scene objects with no id of their own are matched by their place in the scene. A mod that changes the scene differently on two machines would break that match.
- Things that fall below the house (a body without a floor under it) stop being sent once they're 1 km down. Clothes parked by the game used to do that after every load; Unofficial NEET Patches parks them again now (`WornClothes`).

Cost (two games on one PC, a house with about 550 shared things and 13 NPCs; `WorldApi.Timing`, `perf.frames`):

- World's own work per physics step: about 0.1 ms on the host and 0.07 to 0.09 ms on a client, idle or with 30 items falling; the worst step was 1.2 ms, on the frame 30 items spawned at once. Per rendered frame it's under 0.02 ms.
- Allocations stay at about 2 KB per frame for the whole game on both sides (the game alone measured about 11 KB per frame in earlier single-player runs), with no garbage collections from World in normal play.
- The per-tick work only walks what moved lately (plus a quarter of the house per tick to notice things the physics woke up), NPC rigs and marked states; whole-house walks are left for joins, resyncs and reloads. A body knocked into motion by something World doesn't see can start streaming up to 0.2 s late.
- Paint: about 20 bytes a splat. Eight blobs of blood landing make about 150 to 300 splats (4 to 7 KB); someone walking with bloody feet about 10 a second; a room fire, merged, about 8 KB/s after half a minute. Sending costs a matrix inverse and a few lookups per splat; a client's copy allocates the game's own `Paint` object per splat.
- Network traffic, whole mod (`CoopApi.Traffic`): idle, about 6 KB/s from the host to each client and 1 KB/s back (player and NPC poses, stock ticks, the card game). With 30 bricks dropped from 10 m, about 9 KB/s from the host over the 4.5 s they fell and settled.

## Testing with two games on one machine

```sh
neet launch --direct --wait 120          # instance 1, bridge on 27850
neet launch --instance 2 --wait 120      # instance 2, bridge on 27851, logs *-2.log
neet call type.call type=Coop.CoopApi method=Host
neet call type.call type=Coop.CoopApi method=Join 'args=["127.0.0.1"]' --instance 2
neet call type.call type=Coop.CoopApi method=Status --instance 2
neet logs --instance 2
```

Both games share the save folder. The client never saves, but anything that saves on the host (sleeping, `game.save`) writes a real save.
