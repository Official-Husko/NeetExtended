# DevBridge

DevBridge is a mod that opens a TCP socket inside the game. Anything that can write a line of JSON to that socket can read game state, change values, call methods, spawn objects, click buttons and take screenshots. It exists for testing: the `neet` tool and the MCP server use it, and so can scripts. Source: `mods/DevBridge/`.

## Configuration

The mod writes its defaults to `Mods/DevBridge/config.json` on first run:

```json
{
  "Address": "127.0.0.1",
  "Port": 27850,
  "TimeoutMs": 15000
}
```

- `Address`: the IP address to listen on. It must be an address, not a host name. The default `127.0.0.1` accepts only connections from the same machine.
- `Port`: the TCP port. `neet` and the MCP server connect to `127.0.0.1:27850` unless `--port` says otherwise. Change both sides together.
- `TimeoutMs`: how long a request may wait for the game's main thread, in milliseconds. A command that takes longer returns a timeout error.

> **Warning:** the bridge has no authentication. Anyone who can reach the port can run every command, including `type.call`, which calls any static method in any loaded assembly, and `console.exec`. Do not set `Address` to `0.0.0.0` or to a LAN address. Under Wine the Linux host and the game share the loopback interface, which is why `127.0.0.1` works from the host.

The file is read when the mod loads. After editing it, restart the game or reload the mod with `mods.reload`.

## Protocol

The transport is newline-delimited JSON over TCP. Each request is one line:

```json
{"id": 1, "method": "player.get", "params": {}}
```

- `id`: any JSON value. It is echoed back unchanged so replies can be matched to requests.
- `method`: the command name. Lookups are case-insensitive.
- `params`: an object. It may be omitted or `{}`.

Each reply is one line:

```json
{"id":1,"ok":true,"result":{"position":[1.2,0.0,-3.5],"room":"Kitchen"}}
{"id":2,"ok":false,"error":"Unknown method 'playr.get'. Call 'help' for the list."}
```

A connection can carry many requests. Lines are answered in order. Blank lines are ignored. A line that is not valid JSON gets `"id":null` and an `ok:false` reply with the parser's message.

When the error comes from game code (or anything below the command itself), the reply also has a `stack` with the exception's stack trace, and `error` names the exception type when its message is empty. `neet call` prints both.

Raw example using bash's `/dev/tcp`:

```sh
exec 3<>/dev/tcp/127.0.0.1/27850
printf '{"id":1,"method":"ping","params":{}}\n' >&3
read -r reply <&3
echo "$reply"          # {"id":1,"ok":true,"result":"pong"}
exec 3>&-
```

With the `neet` tool the same call is:

```sh
neet call ping
```

which prints:

```json
"pong"
```

Passing parameters:

```sh
neet call player.teleport room=Kitchen
neet call player.set '{"stats":{"Hunger":100}}'
neet call obj.get target='#4312' component=Rigidbody member=mass
```

### Threading and deferred results

Socket work happens on background threads. Every command runs on Unity's main thread. The reply is sent when the command finishes. If the command does not finish within `TimeoutMs`, the reply is:

```json
{"id":3,"ok":false,"error":"'screenshot' didn't finish within 15000 ms."}
```

A command can return a `Deferred` instead of a value. The reply waits until the work completes on the main thread. Commands that do this are `screenshot` (captured at the end of the frame), `computer.view`, `mods.load`, `mods.unload` and `mods.reload` (when it reloads a single mod). Their results arrive a few frames later.

Errors raised inside a command come back as `ok:false` with the exception's base message. Typical messages:

- `Missing parameter 'name'.` A required parameter was not given.
- `No object at 'Root/Foo'.` The target did not resolve.
- `Unknown method 'x'. Call 'help' for the list.`

The client waits for the reply with its own timeout. `neet call --timeout` defaults to 30 seconds, while the game gives up after `TimeoutMs` (15 seconds by default). The game's limit is the shorter one for slow commands.

