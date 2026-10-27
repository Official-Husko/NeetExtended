# Co-op over Steam: research

A Steam transport for the Co-op mod, next to the direct-IP one: Steam's relay so nobody forwards ports, Steam identities so the host knows who joined, and friend invites. Nothing here is built yet. "Guess" marks anything not confirmed by source, docs or a run.

## Summary

| Topic | Choice |
| --- | --- |
| Library | Steamworks.NET 2024.8.0 (MIT, Steamworks SDK 1.60): `Windows-x86/Steamworks.NET.dll` and the `steam_api.dll` from the same zip. Facepunch.Steamworks 2.5.2 also has a 32-bit build, but its own API style. The newer 2025.164.1 (SDK 1.64) was tried first and doesn't start under GE-Proton 10-34: that Proton's Steam bridge lacks `STEAMAPPS_INTERFACE_VERSION009` and `STEAMREMOTEPLAY_INTERFACE_VERSION004`, which only SDK 1.64 asks for. SDK 1.60 asks for nothing that bridge lacks, and newer Protons keep the old versions. |
| Shipping | Its own optional mod (CoopSteam, depends on Coop). `Steamworks.NET.dll` can ship. `steam_api.dll` is not committed or put in the release zip; `neet` downloads the pinned zip on request, as it does for the 64-bit player. |
| Native load | `steam_api.dll` next to `Steamworks.NET.dll` in the mod folder, and loaded by full path with `LoadLibraryW` before the first Steamworks call. |
| App id | Nothing to set: Steam, and `neet launch --direct`, start the game with `SteamAppId`/`SteamGameId` 2054460. No `steam_appid.txt`, never `RestartAppIfNecessary`. |
| Transport | `ISteamNetworkingSockets`: `CreateListenSocketP2P` on the host, `ConnectP2P(hostSteamId)` on clients, a poll group on the host. Not the deprecated `ISteamNetworking`, not `ISteamNetworkingMessages`. |
| Auth | The socket already authenticates the remote SteamID, and only owners of the app can connect. Session tickets (`GetAuthSessionTicket`/`BeginAuthSession`) add the owner id under Family Sharing and VAC status; not needed at first. |
| Invites | A friends-only lobby plus rich presence `connect`. The overlay invite dialog only when the game was started from Steam. |
| Testing | One account probably can't connect to itself. Needs a second Steam account on another machine or a VM. Everything else is developed on the direct-IP transport. |

The biggest unknown is whether `SteamAPI_Init` succeeds in this 32-bit process under Proton. Spike 1 below answers it in minutes.

## Library and loading

- Steamworks.NET's init is `SteamAPI.InitEx(out string errMsg)`, returning `ESteamAPIInitResult` with a readable error (log it). Its interface versions (`SteamNetworkingSockets012`, `SteamUser023`, ...) match the `steam_api.dll` in the same zip, so use that one. If `InitEx` fails with "Failed to initialize CSteamAPIContext", the Steam bridge lacks one of the versions; CoopSteam asks for each one separately (`Native/InterfaceCheck.cs`) and names the missing ones.
- Callbacks use manual dispatch, pumped by `SteamAPI.RunCallbacks()` on the main thread every frame.
- Unity's Mono looks for a `DllImport("steam_api")` in the calling assembly's folder, and the loader loads extra DLLs with `Assembly.LoadFrom`, so `steam_api.dll` beside `Steamworks.NET.dll` should be found (guess). Loading it first by full path with a kernel32 `LoadLibraryW` P/Invoke (as `CleanExit.cs` already calls kernel32) removes the doubt.
- Hot reload: `Steamworks.NET.dll` is a library the loader never reloads (the mod lists only its own DLL), so its static state survives. Dispose `Callback<T>` objects on unload and register again on load; park socket, connection and poll-group handles in `ReloadState` like the LiteNetLib manager; never call `SteamAPI.Shutdown` on unload.
- Initialise lazily, the first time the player picks Steam, so players who never use it pay nothing.

## App id and Proton

- `proton run` with `SteamGameId` set starts the game through Proton's `steam.exe` helper, which sets up the Steam registry keys `steam_api.dll` reads (`Software\Valve\Steam\ActiveProcess`, `SteamClientDll`). `steamclient.dll` in the prefix forwards through `lsteamclient` to the running native Steam client. The prefix has the 32-bit `lsteamclient.dll`. This is the normal Proton path; that it works for a 32-bit process is a guess until Spike 1.
- Init needs the Steam client running as the same OS user and a licence for the app (the demo is free and released).
- Started without Steam (double-clicking `NEET.exe` on Windows), init fails with "no appID found"; the menu should say Steam isn't available.

## Relay and limits

