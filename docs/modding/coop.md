# Co-op

The Co-op mod (`mods/Coop`) lets several games share one house over the network. The host's house is the world: its items, money, NPCs and story. Every player keeps their own body: health, needs, status effects and clothes. The earlier feasibility study is in [docs/research/coop.md](../research/coop.md); the implementation differs from it where noted below.

## Playing

- **Host:** load a game, open the mod menu (F1), **Co-op** tab, **OPEN MY HOUSE**. Others connect to your address on UDP port 8889 (setting **Port**). The port has to be open in your router (forwarded to your PC) and in your firewall. On Linux with firewalld: `sudo firewall-cmd --add-port=8889/udp` (add `--permanent` to keep it). Without a forwarded port, a VPN like Tailscale or ZeroTier works: join the host's VPN address.
- **Join:** **Co-op** tab, type the host's address (`203.0.113.5` or `203.0.113.5:8889`), **JOIN**. You don't need a save of your own; yours are never read or written while you're in someone else's house.
- **Leave:** **LEAVE**, or go back to the main menu. A client goes back to the main menu when the game ends. When the host leaves, the game ends for everyone.
- While in co-op you can rest with your eyes shut, but nobody dreams, and the story's scene changes wait (they'd take one player, or the whole house, away from the others).
- **The host decides the mods:** the mods that shape the shared house (all but Cheat Menu, Performance Fixes, Open Saves, No Feedback, the dev bridge and Co-op over Steam), their features and their shared settings (prices, chances, timings) are the host's. A client's mod menu shows them locked. When the host loads or unloads one of those mods, switches a feature or changes a shared setting, every client follows within a second; leaving gives a client its own setup back (nothing of the host's is saved on the client).
- The mod menu and the cheat menu don't pause the game in co-op: the house keeps going for everyone while you're in a menu.
- **The hitman line:** in co-op, call 448-626 on either phone. For $69 the hitman takes a job on any player in the game, yourself included: about a minute later the cop van comes, its agents go straight for that player wherever they are, and they leave once the target is down or dead. One job at a time, and after an order the line won't take another for a while (setting **Hitman line cooldown**, 10 minutes of play by default; the host's setting counts). Outside co-op the line is busy.

Settings (Mods page, Co-op): **Your name** (shown over your head), **Port**, **Max players** (2 to 4), **Hitman line cooldown**, **Physics hands on other players** (experimental).

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

### Mod setup

`Session/ModSync`: the host sends its setup (ModSetup: the shared mods with versions, their features' states, the `[Shared]` settings) to a player when they're ready, to everyone half a second after something in it changes, and to a client that asks (after joining or a hot reload). A client takes the framework's `ModControl` lock for the shared mods (`ModList.IsShared`), loads and unloads mods to match, and applies the features and settings as overrides (`ModFeatures.Override`, `SharedSettings.Apply`), which are never saved. A shared mod the host runs in a version the client doesn't have gets 10 seconds to catch up (a hot reload reaches every game a moment apart) before the client leaves with the reason. Leaving undoes it all: the lock goes, the overrides are cleared, and mods loaded or unloaded for the host are put back.

### Joining: the host's house, loaded from memory

1. The host takes a snapshot with `NeetModKit.Saves.SaveCapture.Take()`: the game's own save data (mod data included) and the paint, without saving (about 200 ms and 130 KB on a typical house).
2. `SnapshotSanitizer` gives the joiner a body of their own: the `Player` entry keeps its place in the save but loses its sub-saves, so the game starts it like a new player (full needs, empty hands, its default clothes), in front of the host. The host's open conversation is closed.
3. The client holds `SaveLock` (no `SaveAll`, no "reset save"), hands the snapshot to `SaveSource.Use` and loads `NEET_House` through the `Loading` scene. The game's own `LoadAll` reads the snapshot instead of the save folder.
4. On `HouseReady` the client sends Ready.

Returning players: every 10 seconds a client sends its body as the game would save it (`BrainPlayer.Save()`: position, stats, status effects, hands and clothes). The host keeps the latest one per player id in its own save (`BodyBook`, mod data), and the next join puts it into the joiner's `Player` entry instead of a fresh body (`BodyRestore`), with empty hands, and with the default outfit if a worn item is gone (the game's load stops on a missing item id).

### Players