## Commands

The table lists all 53 built-in commands. Parameters marked `?` are optional. `help` returns the same list at runtime, with each method's parameter names.

### Core

| Method | Parameters | Description and result |
| ------ | ---------- | ---------------------- |
| `ping` | none | Check the bridge is up. Returns `"pong"`. |
| `help` | none | List every bridge method. Returns `method`, `description` and `params` for each. |
| `game.info` | none | Scene, `houseReady`, loader and Unity versions, fps, time scale, game time, and the mod list with state. |
| `game.quit` | none | Close the game. Returns `"bye"`. |
| `game.save` | none | Save now, like the autosave. Works only in the house. Returns `"saved"`. |
| `save.check` | `max?`: problems to list, default 20 | Runs every save subscriber (everything `SaveAll` writes) on its own without saving. Returns the count, counts by type, and the subscribers that throw or whose object is destroyed, with the item name and save id for items. |
| `game.timescale` | `value`: 0 pauses, 1 is normal | Set `Time.timeScale`. Negative values are clamped to 0. Returns the new value. |
| `scene.load` | `name`: `MainMenu`, `Tutorial`, `NEET_House`, `DreamZone`, `Nightmare1` | Load a scene by name. Returns `"loading"` without waiting for the load. |
| `rooms.list` | none | Every room in the current scene, keyed by room name, with its centre as `[x,y,z]`. |

### Logs and console

| Method | Parameters | Description and result |
| ------ | ---------- | ---------------------- |
| `log.tail` | `after?`: sequence number, `max?`: default 100, `level?`: `Debug`, `Info`, `Warning` or `Error` (default `Debug`) | Recent log lines from the game, mods and Unity. Returns `last` (the newest sequence number) and `lines` as `"seq [Level] [Source] message"`. Pass the last `last` as `after` to read only new lines. |
| `console.exec` | `command`: for example `get Mug` | Run an in-game console command (IngameDebugConsole). Returns the messages it logged, up to 200. |
| `console.list` | none | Every console command signature. |

### Mods

| Method | Parameters | Description and result |
| ------ | ---------- | ---------------------- |
| `mods.list` | none | Every mod with `id`, `name`, `version`, `state`, `enabled`, `hotReload`, `loadCount` and `error`. |
| `mods.reload` | `id?`: omit to reload every hot-reload mod | Reload one mod. Reloading every mod, or DevBridge itself or a mod it depends on, answers `"reloading"` first and reloads a quarter second later, so the reply is not lost. |
| `mods.load` | `id` | Load a mod that is not running. Returns `"loaded"`. |
| `mods.unload` | `id` | Unload a running mod and anything that depends on it. Returns `"unloaded"`. |
| `mods.rescan` | none | Look for newly added mod folders. Returns the ids found. |
| `mods.enable` | `id`, `enabled`: bool | Switch a mod on or off for the next launch. Returns `restartNeeded`. |

### Features

| Method | Parameters | Description and result |
| ------ | ---------- | ---------------------- |
| `features.list` | none | Every feature of every loaded mod, with `mod`, `id`, `name` and `enabled`. |
| `features.set` | `mod`, `id`, `enabled`: bool | Switch a feature on or off right away. The choice is saved. Returns the new state. |

### Settings

| Method | Parameters | Description and result |
| ------ | ---------- | ---------------------- |
| `settings.list` | none | Every settings section on the mod menu's Mod Settings tab, with `section` (its title) and `values`. |
| `settings.set` | `section`: title, `name`: field name, `value` | Change one setting and save it, which applies it the same way the Settings page does. Names are case-insensitive. Returns the new value. |

### Objects