- Valve's docs: P2P connections are relayed over Valve's network when needed, with no partner setup for P2P. That this holds for an app the mod author doesn't own on Steamworks is community evidence (transports that default to app 480) plus a guess for 2054460; Spike 3 proves it.
- Call `SteamNetworkingUtils.InitRelayNetworkAccess()` at init and wait for the relay status to be current. Only outbound UDP is needed: no port forward, no firewall change.
- Limits (SDK defaults): 512 KiB per reliable message, about 1200-byte payload per packet, 256 KiB/s send rate, 512 KiB send buffer.
- So: chunk the join snapshot into messages of 256 KiB or less, raise `SendRateMin/Max` to about 2 MB/s, keep each pose message under one packet, and add a sequence number to poses (Sockets has no sequenced mode). Use `ConfigureConnectionLanes` so the snapshot (low-priority lane) never starves poses and events.
- Send and receive through one pinned native buffer per transport, so nothing allocates per frame.

## Lobbies and invites

- `CreateLobby(FriendsOnly, 4)` with the host's SteamID64 and the Co-op version in the lobby data; clients `JoinLobby`, read the host and `ConnectP2P`. The lobby only carries presence.
- `CreateLobby` can return AccessDenied "if the game isn't set to allow lobbies"; that's the demo's Steamworks setting, which nobody here can change. Fallbacks that need no lobby: rich presence `connect` (Join Game in the friends list) and pasting the host's SteamID64.
- The overlay is injected by Steam when Steam starts the game; with `neet launch --direct` there is none.

## Testing

- One account in two local game copies probably can't connect to itself (GameNetworkingSockets and FizzySteamworks both say so; cheap to try).
- Real tests need a second account that has the demo, on another machine or a VM.
- `SteamNetworkingSockets.CreateSocketPair` gives an in-process loopback for testing the transport's framing and chunking with one account.

## Licensing

- Steamworks.NET and LiteNetLib are MIT and can ship in this GPL-3.0 project with their notices.
- `steam_api.dll` is Valve's proprietary redistributable. The Steamworks SDK agreement lets its licensee redistribute it; a mod author who never signed it can't rely on that, and Valve's own open-source note calls copyleft licences "problematic" with the SDK.
- So the project should never convey the file: players fetch it with `neet` into their own install. A GPL section 7 linking exception for the Steam mod is the owner's call. Keeping Steam in its own mod behind `ITransport` also leaves room to move it into a separate helper process later. Not legal advice.

## Plan

Mod CoopSteam (`mods/CoopSteam`): native loading and init, `SteamTransport : ITransport`, lobbies, invites.

Spikes, in order:

1. Init smoke test: load, `InitEx`, log the SteamID, persona name, app id (expect 2054460), overlay state and relay status; once started from Steam, once with `neet launch --direct`.
2. Socket pair loopback in one process: framing, chunking, lanes.
3. `ConnectP2P` between the two local copies (expected to fail), then a second account over the relay with ICE switched off to force the relay: a 1 MB join, pose latency.
4. Lobby create, join and invite with the second account; note whether `CreateLobby` is refused for the demo.

## Direct IP on a Linux host

Not Steam, but found on the way: firewalld's default zone doesn't let UDP 8889 in, so a Linux host needs `sudo firewall-cmd --add-port=8889/udp` (plus `--permanent` to keep it), on top of a forward in the router. Routers without UPnP need the forward done by hand, or everyone joins over a VPN such as Tailscale.

## Sources

- Valve: [networking](https://partner.steamgames.com/doc/features/multiplayer/networking), [Steam Datagram Relay](https://partner.steamgames.com/doc/features/multiplayer/steamdatagramrelay), [ISteamNetworkingSockets](https://partner.steamgames.com/doc/api/ISteamNetworkingSockets), [ISteamNetworkingUtils](https://partner.steamgames.com/doc/api/ISteamNetworkingUtils), [auth](https://partner.steamgames.com/doc/features/auth), [ISteamMatchmaking](https://partner.steamgames.com/doc/api/ISteamMatchmaking), [ISteamFriends](https://partner.steamgames.com/doc/api/ISteamFriends), [SDK init](https://partner.steamgames.com/doc/sdk/api), [overlay](https://partner.steamgames.com/doc/features/overlay), [SDK access agreement](https://partner.steamgames.com/documentation/sdk_access_agreement), [open source](https://partner.steamgames.com/doc/sdk/uploading/distributing_opensource)
- [GameNetworkingSockets](https://github.com/ValveSoftware/GameNetworkingSockets) (`steamnetworkingtypes.h`, the P2P source)
- [Steamworks.NET releases](https://github.com/rlabrecque/Steamworks.NET/releases), [Facepunch.Steamworks releases](https://github.com/Facepunch/Facepunch.Steamworks/releases)
- [Unity's Mono native library loading](https://github.com/Unity-Technologies/mono/blob/unity-2022.3-mbe/mono/metadata/native-library.c), [Proton's steam helper](https://github.com/ValveSoftware/Proton)
- [FizzySteamworks](https://github.com/AdrianCabrera/FizzySteamworks), [steamshim](https://github.com/icculus/steamshim)
