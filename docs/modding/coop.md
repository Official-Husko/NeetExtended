# Co-op

The Co-op mod (`mods/Coop`) lets several games share one house over the network. The host's house is the world: its items, money, NPCs and story. Every player keeps their own body: health, needs, status effects and clothes. The earlier feasibility study is in [docs/research/coop.md](../research/coop.md); the implementation differs from it where noted below.

## Playing

- **Host:** load a game, open the mod menu (F1), **Co-op** tab, **OPEN MY HOUSE**. Others connect to your address on UDP port 8889 (setting **Port**). The port has to be open in your router (forwarded to your PC) and in your firewall. On Linux with firewalld: `sudo firewall-cmd --add-port=8889/udp` (add `--permanent` to keep it). Without a forwarded port, a VPN like Tailscale or ZeroTier works: join the host's VPN address.
- **Join:** **Co-op** tab, type the host's address (`203.0.113.5` or `203.0.113.5:8889`), **JOIN**. You don't need a save of your own; yours are never read or written while you're in someone else's house.
- **Leave:** **LEAVE**, or go back to the main menu. A client goes back to the main menu when the game ends. When the host leaves, the game ends for everyone.
- While in co-op you can rest with your eyes shut, but nobody dreams, and the story's scene changes wait (they'd take one player, or the whole house, away from the others).

Settings (Mods page, Co-op): **Your name** (shown over your head), **Port**, **Max players** (2 to 4).

### Over Steam

The **Co-op over Steam** mod (`mods/CoopSteam`) adds Steam next to direct IP. Nobody forwards a port: Steam connects the games directly when it can and through its relay network when it can't, and it vouches for who is connecting.

- **Once:** fetch Valve's `steam_api.dll`, which the pack can't include. The installer asks; later, with the game closed, `neet steam` (`neet steam --remove` takes it out). It's downloaded from the Steamworks.NET release on GitHub and checked against a known hash.
- Start Steam, then the game from Steam, signed in to the account that has NEET Demo.
- **Host:** **OPEN OVER STEAM**. The Co-op page shows your SteamID64 (17 digits starting with 7656119); send it to your friends.
- **Join:** type the host's SteamID64 (or `steam:` and the number) and **JOIN**. A 17-digit Steam ID goes over Steam; anything else is a direct address.

## How it works

### Transport

- LiteNetLib 1.3.1 (MIT) over UDP, behind `Coop.Net.ITransport`, so another transport (Steam's relay) can be added without touching the rest. The Steam option is researched in [docs/research/coop-steam.md](../research/coop-steam.md).
- Four channels: `Stream` (sequenced, for poses and transforms), `Events`, `State` and `Bulk` (reliable and ordered; the join snapshot goes on `Bulk` so it never holds up anything else).
- Every message starts with a `ushort` id (`Coop.Net.MessageIds`: 1-19 session, 20-39 join, 40-59 players, 100-199 world). Handlers are registered with `Messages.On(id, handler)` and run on the main thread. Sending: `Outbox.Begin(id)`, fill the writer, then `Outbox.ToPlayers`, `ToHost` or `ToSlot`. Clients only talk to the host; the host passes on what the others need.
- The connection key carries `Protocol.Version`, and the hello carries the mod's version; mismatched games are turned away with a message.

### Session

`Coop.Session.CoopSession` holds the role (`Hosting`, or `Connecting`, `Joining`, `Playing` for a client), the players (slot 0 is the host) and the events everything else listens to: `PlayerArrived`, `PlayerReady`, `PlayerLeft`, `Welcomed`, `WorldReady`, `Resumed`, `Ended`.

Handshake: the client connects and sends Hello (protocol, version, player id, name); the host gives it a slot (Welcome, with the player list) and sends its house; the client loads it and sends Ready; the host tells the others (PlayerJoined) and raises `PlayerReady`, which is where full state goes out.

### Joining: the host's house, loaded from memory

1. The host takes a snapshot with `NeetModKit.Saves.SaveCapture.Take()`: the game's own save data (mod data included) and the paint, without saving (about 200 ms and 130 KB on a typical house).
2. `SnapshotSanitizer` gives the joiner a body of their own: the `Player` entry keeps its place in the save but loses its sub-saves, so the game starts it like a new player (full needs, empty hands, its default clothes), in front of the host. The host's open conversation is closed.
3. The client holds `SaveLock` (no `SaveAll`, no "reset save"), hands the snapshot to `SaveSource.Use` and loads `NEET_House` through the `Loading` scene. The game's own `LoadAll` reads the snapshot instead of the save folder.
4. On `HouseReady` the client sends Ready.

Returning players: every 10 seconds a client sends its body as the game would save it (`BrainPlayer.Save()`: position, stats, status effects, hands and clothes). The host keeps the latest one per player id in its own save (`BodyBook`, mod data), and the next join puts it into the joiner's `Player` entry instead of a fresh body (`BodyRestore`), with empty hands, and with the default outfit if a worn item is gone (the game's load stops on a missing item id).

