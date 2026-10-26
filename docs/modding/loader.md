# NeetLoader reference

This is the reference for the loader in `src/NeetLoader`. For a walkthrough that builds a first mod, read [getting-started.md](getting-started.md) first.

Contents:

- [Startup](#startup)
- [Folder layout](#folder-layout)
- [mod.json fields](#modjson-fields)
- [Discovery, ordering and failures](#discovery-ordering-and-failures)
- [NeetMod lifecycle](#neetmod-lifecycle)
- [Tracking and cleanup](#tracking-and-cleanup)
- [Configuration](#configuration)
- [Logging](#logging)
- [Runtime control](#runtime-control)
- [Hot reload](#hot-reload)
- [Harmony patching](#harmony-patching)
- [Threading](#threading)
- [Settings file](#settings-file)

## Startup

The loader starts from `NeetLoader.Entry.Init`, which the installer registers in `RuntimeInitializeOnLoads.json` with `BeforeSceneLoad`. `Init` runs once. Any exception in startup is caught and reported with `Debug.LogError`.

`Loader.Start` then does, in order:

1. `LoaderPaths.Init()` computes the folder layout and creates `Mods/` and `NeetLoader/logs/`.
2. `LogHub.Init` opens `latest.log`, moving last session's file to `previous.log`.
3. `ModRegistry.Init` reads `NeetLoader/settings.json`. If `VerboseLog` is set, the file level drops to `Debug`.
4. `ModLifecycle.Init` creates the host: a hidden, persistent `GameObject` named `NeetLoader` that forwards Unity callbacks to mods.
5. `ModDiscovery.Scan` finds mod folders. Each one is added to the registry. Mods listed as disabled in `settings.json` get `ModState.Disabled`.
6. `ModLifecycle.LoadBatch` loads everything still `Pending`, in dependency order.

Public constant `Loader.Version` is `"0.1.0"`.

## Folder layout

| Path | Contents |
| --- | --- |
| `<game>/NEET_Data/Managed/NeetLoader.dll`, `0Harmony.dll`, `Mono.Cecil.dll` | The loader and its dependencies. Unity loads them from here. |
| `<game>/Mods/<folder>/` | One mod per folder. Any folder with a `mod.json` counts. |
| `<game>/Mods/<folder>/config.json` | Default location for `LoadConfig<T>()`. |
| `<game>/NeetLoader/settings.json` | Loader settings, see [Settings file](#settings-file). |
| `<game>/NeetLoader/logs/latest.log` | This session's log. |
| `<game>/NeetLoader/logs/previous.log` | The previous session's log. |

`LoaderPaths` exposes these as public static properties: `GameDir` (the folder with `NEET.exe`), `ModsDir`, `DataDir` (`<game>/NeetLoader`) and `LogsDir`.

## mod.json fields

`mod.json` is deserialized into `ModManifest`. Unknown fields are ignored. Missing fields get the defaults below.

| Field | Type | Default | Meaning |
| --- | --- | --- | --- |
| `id` | string | folder name | Unique id. Used by dependencies, the Harmony instance, log source and unload cleanup. A second folder with the same id is skipped. |
| `name` | string | `id` | Display name on the Mods page (`DisplayName`). |
| `version` | string | `"0.0.0"` | Compared against `dependencies`. Loose SemVer: `1.2`, `1.2.3` and `1.2.3-beta` all parse. Pre-release tags are ignored. |
| `author` | string | `null` | Shown on the Mods page. |
| `description` | string | `null` | Shown on the Mods page. |
| `assemblies` | string[] | `[]` | DLLs to load, relative to the mod folder. Empty means every `*.dll` in the folder root. Subfolders are not searched. |
| `dependencies` | object | `{}` | Required mods, as `"id": "minimum version"`. `"*"` accepts any version. A leading `>=` is allowed and ignored, so `">=1.2"` means `1.2` or newer. |
| `loadAfter` | string[] | `[]` | Mods to load before this one if they are present. Not required. |
| `autoPatch` | bool | `true` | Apply Harmony patches for every class in the mod's assemblies. See [Harmony patching](#harmony-patching). |
| `hotReload` | bool | `false` | Load the DLLs from memory and reload the mod when they change. The mod must clean up in `OnUnload`. See [Hot reload](#hot-reload). |
| `essential` | bool | `false` | Always on: the mod can't be switched off or unloaded, and neither can the mods it depends on. Reloading still works. For the framework (NeetModKit) and mods that protect something (No Feedback). |

Example:

```json
{
  "id": "CardExpansion",
  "name": "Card Expansion",
  "version": "0.3.0",
  "author": "pawbeans",
  "description": "Adds summons, an auto-clicker and better exchange rates.",
  "assemblies": [ "CardExpansion.dll" ],
  "dependencies": { "NeetModKit": "0.1.0" },
  "loadAfter": [ "OpenSaves" ],
  "autoPatch": true,
  "hotReload": true
}
```

## Discovery, ordering and failures

### Discovery

`ModDiscovery.Scan` looks at each direct subfolder of `Mods/`:

- A folder without `mod.json` is ignored.
- A `mod.json` that doesn't parse is skipped and logged as an error. The mod doesn't appear on the Mods page.
- A missing `id` takes the folder name.
- A duplicate `id` is skipped and logged. Folders are scanned in directory order, and the first one scanned wins.

### Ordering

`DependencySorter` orders the batch so that each mod comes after everything in its `dependencies` and `loadAfter` that is in the same batch. Traversal starts in alphabetical order by `id`, so the order is the same on every launch.

A dependency outside the batch is accepted only if it is already `Loaded`. This matters for runtime loads and reloads.

### Failures

A failed mod gets `ModState.Failed` and `ModInfo.Error` with a message. Dependents of a failed mod fail too.

| Situation | Result |
| --- | --- |
| `dependencies` names a mod that is missing, disabled or not loaded | Failed: `Needs 'X', which isn't installed or is switched off.` |
| A dependency's version is below the minimum | Failed: `Needs 'X' 1.2 or newer (found 1.0.0).` |
| A dependency failed | Failed: `Needs 'X', which failed to load.` |
| A cycle through `dependencies` | Failed: `Circular dependency.` The mod that closes the cycle fails, and so do the mods that depend on it. A `loadAfter` hint that would close a loop is skipped instead. |
| A DLL in `assemblies` can't be loaded | Failed with the exception message. |
| `OnLoad` throws | Failed. Tracked items are disposed. `OnUnload` is not called, because `OnLoad` never completed. |
| A Harmony class fails to patch | Failed: `Patch class <FullName> failed: <message>`. The mod's patches are rolled back. |
| No `NeetMod` subclass in the assemblies | Not a failure. The mod runs as a patch-only or library mod. |
| More than one non-abstract `NeetMod` subclass | Warning. The first type returned by reflection is used, so keep exactly one. |

Failures during the startup batch are logged at error level as `<mod>: <error>`.

## NeetMod lifecycle

`NeetMod` is the base class for a mod's entry point. The loader creates one instance per load, with a parameterless constructor. A reload disposes the old instance and creates a new one. Override only the callbacks you need. The host checks which ones you override, so the rest cost nothing per frame.

| Member | When it runs | Notes |
| --- | --- | --- |
| `OnLoad()` | Right after the mod's assemblies load and its Harmony classes are patched. At startup no scene exists yet. | An exception fails the mod. |
| `OnAllLoaded()` | After every mod in the same batch has finished `OnLoad`. | An exception is logged. The mod stays loaded. |
| `OnSceneLoaded(Scene, LoadSceneMode)` | Every scene load while the mod is running. For a mod loaded at runtime, it is also called once for the current active scene, with `LoadSceneMode.Single`. | Mods loaded at startup see the first scene only if it loads after `OnLoad`. |
| `OnUpdate()` | Every frame, from the host's `Update`. | Runs after queued main-thread work. |
| `OnLateUpdate()` | Every frame, from `LateUpdate`. | |
| `OnFixedUpdate()` | Every physics step, from `FixedUpdate`. | |
| `OnGUI()` | Every IMGUI pass. | |
| `OnQuit()` | Application quit. | |
| `OnUnload()` | Before the mod is unloaded or reloaded. Only if `OnLoad` completed. | Undo anything you didn't `Track`. |

Callback exceptions are caught. An exception in an update callback is logged once per mod per callback. Later ones are hidden, so one bad mod can't flood the log every frame.

### Load and unload order

Loading a batch, in order:

1. Each mod is activated in dependency order: its assemblies load, its entry object is created, its Harmony classes are patched, and `OnLoad` runs.
2. `OnAllLoaded` runs for every mod that loaded.
3. For runtime loads only, `OnSceneLoaded` is replayed for the active scene.
4. `ModRegistry.ModLoaded` is raised for each mod.

Unloading one mod:

1. Loaded dependents unload first, each by the same steps.
2. `ModRegistry.ModUnloading` is raised for the mod.
3. `OnUnload` runs.
4. Tracked items are disposed in reverse order of tracking.
5. Harmony patches with this mod's id are removed.
6. The mod's assemblies are marked as stale. They stay in memory.
7. The mod's state becomes `Disabled`.

Reloading (see [Hot reload](#hot-reload)) runs the same steps for the whole set, then loads them all again.

### Mod state

`ModInfo.State` is a `ModState`:

| Value | Meaning |
| --- | --- |
| `Pending` | Found, not loaded yet. |
| `Disabled` | Switched off in settings, or unloaded at runtime. |
| `Loaded` | Running. |
| `Failed` | Could not load. `ModInfo.Error` says why. |

## Tracking and cleanup

`NeetMod` implements `ITracker`. Anything you pass to `Track` is disposed when the mod unloads, after `OnUnload`, in reverse order.

```csharp
public override void OnLoad()
{
    ModRegistry.ModLoaded += OnAnyModLoaded;
    Track(Disposable.Create(() => ModRegistry.ModLoaded -= OnAnyModLoaded));
}

private void OnAnyModLoaded(ModInfo mod) => Log.Info($"{mod.Id} loaded");
```

Helpers:

| Member | Does |
| --- | --- |
| `Track<T>(T disposable)` | Registers an `IDisposable`. Returns it. |
| `Disposable.Create(Action)` | Wraps an action as an `IDisposable` that runs once. |
| `Disposable.Destroying(UnityEngine.Object)` | Destroys a Unity object on dispose. |
| `CreateObject(string name)` | Creates a `DontDestroyOnLoad` `GameObject` named `<Id>:<name>`. It is destroyed on unload. Use it as a home for your `MonoBehaviour`s. |
| `StartCoroutine(IEnumerator)` | Runs the coroutine on the loader's host object. It is stopped on unload. |

Framework features follow the same pattern. `ModFeature` implements `ITracker` and cleans up when it is switched off.

### What is not cleaned up

The loader disposes only what the mod registered with `Track`, `CreateObject` and `StartCoroutine`, and the Harmony patches made through the mod's `Harmony` instance. Everything else stays alive after unload or reload, including:

- `GameObject`s and components created with `new GameObject` or `AddComponent` directly.
- Event handlers subscribed to game or other-mod objects, unless you track the unsubscribe.
- Harmony patches made through your own `Harmony` instance with a different id.
- Objects that game code or other mods still reference, such as delegates from old code.

## Configuration

`LoadConfig<T>(string fileName = "config.json")` returns a `ConfigFile<T>` backed by `<mod folder>/<fileName>`. `T` is a class with a parameterless constructor, and its public fields or properties hold the settings.

```csharp
public sealed class MyConfig
{
    public int Amount = 5;
    public bool Enabled = true;
}

public override void OnLoad()
{
    var config = LoadConfig<MyConfig>();
    config.Save();                              // writes the defaults if the file is missing
    config.Changed += _ => Log.Info("Config changed");
    Log.Info($"Amount: {config.Value.Amount}");
}
```

| Member | Does |
| --- | --- |
| `Value` | The current settings object. |
| `Path` | Full path of the file. |
| `Reload()` | Reads the file again. Raises `Changed`. |
| `Save()` | Writes `Value` as indented JSON, through a temp file. Raises `Changed`. |
| `Changed` | `event Action<T>`, raised after `Save` and `Reload`. |

A missing file or missing fields use the defaults in `T`. An unreadable file logs a warning and uses the defaults. A write failure logs an error. Null values are not written.

The settings shown in the in-game menu come from NeetModKit (`ModSettings.Register`). The loader only stores the values.

`JsonFile` (public, in `NeetLoader.Config`) has `Read<T>`, `TryRead<T>` and `Write<T>` if you need plain JSON files.

## Logging

### ModLogger

Every mod gets a `ModLogger` as `Log`, tagged with its id. You can create one for your own use with `new ModLogger("Name")`.

| Method | Level |
| --- | --- |
| `Debug(string)` | `Debug` |
| `Info(string)` | `Info` |
| `Warn(string)` | `Warning` |
| `Error(string)` | `Error` |
| `Error(string, Exception)` | `Error`, with the exception appended |

### Where lines go

`LogHub` is the single sink. Each line goes to three places:

- **The ring buffer.** `LogHub.Buffer`, the last 4096 entries. Every level goes here.
- **`latest.log`.** Only entries at or above `MinFileLevel`, which is `Info` by default. Set `VerboseLog: true` to include `Debug`.
- **The Unity console.** Mapped to `Debug.Log`, `LogWarning` or `LogError`.

Entries look like this:

```
[14:02:31.415] [Info] [CardExpansion] Loaded CardExpansion 0.3.0
```

Errors are written to the file at once. Other lines are flushed about once a second by the host.

### Unity messages

The loader also captures Unity's own log messages. Errors, exceptions and asserts become `Error` entries with the stack trace. Warnings become `Warning`. Ordinary log messages are `Debug`, which means they go into the ring buffer only. Warnings and errors from Unity are written to `latest.log` with the source `Unity`.

Repeated Unity messages are collapsed. The first copy is kept. When a different message arrives, the buffer gets a line such as `(previous message repeated 40 more times)`. This applies to Unity messages only, and the summary line goes to the buffer, not the file. Mod messages are never collapsed.

### Reading the ring buffer

```csharp
private long _lastSeen;

private void DumpWarnings()
{
    foreach (LogEntry entry in LogHub.Buffer.Read(_lastSeen, 200, LogLevel.Warning))
        Log.Info(entry.ToString());
    _lastSeen = LogHub.Buffer.LastSequence;
}
```

`LogBuffer.Read(long afterSequence, int max, LogLevel minLevel = LogLevel.Debug)` returns entries newer than `afterSequence`, oldest first, keeping the newest `max` if there are more. `LastSequence` is the last sequence number written.

### Files

`latest.log` is recreated at each start. The previous session's file is renamed to `previous.log`, replacing the one before it. `neet logs` prints the end of `latest.log`, and `neet logs --player` prints Unity's `Player.log`.

## Runtime control

`ModRegistry` is the static API for everything the Mods page does.

| Member | Does |
| --- | --- |
| `Mods` | Every known `ModInfo`, including disabled and failed mods. |
| `Find(string id)` | The `ModInfo` for an id, or `null`. Lookups are case-insensitive. |
| `IsLoaded(string id)` | True if the mod is `Loaded`. |
| `Get<T>()` | The entry object of the first loaded mod whose entry is a `T`. |
| `OwnerOf(Type)` | The loaded mod whose assemblies contain `type`. |
| `Load(id, done)` | Loads the mod now. Fails if it is already loaded, or was loaded earlier this session and isn't `hotReload`. |
| `Unload(id, done)` | Unloads the mod now. Loaded dependents are unloaded first. Refused for mods that are always on. |
| `Reload(id, done)` | Reloads the mod and its loaded dependents as one batch. |
| `ReloadAll(done)` | Reloads every loaded `hotReload` mod as one batch. |
| `Rescan()` | Looks for new folders. Returns the new `ModInfo`s, which start `Disabled`. |
| `IsEnabled(id)` | The "load when the game starts" switch. Always true for mods that are always on. |
| `SetEnabled(id, bool)` | Sets the switch and saves it. Takes effect on the next launch. Returns `null`, or why the mod can't be switched off. |
| `AlwaysOnBecause(ModInfo)` | The essential mod that keeps this one on (itself, or an essential mod that needs it), or `null`. |
| `AlwaysOnReason(ModInfo)` | The same as text ("it's always on", "No Feedback needs it and is always on"), or `null`. |
| `RestartNeeded` | True if any switch differs from what is running. |
| `AutoReload` | Gets or sets automatic reloading. Saved to settings. |
| `ModLoaded` | `event Action<ModInfo>`. Raised after a mod loads or reloads. |
| `ModUnloading` | `event Action<ModInfo>`. Raised before a mod unloads or reloads. |

Runtime operations are queued and run on the main thread during the next `Update`. `done` is called on the main thread with `null` on success or the reason for failure. Reload and unload failures are also logged as warnings.

```csharp
ModRegistry.Reload("NeetModKit", error =>
{
    if (error != null)
        Log.Warn(error);
});
```

`ModInfo` has these public members: `Id`, `Manifest`, `Directory`, `State`, `Error`, `Instance` (the entry object, or `null`), `Assemblies`, `LoadCount` (1 after the first load), and `CanHotReload`.

## Hot reload

Set `"hotReload": true` in `mod.json` to opt in.

### What happens

- The mod's DLLs are read into memory, so the files on disk are not locked and can be rebuilt.
- `HotReloadWatcher` checks the newest DLL write time of each loaded hot-reload mod once a second. Polling is used rather than `FileSystemWatcher`, which is unreliable under Wine.
- A change counts once the files have been unchanged for one poll. That keeps a half-copied build from loading.
- Every change found in the same poll is reloaded as one batch.
- The watcher runs only when `AutoReload` is on. It defaults to on.

### Reload sequence

`ModRegistry.Reload`, `ReloadAll`, the watcher, and the Mods page's `RELOAD` all go through `ModLifecycle.ReloadSet`:

1. The set is the mod plus every loaded mod that depends on it, directly or through other mods.
2. If any mod in the set has `hotReload: false`, nothing changes and the call fails with `Can't reload without a restart: <id> doesn't support hot reload.`
3. The loaded mods in the set unload, dependents first.
4. Each mod's `mod.json` is read again. Only the manifest changes; the `id` cannot.
5. The whole set loads again in dependency order, with the same steps as a startup batch.

Reloading a mod that is not currently loaded loads it, subject to the same hot-reload check.

### Rules and limits

- `mod.json` alone does not trigger a reload. The watcher looks only at DLL timestamps. A new manifest is read on the next reload.
- The loader does not reload itself. Changes to `NeetLoader.dll`, `0Harmony.dll` or `Mono.Cecil.dll` need a restart.
- A mod without `hotReload` can be loaded once per session. After it unloads, `Load` refuses it until the game restarts.
- Static fields in the reloaded mod start over, because the new copy of the assembly has its own statics. Objects those fields pointed to, and anything else listed under [What is not cleaned up](#what-is-not-cleaned-up), are not affected by the reload.
- Each reload leaves the previous assembly in memory until the game exits, because Mono cannot unload assemblies. Heavy reloading grows memory use.

### Carrying state across a reload

`ReloadState` lives in the loader, which never reloads, so values put there survive.

```csharp
public override void OnUnload()
{
    ReloadState.Put("MyMod.Count", count);
}

public override void OnLoad()
{
    if (ReloadState.Take("MyMod.Count", out int count))
        _count = count;
}
```

`Put(string key, object value)` stores a value. `Take<T>(string key, out T value)` returns it once and removes it. Store plain data: strings, numbers, and types from assemblies that don't reload. A value whose type comes from the reloaded mod's old assembly won't match `T` in the new one, so `Take` returns `false`.

### Assembly renaming

Each hot reload gives the mod's assembly a new name: the first load keeps the real name, and later loads use `<Name>-r2`, `-r3`, and so on. `AssemblyRewriter` uses Mono.Cecil to change the assembly name, give it a new module id, and point its references at the newest renamed copies. Without new names Mono would keep binding to the first copy it loaded.

Consequences:

- `assembly.GetName().Name` changes after a reload. Don't look up assemblies by name.
- Type full names don't change. `GetType("MyMod.Thing")` works on the newest copy.
- Reflection code should skip old copies. `LoadedAssemblies.IsStale(Assembly)` returns true for them.

```csharp
foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
{
    if (LoadedAssemblies.IsStale(assembly))
        continue;
    // search this assembly
}
```

Assemblies from non-hot-reload mods load from disk with `Assembly.LoadFrom` and are never renamed.

## Harmony patching

`NeetMod.Harmony` is `new Harmony(Info.Id)`, created on first access. The loader unpatches only by that id on unload, so patches made through any other `Harmony` instance survive unload. Use the property, or give your instance the mod's id.

### Auto patching

With `autoPatch: true` (the default), the loader calls `CreateClassProcessor(type).Patch()` for every type in the mod's assemblies. Only classes with a `[HarmonyPatch]` attribute do anything. Classes with `[HarmonyPatchCategory]` are skipped, because they belong to a `ModFeature` that applies them while it is on and removes them when it is off.

If a class fails to patch, the mod fails with an `InvalidOperationException` that names the class.

With `autoPatch: false`, patch yourself:

```csharp
public override void OnLoad()
{
    Harmony.PatchAll(GetType().Assembly);
}
```

`PatchAll` also applies category classes, so only use it if you don't have any.

## Threading

`MainThread` (public static) runs work on Unity's main thread.

| Member | Does |
| --- | --- |
| `IsCurrent` | True on the main thread. |
| `Post(Action)` | Queues an action for the next host `Update`. Thread-safe. |
| `Invoke<T>(Func<T>, int timeoutMs = 10000)` | Runs the function on the main thread and waits for the result. Runs it directly if already on the main thread. Throws `TimeoutException` after the timeout. |

Don't call `Invoke` from code the main thread waits on. It deadlocks until the timeout.

## Settings file

`<game>/NeetLoader/settings.json` is a `LoaderSettings`. It is written when the mod menu changes a switch.

| Field | Type | Default | Meaning |
| --- | --- | --- | --- |
| `DisabledMods` | string[] | `[]` | Ids of mods switched off. Applied on the next launch. Mods that are always on load anyway. |
| `AutoReload` | bool | `true` | Reload hot-reload mods when their DLLs change. |
| `VerboseLog` | bool | `false` | Write `Debug` lines to `latest.log`. |

```json
{
  "DisabledMods": [ "PumpkinSmash" ],
  "AutoReload": true,
  "VerboseLog": false
}
```

Edit the file while the game is closed. The loader reads it at startup.
