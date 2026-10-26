# neet command-line tool

`neet` installs the mod loader, starts and stops NEET Demo, sends commands to the DevBridge mod, inspects and converts save files, builds the changelog, and runs an MCP server for editors. Source: `tools/NeetTool/`. The tool targets .NET 10.

## Build

```sh
dotnet build NeetMods.sln -c Release
```

The build copies the loader files into `payload/` next to the binary. `neet install` reads them from there, so build the solution before installing.

The binary is `tools/NeetTool/bin/Release/net10.0/neet`. The examples below use `neet`. Put the binary on `PATH` or call it by its full path.

## Options

| Option | Meaning |
| ------ | ------- |
| `--game DIR` | Game folder, the one containing `NEET.exe`. |
| `--port N` | Bridge port used by `call`, `status`, `launch --wait`, `stop` and `mcp`. Default `27850`. |
| `NEET_GAME_DIR` | Environment variable with the game folder. |

The game folder is looked up in this order: `--game`, then `NEET_GAME_DIR`, then `NEET Demo` in every Steam library folder listed in `libraryfolders.vdf`. If none of them contains `NEET.exe`, the command fails with `Couldn't find NEET Demo. Pass --game <folder> or set NEET_GAME_DIR.`

Options take a value as `--name value` or `--name=value`. A bare `--flag` is a switch. An option that takes a value will consume the word after it, so put options before positional arguments.

Expected errors (bad arguments, the game not found, the bridge not answering) print to stderr and exit with code 1. An unknown command exits with code 2. `neet help` lists every command.

## install

```sh
neet install [--game DIR] [--mods DIR] [--64bit | --32bit] [--incremental-gc | --no-incremental-gc]
```

Copies these files from `payload/` into `NEET_Data/Managed/`: `NeetLoader.dll`, `NeetLoader.pdb` (optional), `0Harmony.dll`, `Mono.Cecil.dll`.

Then it edits two JSON files Unity reads at startup, both in `NEET_Data/`:

- `ScriptingAssemblies.json`: adds `NeetLoader.dll` to `names` and `16` (user assembly) to `types`.
- `RuntimeInitializeOnLoads.json`: adds an entry that calls `NeetLoader.Entry.Init` before the first scene loads.

Before the first edit, each file is copied to `<name>.neetbak`. Those backups are kept until uninstall. Running `install` again is safe: entries are not added twice and the backups are not overwritten. It also creates the `Mods/` folder. If the game is running, it prints a warning, and the changes apply on the next start.

Options:

- `--mods DIR` copies every folder in `DIR` that has a `mod.json` into `Mods/`, file by file over the installed copy. Files that only exist in the game, like a mod's `config.json`, stay. The release package's install scripts use this.
- `--64bit` swaps the game's 32-bit Unity player for the 64-bit player of the same Unity version (2022.3.17f1, Mono). The game's data is the same for both, so only `NEET.exe`, `UnityPlayer.dll`, the crash handler (`UnityCrashHandler32.exe` becomes `UnityCrashHandler64.exe`) and the `MonoBleedingEdge/` folder change. The 32-bit files are copied to `NeetLoader/backup/player-x86/` first.
- `--32bit` puts the backed-up 32-bit player back. Steam's "Verify integrity of game files" does the same.
- `--incremental-gc` adds `gc-max-time-slice=3` to `NEET_Data/boot.config` (original kept as `boot.config.neetbak`). Unity writes that line for games built with incremental garbage collection; with it, Mono collects garbage in slices of at most 3 ms instead of stopping the game for each collection. `--no-incremental-gc` restores the original file.

`--64bit`, `--32bit` and the GC options refuse to run while the game is open.

### Where the 64-bit player comes from

Unity's licence lets a game ship the player, not a mod, so `--64bit` downloads it from Unity when asked. The only standalone download with the Windows players is the "Windows Build Support (Mono)" module for the macOS editor:

`https://download.unity3d.com/download_unity/4fc78088f837/MacEditorTargetInstaller/UnitySetup-Windows-Mono-Support-for-Editor-2022.3.17f1.pkg` (180,824,073 bytes)