### Players

- Each game sends its player's pose 30 times a second: position, the body's full rotation (so a slipped or downed body lies on everyone's screen), camera pitch and the body animator's bool parameters as a bitmask.
- Remote players are `PlayerPuppet`s: a copy of the player prefab's model (`Neet`: animator, skinned mesh, bones, hands) under a kinematic capsule. None of the game's player scripts are on it, so it reads no input, has no stats and is never saved. Poses play back 100 ms in the past from an 8-pose buffer.
- `AppearanceSync` sends each player's look when it changes (clothes by item name, the outfit's UV set, the fat, body type and muscle blend shapes). Bodies get their own mesh copy, because the game swaps clothing UVs on the shared mesh.
- **Going down** (`Downed`): in co-op a player whose health runs out goes down instead of dying (a prefix on `DieAccessor` for this game's own player). They fall like a slip and can't get up, health stays at 1, they take no damage, and their eyes stay open, so they see the house rather than the death screen; a line on screen counts down 15 seconds. Anyone who uses food while looking at them (a prefix on `Food.Use`, a sphere cast from the camera) feeds it to them instead: the food is used up and they stand up with 30 health. Otherwise, after 15 seconds the game's own death and respawn happen. Each game tells the others when its player goes down or gets up; the name over a downed body says so, and `Downed.IsDown(slot)` lets enemies leave them alone.

### Steam transport

- `SteamTransport` (CoopSteam) implements `ITransport` with `ISteamNetworkingSockets` peer to peer: the host listens (`CreateListenSocketP2P`, virtual port 0), clients `ConnectP2P` to the host's SteamID. Steam encrypts the traffic and authenticates each SteamID; only owners of the game can connect.
- Two send lanes: Bulk (the snapshot) on a lower-priority lane, everything else on the other. Messages over 256 KB are cut into pieces and put back together (Steam's limit is 512 KB). Stream packets carry a 16-bit counter so stale ones are dropped. The send rate is raised to 2 MB/s and the send buffer to 8 MB.
- Framing: the first byte is the channel (bit 6 marks a piece of a bigger message, followed by a flags byte: 1 first, 2 last); Stream adds the counter.
- Buffers are built and read in place in Steam's own message structs (`Native/MessageLayout.cs`), so nothing allocates per message. The struct layout is checked against a real message when Steam starts; if it doesn't match, the transport refuses to run.
- The Steam API starts lazily, the first time Steam is used (retried every 5 seconds after a failure). Callbacks are pumped from `Poll()`.
- `Steamworks.NET.dll` (MIT, Windows-x86 build of release 2024.8.0, Steamworks SDK 1.60) is committed in `mods/CoopSteam/lib/` with its licence: the NuGet package has no x86 build, and it must match the `steam_api.dll` that `neet steam` fetches from the same release. SDK 1.60 is on purpose: SDK 1.64's `steam_api.dll` asks for interface versions (Apps 009, RemotePlay 004) that GE-Proton 10's Steam bridge doesn't have, and Steamworks.NET won't start without every interface. If start-up fails anyway, the message names the missing interfaces (`Native/InterfaceCheck.cs`). Update both together (`lib/`, and the URL, size and hashes in `tools/NeetTool/Game/SteamApiSource.cs`). `steam_api.dll` is never committed or packaged; `build/package.sh` stops if one ends up in `dist/`. Licence reasoning: [coop-steam.md](../research/coop-steam.md#licensing).
- `CoopSteam.SteamProbe.Run()` returns one line (init result, SteamID, persona name, app id, overlay, relay status), for checking the Steam side through the dev bridge without a session.

### Hot reload

The mod reloads mid-session without dropping anyone, which keeps the build-and-try loop fast:

- `mod.json` lists only `Coop.dll`, so the loader treats `LiteNetLib.dll` as a library and never reloads it.
- On unload the `NetManager` and its listener are parked in `ReloadState` with the session (as JSON); the next copy picks them up, re-subscribes, and raises `Resumed`. A parked connection nobody picks up within 10 seconds is closed.
- Everything the mod puts in the scene is destroyed on `CoopLoop.Unloading` and rebuilt from the next poses and a resync.

## Known limitations

Everything co-op doesn't do yet, or does differently from single player. Each line is something to look into; when one is fixed, remove it here. Lines marked (world) belong to world sync (`mods/Coop/World/`).

### Connecting

- Direct IP: the host needs the UDP port forwarded in their router and open in their firewall (firewalld on Linux blocks it by default). There is no UPnP, NAT punch-through or relay; a VPN (Tailscale, ZeroTier) is the workaround.
- Over Steam (no port forwarding) needs the Co-op over Steam mod, which hasn't been run in a game yet; see the Steam list below.
- No password or allow list: anyone who has the address and the same mods can join. Traffic isn't encrypted.
- At most 4 players. No host migration: when the host leaves, the game ends for everyone.
- Both sides need the same mods at the same versions, except mods that only change your own game (Dev Bridge, Cheat Menu, Performance Fixes, No Feedback, Open Saves, Co-op over Steam). The message lists at most three differences.
- A connection carried over a hot reload keeps the channel layout it was opened with; after a change to the channels, leave and join again.

### Steam

- **Never run in a game yet.** It compiles and `neet steam` fetches and checks the DLL, but nobody has connected over it. Next: `SteamProbe.Run()` in one game (does the Steam API start under Proton, is the relay ready), then host and join with a second Steam account on another machine.
- One Steam account probably can't connect to itself, so two games on one PC can't test it (use direct there).
- No lobbies, no invites, no "Join Game" in the friends list, no rich presence: friends type the host's SteamID64. The overlay only exists when Steam started the game.
- No Steam auth tickets: the identity Steam gives each connection is trusted, but owner id under Family Sharing and VAC status aren't checked.
- Needs Steam running and online. Right after Steam starts, hosting or joining over Steam is refused for a few seconds until its relay network is ready. With `neet launch --direct` the game gets its app id from the tool; expected to work, untested.
- Needs a Proton whose Steam bridge has SDK 1.60's interface versions: checked for GE-Proton10-34, GE-Proton 11, Proton 11.0 and Experimental. An older Proton may fail to start Steam; the error names what's missing.
- 32-bit `steam_api.dll` only; with the 64-bit player (`neet install --64bit`) it would need `steam_api64.dll` and the matching Steamworks.NET build.
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
- Joining costs the host a hitch of about 200 ms per joiner (the snapshot), plus the time to send about 130 KB.
- If the host is in a conversation when someone joins, the joiner's copy of the story may start inside it (only the dialogue box is closed). Untested.
- Paint is sent at join only; paint made afterwards isn't shared. (world, M3)
- Things that come from local files (custom photos, custom cassettes from the cassette site, the player's face) show blank or default for the others.

### Players

- Going down only happens in co-op, to a game's own player, and only when the death runs through `DieAccessor`: being gibbed, skeletonized or killed by a story event is still instant death. Reviving takes food; nothing else (medicine, a slap) works yet. There's no carrying a downed player.
- Remote players have the default face, and their hands stay in the animation's pose: no arm reach, no held-item grip, no kick animation, no hand raising. Looking up and down only tilts the head a little.
- Remote bodies are on the Player layer and can be seen and hurt: hits and explosion pushes are passed on to the player's own game (see World). Spiders still ignore remote players (they want an enabled Brain), and NPCs only talk to the host. Teleports done to a remote body (kill planes, the teleporter) aren't passed on.
- Clothes show by item name, so a dyed or custom piece shows as the plain item.

### Scenes and story

- Nobody dreams in co-op: resting with your eyes shut restores energy but never loads the dream.
- The story's scene changes (the nightmare, the soul trade, story deaths that load a scene) are skipped, so story beats that need them can't happen in co-op.
- Going back to the main menu leaves the game (for the host, it ends it for everyone).

### On a client

- A client never saves. Their progress in the host's house exists only in the host's save (and see "Bodies aren't kept" above).
- Timed content from mods (Phone Expansion's incoming calls, Email Expansion's mail) only happens on the host.
- Clients can't talk to the people in the house (visitors, Grandpa, the thief): their story runs on the host, and a client's conversation would move its own copy of it. Only the host talks to them, and the others don't see that conversation. The phone works for everyone.
- If the Co-op mod is switched off (not reloaded) while in someone's house, the game goes back to the main menu after 10 seconds.

### World

Shared today: items and NPCs appearing and going away, where every item, door and NPC is (and whether it's shown), who holds what, door locks, light switches, the fuse box (breakers, which fuse sits in which slot, how charged each fuse is), smoke detector batteries, the TV, the radio, taps, stove knobs, flashlights, BBQ lighters, cardboard boxes (opened, taped, what's frozen inside), room fires, the cop van, and on the computer the money, stock market, tax bills, the anime card game and the inbox. The lines below are what isn't, or only partly.

State of things:

- Other item state (cooked, filled, burning, a glass's drink) is only sent when the item spawns; later changes stay on the machine that made them.
- The stove's knob is shared, but what the gas does afterwards (lighting, exploding) happens on each machine on its own.
- The radio's tape starts from the top on each machine; the play position isn't shared.
- Fire: the host spreads it and clients get its flames once a second, so a client sees a growing fire in steps. A flame a client puts out goes out on the host too (the nearest one). Candles and other flames a fire lights on a client stay lit only there.
- Prank calls only ring on the host.
- The inbox is the host's: every email that arrives there reaches everyone (a joining player gets the whole list). Which emails you've read is your own. Email Expansion's conversations are shared: anyone can open and answer them, the host's game runs them, and everyone sees the same conversation. What an email does (money, deliveries) happens once, on the host.

Picking things up and pushing them:

- Clients can pick up rats, the cat and dead NPCs: the host pauses the animal while someone else holds or throws it and wakes it once it lands. A held rat doesn't bite a client, and doesn't wriggle in a client's hands.
- A player takes over what their body, what they hold, or (on a client) what they threw runs into, so it can be pushed. Things only knocked on by other knocked things move on the host first and reach the others a moment later.
- The client that throws a plate breaks it. Scene windows and other breakables that aren't items only break on the host, so a client throwing something at a window doesn't break it.
- Item logic that replaces, wears down or uses up the item runs only on the machine that simulates it: damage (burns, cuts, hits, so liquid bombs and lighters going off too), food spoiling, cooking and soaking, breaking, ice melting, glasses tipping and filling, fuse and lighter explosions, fuses snapping into the fuse box or a smoke detector, dust balls merging (only between two balls simulated on the same machine), the cat bowl, the piggy bank and baths soaking things.
- The toilet handle doesn't show moving for the others, but what a flush takes away is gone everywhere.
- Things with a body but no item name (some made at runtime) aren't shared. The log names each kind once ("has no item name").

NPCs:

- NPCs think only on the host. Clients see their position and rotation (corpses included), the walking, sitting, crouching and death animations, dancing, speed and raised hands. They don't see crouch height, kicks, or an NPC taking clothes off (dressing is shared, undressing isn't). Skeletonizing and gibbing should come through as the spawns and destroys they are; untested.
- What hurts a player hurts their real body, whoever's game it happened in: NPC punches, cop shots, player punches and kicks, knives, guns, traps and explosions (with their push). Burns come from each game's own copy of the fire. A client's hits on an NPC go to the host's real NPC.
- Not passed on yet: spiders only go after NPCs and the host's player; a rat held by a client doesn't bite; NPCs and cops can't knock things out of a remote player's hands; the skeleton parasite only goes after the host's player.

Money and the computer:

- One shared wallet, kept by the host. A client's spend shows at once, but two quick purchases on a client can both pass the "enough money" check before the host's total comes back.
- Buying and selling stocks, paying tax bills, the card game's buttons and the cop van's roll on a client are requests the host carries out. A stock trade or card upgrade the host turns down still played its "done" sound on the client, and the card game shows a client's clicks and upgrades at once, then the host's numbers half a second later.
- Card game experience and crystals come from the host twice a second, so the bars move in small steps on clients.

Other:

- The sun and moon drift apart between machines (the game turns them a fixed step per frame), and thunder flickers at different times in each game.
- During a hot reload of the Co-op mod (about 0.2 s), changes aren't sent. The resync afterwards fixes items, but something a client used up or destroyed during its own reload comes back.
- A few scene objects with no id of their own are matched by their place in the scene. A mod that changes the scene differently on two machines would break that match.
- Some worn clothes in older saves aren't kinematic and keep falling below the house forever (a game quirk, fixed for new saves by Unofficial NEET Patches). World stops sending things once they're 1 km below the house.

Cost (two games on one PC, a house with about 550 shared things and 13 NPCs; `WorldApi.Timing`, `perf.frames`):

- World's own work per physics step: about 0.1 ms on the host and 0.07 to 0.09 ms on a client, idle or with 30 items falling; the worst step was 1.2 ms, on the frame 30 items spawned at once. Per rendered frame it's under 0.02 ms.
- Allocations stay at about 2 KB per frame for the whole game on both sides (the game alone measured about 11 KB per frame in earlier single-player runs), with no garbage collections from World in normal play.
- The per-tick work only walks what moved lately (plus a quarter of the house per tick to notice things the physics woke up), NPC rigs and marked states; whole-house walks are left for joins, resyncs and reloads. A body knocked into motion by something World doesn't see can start streaming up to 0.2 s late.
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