- Each game sends its player's pose 30 times a second: position, the body's full rotation (so a slipped or downed body lies on everyone's screen), camera pitch and the body animator's bool parameters as a bitmask.
- Remote players are `PlayerPuppet`s: a copy of the player prefab's model (`Neet`: animator, skinned mesh, bones, hands) under a kinematic capsule. None of the game's player scripts are on it, so it reads no input, has no stats and is never saved. Poses play back 100 ms in the past from an 8-pose buffer.
- Arms: the player model also carries the first-person arms (`NeetHandL`/`NeetHandR`: a jointed chain of bones from the shoulder to a hand end, skinned to the arm mesh, which the player's own scripts pull with forces). By default a body's copy drops their joints, bodies and colliders and hides them, so it shows the body mesh's own arms. With the experimental setting **Physics hands on other players**, the copy keeps the chains (joined to its own body, without colliders, so they can't push anything in the house) and `PuppetHands` moves each hand end to where that player's hand is; the pose carries both hand ends relative to the body and whether each hand shows.
- `AppearanceSync` sends each player's look when it changes (clothes by item name, the outfit's UV set, the fat, body type and muscle blend shapes). Bodies get their own mesh copy, because the game swaps clothing UVs on the shared mesh.
- **Going down** (`Downed`): in co-op a player whose health runs out goes down instead of dying (a prefix on `DieAccessor` for this game's own player). They fall like a slip into the death pose (the animator's `isDead`) and can't get up, health stays at 1, they take no damage, and their eyes stay open, so they see the house rather than the death screen; a line on screen counts down 15 seconds. Anyone who uses food while looking at them (a prefix on `Food.Use`, a sphere cast from the camera) feeds it to them instead: the food is used up and they stand up with 30 health. Otherwise, after 15 seconds the game's own death and respawn happen. Each game tells the others when its player goes down or gets up; the name over a downed body says so, and `Downed.IsDown(slot)` lets enemies leave them alone.

### Steam transport