The download is checked against that size and a pinned SHA-256. The `.pkg` is a xar archive (read by `Archives/XarArchive.cs`); its `TargetSupport.pkg.tmp/Payload` is a gzipped cpio archive in the portable ASCII format (`Archives/CpioReader.cs`). Only `Variations/win64_player_nondevelopment_mono/` is unpacked, and of that only `WindowsPlayer.exe` (installed as `NEET.exe`), `UnityPlayer.dll`, `UnityCrashHandler64.exe` and `MonoBleedingEdge/`. Those stay in the tool's cache (`~/.cache/neet/` or `$XDG_CACHE_HOME/neet/` on Linux, `%LOCALAPPDATA%\neet\cache\` on Windows), and the package is deleted. Measurements of the 64-bit player are in [docs/research/performance.md](../research/performance.md#64-bit).

## uninstall

```sh
neet uninstall [--game DIR] [--remove-mods]
```

Puts the 32-bit player back if `install --64bit` swapped it (the backup is in the loader's folder, so this comes first) and restores `boot.config`. Then it restores both JSON files from their `.neetbak` backups and deletes the backups. If a backup is missing, it removes the loader's entries from the file instead. Then it deletes the four payload files from `NEET_Data/Managed/`. It refuses to run while the game is open.

Mods stay in place unless `--remove-mods` is given. That option also deletes `Mods/` and `NeetLoader/` (loader data and logs), including each mod's config.

## status

```sh
neet status
```

Example output:

```text
Game:        /home/you/.local/share/Steam/steamapps/common/NEET Demo
Loader DLLs: present
Registered:  yes
Player:      64-bit (32-bit kept for switching back)
Incremental GC: on
Proton tool: GE-Proton10-34
Save folder: /home/you/.local/share/Steam/steamapps/compatdata/2054460/pfx/drive_c/users/steamuser/AppData/LocalLow/NEET Games/NEET
Mods:        DevBridge, OpenSaves
Running:     no
Bridge:      down
```

`Registered` is `yes` when both JSON files contain the loader's entries. `Player` reads the machine type from `NEET.exe`'s header. `Bridge` is `up` when the bridge answers `ping`.

## launch

```sh
neet launch [--fullscreen] [--direct] [--wait SECONDS]
```

By default the game starts windowed at 1600x900 with `-screen-fullscreen 0 -screen-width 1600 -screen-height 900`. `--fullscreen` leaves those arguments out.

- Without `--direct`, the tool runs `steam -applaunch 2054460` with the arguments. Steam applies the Proton build and launch options set for the game.
- With `--direct`, the tool runs the game through a Proton build without the Steam client. It reads the build chosen for the game in Steam's `config/config.vdf`, finds its `proton` script in `compatibilitytools.d/`, in `steamapps/common/<name>`, or as `Proton X.0` for official `proton_X` ids, and runs `proton run NEET.exe`. The game's Proton prefix must already exist, so start the game once from Steam first. Steam's launch options are not used in this mode.

If the game is already running, the command prints a warning and exits with 0.

`--wait SECONDS` polls the bridge once per second. It prints `Bridge is up.` and exits 0 when the bridge answers. If the time runs out, it exits 1 and names the loader log to check.

## stop

```sh
neet stop [--force]
```

Sends `game.quit` through the bridge with a 3 second timeout, waits up to 10 seconds for the process to exit, and kills whatever is still running. `--force` skips the bridge request and kills right away.

Under Wine the tool finds the game by scanning `/proc` for a command line that starts with `Z:\` and contains `\NEET.exe`. On Windows it looks for a process named `NEET`.

## logs

```sh
neet logs [--lines N]
neet logs --player [--lines N]
neet logs --errors
```

Prints the last 60 lines (default) of the loader log, `NeetLoader/logs/latest.log` in the game folder. With `--player` it prints Unity's `Player.log` from the save folder. With `--errors` it prints the whole error report, `NeetLoader/logs/errors.log` (see the loader guide), or says there is none. The file is opened with shared access, so it can be read while the game runs.

## call

```sh
neet call <method> [json-params | key=value ...] [--timeout SECONDS]
```

Runs one DevBridge command and prints the result as indented JSON. `--timeout` sets how long the client waits, in seconds (default 30). Parameters can be given in one of two ways:

```sh
neet call player.get
neet call player.set '{"stats":{"Hunger":100,"Energy":50}}'
neet call obj.find name=Button component=UnityEngine.UI.Button
```

A single argument that starts with `{` is parsed as the whole parameter object. Otherwise each `key=value` pair is one parameter. A value is parsed as JSON when it is valid JSON (`12`, `true`, `[1,2,3]`, `"text"`), and taken as a plain string otherwise. A word that looks like a number but should be a string needs JSON quoting: `'name="123"'`.

The full command list is in [dev-bridge.md](dev-bridge.md).

## save

```sh
neet save list
neet save info FILE
neet save decode FILE [OUT]
neet save encode FILE [OUT]
```

- `list` (the default action) prints the save folder, then one line per `neet*` file, newest first. Each line shows the modification time, size, format (`nsav v1 Deflate` or `plain JSON`) and name.
- `info` prints the format, sizes, the CRC-32 for `.nsav` files, and the number of entries in the `Save` array grouped by `type`.
- `decode` writes the save's JSON, indented. `OUT` defaults to the same name with a `.json` extension. The output must be a different file from the input.
- `encode` parses a JSON file, so a syntax error is caught before the game sees it, and writes it as `.nsav`. The output is compact. `OUT` defaults to the input name with a `.nsav` extension.

The game loads the newest file in the save folder by modification time, so `decode` or `encode` written into the save folder makes that file the one the game loads next. See [save-format.md](save-format.md).

## changelog

```sh
neet changelog            # print the JSON
neet changelog --write    # write changelog.json at the repo root
neet changelog --write --out FILE
```

Run it from inside the repository. `--out` only applies together with `--write`.

### Trailers

Each commit can carry player-facing lines as git trailers in its final paragraph:

```text
feat(loader): Keep mod settings between launches

Changelog-Added: Mod settings are remembered between launches.
Changelog-Fixed: Settings no longer reset when a mod is reloaded.
```

- Trailer names are `Changelog-Added`, `Changelog-Changed`, `Changelog-Fixed` and `Changelog-Removed`. The match ignores case.
- Only trailers count. A `Changelog-*` line in the body of a message, not in the trailer block, is ignored.
- The conventional prefix sets the area: `feat(loader):` gives the area `Mod loader`. Scope names are mapped to display names in `ChangelogBuilder.cs` (`loader` to `Mod loader`, `bridge` to `Dev bridge`, `mcp` and `tool` to `Command-line tool`, and so on). Unmapped scopes are capitalized. Commits without a scope get no `area`.
- Within one release and section, a line that appears twice (case-insensitive) is kept once, at its first occurrence.

### Release resolution

Every `vX.Y.Z` tag closes a release that covers the commits after the previous tag. Commits after the last tag belong to the upcoming version, which is the `<Version>` in `Directory.Build.props`. If that version is already tagged, the entry is labelled `Unreleased`. Releases with no changelog lines are left out. The output lists the newest release first.

The date of a tagged release is the tag's creation date. The date of the upcoming release is the date of its newest commit.

### changelog.json

```json
{
  "releases": [
    {
      "version": "0.1.0",
      "date": "2026-10-26",
      "released": false,
      "added": [
        {
          "text": "Install a mod by dropping its folder into the game's Mods folder.",
          "area": "Mod loader"
        }
      ],
      "changed": [],
      "fixed": [
        {
          "text": "Settings no longer reset when a mod is reloaded.",
          "area": "Mod loader"
        }
      ],
      "removed": []
    }
  ]
}
```

- `version`: the tag without `v`, or the upcoming version, or `Unreleased`.
- `date`: `YYYY-MM-DD`.
- `released`: `true` for a tagged release, `false` for the upcoming one.
- `added`, `changed`, `fixed`, `removed`: always present, possibly empty. Each item has `text` and, when the commit had a scope, `area`.

Do not edit `changelog.json` by hand. The in-game What's New screen reads it, and the next `--write` replaces it.

## mcp

```sh
neet mcp [--game DIR] [--port N]
```

Runs a Model Context Protocol server on stdin and stdout. The protocol is newline-delimited JSON-RPC 2.0, protocol version `2025-06-18`. The server answers `initialize`, `ping`, `tools/list` and `tools/call`. Notifications get no reply. An unknown method gets a `-32601` error. A tool failure is returned as a result with `isError: true`, not as a protocol error. Requests are handled concurrently, so a slow tool such as `game_launch` does not block `ping`. Everything except protocol messages goes to stderr.

Any client that speaks MCP over stdio can use it. Add a server entry whose command is the `neet` binary and whose arguments are `["mcp"]`. Key names differ between clients, but the entry looks like this:

```json
{
  "mcpServers": {
    "neet": {
      "command": "/path/to/neet-mods/tools/NeetTool/bin/Release/net10.0/neet",
      "args": ["mcp"]
    }
  }
}
```

If the game is not in a default location, add `"env": { "NEET_GAME_DIR": "/path/to/NEET Demo" }` to the entry, or add `"--game", "/path/to/NEET Demo"` to `args`.

### Tools

Five tools act on this machine. Four of them work whether or not the game is running. `screenshot` needs the bridge.

| Tool | What it does |
| ---- | ------------ |
| `game_status` | Returns the game folder, whether the game process is running, whether the bridge answers, and whether the loader is installed. |
| `game_launch` | Starts the game windowed, like `neet launch`, and waits for the bridge. Inputs: `direct` (boolean), `waitSeconds` (default 90). |
| `game_stop` | Asks the game to quit through the bridge, then kills it if it is still running. |
| `game_logs` | Returns the end of the loader log, or `Player.log` with `player: true`. Inputs: `lines` (default 80). |
| `screenshot` | Captures the game window. Input: `scale` (0.1 to 1, default 0.5). Returns the image as a JPEG image content item, followed by a text item with the width, height and scene name. |

The other 23 tools forward to a DevBridge command. They need the game running with the bridge answering. Each one sends its arguments as the command's parameters. The bridge method is shown in the table, and the parameters are described in [dev-bridge.md](dev-bridge.md).

| Tool | Bridge method | Inputs |
| ---- | ------------- | ------ |
| `bridge_call` | any method | `method` (required), `params` (object) |
| `bridge_help` | `help` | none |
| `game_info` | `game.info` | none |
| `game_save` | `game.save` | none |
| `game_timescale` | `game.timescale` | `value` (required) |
| `scene_load` | `scene.load` | `name` (required) |
| `console_exec` | `console.exec` | `command` (required) |
| `log_tail` | `log.tail` | `after`, `max`, `level` |
| `player_get` | `player.get` | none |
| `player_set` | `player.set` | `stats` (object, required) |
| `player_teleport` | `player.teleport` | `room`, or `x`, `y`, `z` |
| `player_god` | `player.god` | `enabled` (required) |
| `money_set` | `money.set` | `value` (required), `taxed` |
| `items_list` | `items.list` | `filter` |
| `item_spawn` | `items.spawn` | `name` (required), `room`, `count` |
| `rooms_list` | `rooms.list` | none |
| `scene_find` | `obj.find` | `name`, `component`, `includeInactive`, `max` |
| `scene_tree` | `obj.tree` | `target`, `depth` |
| `object_inspect` | `obj.inspect` | `target` (required), `component` |
| `object_set` | `obj.set` | `target`, `component`, `member`, `value` (all required) |
| `object_call` | `obj.call` | `target`, `component`, `method` (required), `args` |
| `static_call` | `type.call` | `type`, `method` (required), `args` |
| `cards_get` | `cards.get` | none |

`bridge_call` reaches any command, including ones not in the table. Use `bridge_help` to list them.

If the bridge does not answer, a bridge tool returns an error result with the text `Can't reach the game. Is it running with the DevBridge mod installed?`.

## Release packages

`build/package.sh` builds a package for players: `dist/NEET-Mods-<version>.zip`, with `<version>` from `Directory.Build.props`.

| In the zip | What it is |
| --- | --- |
| `Mods/` | Every mod folder from a Release build, except DevBridge (it opens a port and is only for development). |
| `tool/linux-x64/neet`, `tool/win-x64/neet.exe` | The tool, self-contained (no .NET needed), with `payload/` next to it. |
| `install.sh`, `install.bat` | Ask whether to switch to the 64-bit player (default no) and whether to turn on incremental GC (default yes), then run `neet install --mods Mods ...`. Extra arguments, like `--game DIR`, are passed on. |
| `uninstall.sh`, `uninstall.bat` | Run `neet uninstall`, passing arguments on. |
| `README.txt` | Install and uninstall steps for players. |

The script builds with `NeetDeploy=false`, so packaging doesn't touch the game folder. The script sources are in `build/package/`; `.gitattributes` keeps the `.bat` files in CRLF.

## Common problems

- `Couldn't find NEET Demo`: pass `--game` or set `NEET_GAME_DIR`.
- `Missing <file> next to the tool. Build the solution first.` from `install`: run `dotnet build NeetMods.sln -c Release`, which fills `payload/`.
- `Can't reach the game`: the game is not running, the mod is not loaded, or `--port` does not match `Mods/DevBridge/config.json`.
- `launch --wait` exits with `Bridge didn't come up`: read `neet logs`.