Targets are described in [Target addressing](#target-addressing).

| Method | Parameters | Description and result |
| ------ | ---------- | ---------------------- |
| `obj.find` | `name?`: substring, `component?`: type name, `includeInactive?`: bool, `max?`: default 50 | Find GameObjects by name and/or component. Returns `name`, `id`, `path`, `active` and `position` for each. |
| `obj.tree` | `target?`, `depth?`: default 2 | Hierarchy under an object, or the scene roots when `target` is omitted. Each node has `name`, `id`, `active` and `children`. When the depth limit is reached, `children` is a count. |
| `obj.inspect` | `target`, `component?` | Without `component`, returns `name`, `id`, `path`, `active`, `position` and the component type names. With `component`, returns every readable field and property of that component. |
| `obj.get` | `target`, `component`, `member`, `depth?`: default 2 | Read one field or property. |
| `obj.set` | `target`, `component`, `member`, `value` | Write one field or property. Returns the value read back. |
| `obj.call` | `target`, `component`, `method`, `args?`: array | Call an instance method. Returns the method's return value. |
| `obj.active` | `target`, `value`: bool | Show or hide an object (`SetActive`). Returns `activeSelf`. |
| `obj.destroy` | `target` | Destroy an object. |
| `obj.move` | `target`, and `x`, `y`, `z` or `pos`: `[x,y,z]` | Move an object. A Rigidbody gets its velocity cleared first. Returns the new position. |

### Player and money

| Method | Parameters | Description and result |
| ------ | ---------- | ---------------------- |
| `player.get` | none | `position`, `room`, `stats` (every need and stat), `godMode`, `held` (item names) and `money`. Fails when there is no player object, for example in the main menu. |
| `player.set` | `stats`: object of stat name to number | Set needs and stats. Stat names may also be given as top-level keys. Returns all stats. |
| `player.teleport` | `room?`: Location name, or `x`, `y`, `z` or `pos` | Move the player. Returns the new position. |
| `player.god` | `enabled`: bool | Toggle god mode: no damage, and needs stay frozen. Returns the new state. |
| `money.get` | none | Current money. |
| `money.set` | `value`, `taxed?`: bool | Set money. Bypasses the 12% tax unless `taxed` is true. Returns the new money. |

### Screenshot

| Method | Parameters | Description and result |
| ------ | ---------- | ---------------------- |
| `screenshot` | `scale?`: 0.1 to 1, default 0.5, `format?`: `jpg` (default) or `png`, `quality?`: JPEG quality 10 to 100, default 80 | Capture the game window at the end of the frame. Returns `mimeType`, `width`, `height`, `scene` and `data` (base64). |

### Type members

These work on any loaded type, not only on scene objects. Type names follow the rules in [Target addressing](#target-addressing).

| Method | Parameters | Description and result |
| ------ | ---------- | ---------------------- |
| `type.get` | `type`, `member`, `depth?`: default 2 | Read a static field or property. |
| `type.set` | `type`, `member`, `value` | Write a static field or property. Returns the value read back. |
| `type.call` | `type`, `method`, `args?`: array | Call a static method. Returns its return value. |

### Menu and UI

| Method | Parameters | Description and result |
| ------ | ---------- | ---------------------- |
| `menu.open` | `page?`: tab title, for example `Mods` or `What's New` | Open the mod menu, optionally on a page. Returns whether it is open. |
| `menu.close` | none | Close the mod menu. Returns whether it is open. |
| `ui.click` | `target`: path or id of a button | Run the button's `onClick` handlers, even when the game blocks clicks on it. Fails if the object has no `Button`. Returns `"clicked"`. |
| `ui.text` | `target` | Text of the first TextMeshPro or legacy `Text` component on the object or under it, including inactive children. |

### World

| Method | Parameters | Description and result |
| ------ | ---------- | ---------------------- |
| `items.list` | `filter?`: substring | Every spawnable item name. |
| `items.spawn` | `name`, `room?`, and `x`, `y`, `z` or `pos?`, `count?`: default 1, max 50 | Spawn items in a room, at a point, or (when neither is given) near the player. Returns references to the spawned objects. |
| `npcs.list` | `filter?`: substring | Every spawnable NPC name. |
| `npcs.spawn` | `name`, `room?`, and `x`, `y`, `z` or `pos?` | Spawn an NPC in a room or at a point. Without a position it appears 1.5 m in front of the player. |
| `fire.start` | `room` | Set a room on fire. Returns `"burning"`. |
| `fire.stop` | `room` | Put out a room's fire. Returns `"out"`. |
| `power.set` | `on`: bool, `room?`: omit for the whole house | Force power on or off. Returns `on`. |
| `sky.get` | none | The sky: `moon` and `sun` (intensity, scene intensity, colour, shadows, rotation), `fog` (density, scene density, multiplier), `ambient`, `skybox`, `thunder` (`loopAlive`, `nextStrikeIn`, `noThunder`, `strikes` counted since the bridge loaded, `lastStrikeAgo`), `rain` (each system's rate, particle cap and live particles), `stormVolume` (the loop's volume now and the multiplier) and `weather` (the framework's state). |
| `sky.strike` | `story?`: run the story's own thunder sequence, `measure?`: wait and report the flash | Lightning now (`Sky.Strike()`). With `measure` the answer says how long the moon stayed above 1 (`secondsAbove1`, `framesAbove1`), its `peak`, when `OnStrike` came and where it ended up (`moonNow`). With `story` it fires the sequence the story uses, which stops the random loop unless ThunderLoop is on. |
| `sky.set` | `fog?`, `moon?`, `sun?` (set directly), `fogAmount?`, `rainAmount?`, `stormVolume?` (multipliers), `weather?` (drizzle, rain, storm) and `intensity?`, `thunderMin?` and `thunderMax?` (seconds between random strikes; `thunderMin=default` gives the timer back), `strikeIn?`, `reschedule?`, `reset?` | Change the sky. The multipliers, delay and weather belong to the bridge until `reset`. Returns what `sky.get` returns. |
| `rain.set` | `amount?`: multiplier, `reset?`, `system?` and `rate?`: lay one system's emission down directly | Scale the four rain systems. Returns the rain systems as in `sky.get`. |

### Computer, email and cards

| Method | Parameters | Description and result |
| ------ | ---------- | ---------------------- |
| `computer.view` | `site?`: site name, for example `anime`, `pun` or `stonk` | Turn the computer on, open a site if one is given, and stand the player in front of the screen. Returns the list of site names. |
| `computer.wake` | none | Hide the computer's screensaver. Returns `"awake"`. |
| `cards.get` | `all?`: bool, default true | Anime card game state: `crystals`, `maxUnlocked`, `extraCards` and `cards`. Each card has `id`, `name`, `object`, `locked`, `level`, `medal`, `income`, `price`, `exp` and `expToLevel`. `all: false` leaves out locked cards. |
| `cards.crystals` | `value` | Set the crystal count. Returns the new count. |
| `email.lists` | none | The game's email lists: `scheduled`, `random` and `conditional` knots with their titles, plus what's `delivered` and `pending` in the inbox. |
| `email.send` | `knot`, `title` | Send an email by its Ink knot (from the game's email story or a mod's), with the subject shown in the inbox. |

### Assets

| Method | Parameters | Description and result |
| ------ | ---------- | ---------------------- |
| `items.banks` | `filter?`: bank name | Every `PickupBank` (the game's item lists: the main one, each store, the custom shops) with its items and the components on each. |
| `assets.texts` | none | Every loaded `TextAsset` with its name and length. The game's Ink stories are `Master`, `Master_Email` and `Thoughts`. |
| `assets.text` | `name`, `path` | Write a `TextAsset` to a file. The game sees Windows paths: `Z:` is the Linux root under Proton (`Z:/tmp/Master.json`). Returns `path` and `chars`. |

### Performance

Release builds have no Unity profiler, so these measure the running game from the inside. Findings and numbers are in [docs/research/performance.md](../research/performance.md).

| Method | Parameters | Description and result |
| ------ | ---------- | ---------------------- |
| `perf.frames` | `seconds` (default 5, at most 12), `uncapped`: bool | Frame time average and percentiles, frames over 33/50 ms, GC count, how much was allocated (Mono heap growth between frames, per frame and per second), memory (Mono and Unity), and Unity's render counters (draw calls, batches, SetPass calls, triangles, shadow casters). `uncapped` lifts the game's 120 fps cap and vsync while sampling, to see the real headroom. |
| `perf.scene` | none | What's alive that costs time: cameras rendering (and into which texture), lights by type and which cast shadows, mesh and skinned renderers (visible), particle systems, rigidbodies (awake, kinematic), colliders, playing audio, enabled scripts by type, and the quality settings. |
| `perf.scripts` | `seconds` (default 5), `top` (default 30), `allocations`: bool | Patches every game script's Update, LateUpdate, FixedUpdate, OnGUI and Stay callbacks with a timer for a few seconds, then removes them. Returns script time per frame and the slowest methods. With `allocations` it also adds up how much the Mono heap grew during each method and ranks by that (`allocatedKBPerFrame`); the heap grows in blocks, so only totals over many calls mean something. |
| `perf.gc` | `count` (default 3) | Runs full garbage collections and times each: how long one GC freezes the game. |
| `perf.save` | none | Times one save. Writes a real save file. |
| `perf.savebreakdown` | `top` | Saves once with timers on the save steps and every object's Save method. Writes a real save file. |
| `textures.count` | `top?`: groups to list, default 25 | Every `Texture2D` and `RenderTexture` in memory, grouped by type, name and size, with a size estimate, how many render textures hold GPU memory (`IsCreated`) and render textures by format. Run `type.call` `UnityEngine.Resources.UnloadUnusedAssets` first to count only what's still referenced. |
| `textures.users` | `name` | What uses a texture: renderer materials (with the property), raw images and cameras rendering into it. |
| `leaks.find` | `depth?` (default 6), `type?`: type name to look for, `all?`: bool | Walks static fields, ScriptableObject assets and objects kept across scenes, and lists the ones that still lead to destroyed Unity objects, with a path to one of them. Slow on big object graphs. |

Timers add a little overhead to every timed call, so compare methods with each other rather than with an untimed run. Back up the save folder before `perf.save` and `perf.savebreakdown`.

To check for a texture leak: `textures.count` after an `UnloadUnusedAssets`, load the house again (`scene.load`), and count again. The counts should stay about the same.

## Target addressing

Commands that take a `target` accept one of two forms:

- `#<instanceId>`, for example `#4312`. The id comes from a previous reply (`id` fields). Ids change between sessions, so do not store them.
- A path from a scene root, separated by `/`: `Root/Child/Grandchild`. The first part is matched against the roots of every loaded scene and the DontDestroyOnLoad scene.

If no path matches, the whole string is compared with object names in the scene (exact match, not substring) and the first hit is used. This fallback does not work for a path containing `/`. Use a full path when names repeat.

Component names (`component`, `type`) are matched first against the components the object has, by short name or full name. Otherwise the name is looked up across loaded assemblies. Full names avoid ambiguity: `UnityEngine.UI.Button` is safer than `Button`. Lookups by short name prefer `Assembly-CSharp`, then `UnityEngine*`, then other assemblies. Types from assemblies left behind by hot reloads are skipped.

## Value conversion

Replies and inputs use one set of rules. The conversion code is in `src/NeetModKit/Values/JsonValues.cs`.

Output:

- Strings, booleans and numbers are sent as they are. Floats are rounded to 4 decimals.
- `null` is `null`.
- Enums are sent as their names, for example `"Kitchen"`.
- `Vector2`, `Vector3`, `Vector4` and `Color` are arrays: `[x,y]`, `[x,y,z]`, `[x,y,z,w]`, `[r,g,b,a]`. A `Quaternion` is sent as its Euler angles.
- A Unity object is sent as a reference: `{"name", "type", "id", "path"}`. `path` is present for GameObjects and components.
- `Type` values are sent as full names.
- Lists are sent as arrays, cut after 100 items with a final `"..."`. Dictionaries are sent as objects, cut after 100 entries.
- Other objects are expanded field by field, with `$type` giving the class name. Expansion stops at the depth limit, and the object is then sent as `"<TypeName>"`.

Input is converted to the target member's type:

- `Vector3` takes `[x,y,z]` or `{"x","y","z"}`. `Vector2` and `Quaternion` (Euler angles) work the same way.
- `Color` takes `[r,g,b]` or `[r,g,b,a]`, or an object with `r`, `g`, `b`, `a`. Alpha defaults to 1.
- Enums take a name (case-insensitive) or an integer.
- `Type` takes a type name.
- A Unity object takes a string: a path or `#id`. The string resolves to the GameObject, its Transform, or the named component.
- `null` gives the default for value types and `null` otherwise.
- Anything else goes through the JSON deserializer for that type.

Method arguments (`args`) are converted the same way, one per parameter. Overloads are picked by argument count, and parameters with defaults can be left out.

## Adding commands from another mod

The DevBridge mod exposes its command table as `Commands`. Other mods can add their own commands by getting the DevBridge instance through `ModRegistry`:

```csharp
using DevBridge;
using NeetLoader;
using NeetLoader.Mods;
using Newtonsoft.Json.Linq;

public sealed class MyMod : NeetMod
{
    public override void OnLoad()
    {
        var bridge = ModRegistry.Get<DevBridgeMod>();
        bridge?.Commands.Add(this, "mymod.count", "Count the widgets.",
            new[] { "filter?: name substring" },
            p => CountWidgets(p.Value<string>("filter"))); // any JSON-friendly value
    }
}
```

`Add(owner, ...)` removes the command when the owner unloads. The handler gets the parameters as a `JObject` and returns a JSON-friendly value, or a `Deferred` for work that finishes later. The handler runs on the main thread.

Commands can also be added with a `[BridgeCommand]` attribute on a static method inside the DevBridge assembly. That scan only covers the DevBridge assembly, so other mods use `Add`.

## Testing recipes

These use the `neet call` form. The MCP tools accept the same method names and parameters.

Start the game and load the house:

```sh
neet launch --wait 90
neet call scene.load name=NEET_House
neet call game.info      # repeat until "houseReady" is true
```

Teleport the player and read the state:

```sh
neet call player.teleport room=Kitchen
neet call player.get
```

Open a site on the computer and list the sites the game knows:

```sh
neet call computer.view site=anime
```

Click a button. Find it first, then click it by path or id:

```sh
neet call obj.find name=Anime component=UnityEngine.UI.Button
neet call ui.click target='Root/Canvas/Button'      # use a path or #id from obj.find
neet call ui.text target='#4312'                    # read the label
```

Take a screenshot. `screenshot` returns base64 data inside JSON, so decode the `data` field (`jq` is used here):

```sh
neet call screenshot '{"format":"png","scale":1}' > reply.json
jq -r .data reply.json | base64 -d > shot.png
```

Read new log lines since the last check:

```sh
neet call log.tail level=Warning max=50
neet call log.tail after=812
```

Pause the game and resume it:

```sh
neet call game.timescale value=0
neet call game.timescale value=1
```

Inspect a component's fields and set one:

```sh
neet call obj.inspect target='#4312' component=UnityEngine.Rigidbody
neet call obj.set target='#4312' component=UnityEngine.Rigidbody member=mass value=2
```

Spawn items in a room, using a name from `items.list`:

```sh
neet call items.list filter=mug
neet call items.spawn name=Mug room=Kitchen count=2
```