- `SteamTransport` (CoopSteam) implements `ITransport` with `ISteamNetworkingSockets` peer to peer: the host listens (`CreateListenSocketP2P`, virtual port 0), clients `ConnectP2P` to the host's SteamID. Steam encrypts the traffic and authenticates each SteamID; only owners of the game can connect.
- Two send lanes: Bulk (the snapshot) on a lower-priority lane, everything else on the other. Messages over 256 KB are cut into pieces and put back together (Steam's limit is 512 KB). Stream packets carry a 16-bit counter so stale ones are dropped. The send rate is raised to 2 MB/s and the send buffer to 8 MB.
- Framing: the first byte is the channel (bit 6 marks a piece of a bigger message, followed by a flags byte: 1 first, 2 last); Stream adds the counter.
- Buffers are built and read in place in Steam's own message structs (`Native/MessageLayout.cs`), so nothing allocates per message. The struct layout is checked against a real message when Steam starts; if it doesn't match, the transport refuses to run.
- The Steam API starts lazily, the first time Steam is used (retried every 5 seconds after a failure). Callbacks are pumped from `Poll()`.
- `Steamworks.NET.dll` (MIT, Windows-x86 build of release 2024.8.0, Steamworks SDK 1.60) is committed in `mods/CoopSteam/lib/` with its licence: the NuGet package has no x86 build, and it must match the `steam_api.dll` that `neet steam` fetches from the same release. SDK 1.60 is on purpose: SDK 1.64's `steam_api.dll` asks for interface versions (Apps 009, RemotePlay 004) that GE-Proton 10's Steam bridge doesn't have, and Steamworks.NET won't start without every interface. If start-up fails anyway, the message names the missing interfaces (`Native/InterfaceCheck.cs`). Update both together (`lib/`, and the URL, size and hashes in `tools/NeetTool/Game/SteamApiSource.cs`). `steam_api.dll` is never committed or packaged; `build/package.sh` stops if one ends up in `dist/`. Licence reasoning: [coop-steam.md](../research/coop-steam.md#licensing).
- `CoopSteam.SteamProbe.Run()` returns one line (init result, SteamID, persona name, app id, overlay, relay status), for checking the Steam side through the dev bridge without a session.

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

### Steam

- **Never joined over Steam yet.** The Steam API starts under GE-Proton10 (`SteamProbe.Run()`), the relay gets ready and a game hosts over Steam, but nobody has joined: that needs a second Steam account on another machine.
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

- Physics hands on other players (experimental setting) has only been checked by eye: an arm reaching for a held item looks right; fast moves and two-handed holds haven't been looked at.
- Going down only happens in co-op, to a game's own player, and only when the death runs through `DieAccessor`: being gibbed, skeletonized or killed by a story event is still instant death. Reviving takes food; nothing else (medicine, a slap) works yet. There's no carrying a downed player.
- Remote players have the default face, and their hands stay in the animation's pose: no arm reach, no held-item grip, no kick animation, no hand raising. Looking up and down only tilts the head a little.
- Remote bodies are on the Player layer and can be seen and hurt: hits and explosion pushes are passed on to the player's own game (see World). Teleports done to a remote body (kill planes, the teleporter) aren't passed on.
- Clothes show by item name, so a dyed or custom piece shows as the plain item.

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

Shared today: items and NPCs appearing and going away, where every item, door and NPC is (and whether it's shown), who holds what, door locks, light switches, the fuse box (breakers, which fuse sits in which slot, how charged each fuse is), smoke detector batteries, the TV, the radio, taps, stove knobs, flashlights, BBQ lighters, cardboard boxes (opened, taped, what's frozen inside), room fires, the cop van, and on the computer the money, store orders, apps, stock market, tax bills, the anime card game and the inbox. The lines below are what isn't, or only partly.

State of things:

- Other item state (cooked, filled, burning, a glass's drink) is only sent when the item spawns; later changes stay on the machine that made them.
- The stove's knob is shared, but what the gas does afterwards (lighting, exploding) happens on each machine on its own.
- The radio's tape starts from the top on each machine; the play position isn't shared.
- Fire: the host spreads it and clients get its flames once a second, so a client sees a growing fire in steps. A flame a client puts out goes out on the host too (the nearest one). Candles and other flames a fire lights on a client stay lit only there.
- The inbox is the host's: every email that arrives there reaches everyone (a joining player gets the whole list). Which emails you've read is your own. Email Expansion's conversations are shared: anyone can open and answer them, the host's game runs them, and everyone sees the same conversation. What an email does (money, deliveries) happens once, on the host.

Picking things up and pushing them:

- What a client's game frees by itself without a hand (clothes the game takes off, what a dead player had falling where they died) becomes that client's to simulate (`World/Ownership/FreedReplicas`: a replica that turned dynamic is claimed within a quarter second), so the host and everyone else see it fall and land in the same place.
- Clients can pick up rats, the cat and dead NPCs: the host pauses the animal while someone else holds or throws it and wakes it once it lands. A rat held by a client bites and wriggles in its hand like in the host's (`World/Ownership/HeldAnimals`: the client's game does what BrainRat.Update does for a held rat, since the rat's brain only runs on the host).
- A player takes over what their body, what they hold, or (on a client) what they threw runs into, so it can be pushed. Things only knocked on by other knocked things move on the host first and reach the others a moment later.
- The client that throws a plate breaks it. Scene windows and other breakables that aren't items only break on the host, so a client throwing something at a window doesn't break it.
- Item logic that replaces, wears down or uses up the item runs only on the machine that simulates it: damage (burns, cuts, hits, so liquid bombs and lighters going off too), food spoiling, cooking and soaking, breaking, ice melting, glasses tipping and filling, fuse and lighter explosions, fuses snapping into the fuse box or a smoke detector, dust balls merging (only between two balls simulated on the same machine), the cat bowl, the piggy bank and baths soaking things.
- Toilet handles move for everyone: the game where one is pulled sends its angle 15 times a second while it's held and springs back, and the others show it (kinematic, with their own flush check off, playing the flush sound). What a flush takes away is gone everywhere (`World/Ambience/ToiletSync`).
- Things with a body but no item name (some made at runtime) aren't shared. The log names each kind once ("has no item name").

NPCs:

- NPCs think only on the host. Clients see their position and rotation (corpses included), the walking, sitting, crouching and death animations, dancing, speed and raised hands. They don't see crouch height, kicks, or an NPC taking clothes off (dressing is shared, undressing isn't). Skeletonizing and gibbing should come through as the spawns and destroys they are; untested.
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
- A custom photo or tape from the custom shops is unpacked by whoever opens its box, wherever it was ordered; the item keeps the file's path, and each game loads the picture or song from that path on its own machine.
- Card game experience and crystals come from the host twice a second, so the bars move in small steps on clients.

Other:

- The sun and moon turn a fixed step per frame, so they'd drift apart between games at different frame rates: clients ease toward the host's twice a second. Thunder strikes when the host's does (`World/Ambience/SkySync`). A client's first strike of a session can still be its own, since its thunder was scheduled before co-op started.
- During a hot reload of the Co-op mod (about 0.2 s), changes aren't sent. The resync afterwards fixes items, but something a client used up or destroyed during its own reload comes back.
- A few scene objects with no id of their own are matched by their place in the scene. A mod that changes the scene differently on two machines would break that match.
- Things that fall below the house (a body without a floor under it) stop being sent once they're 1 km down. Clothes parked by the game used to do that after every load; Unofficial NEET Patches parks them again now (`WornClothes`).

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
