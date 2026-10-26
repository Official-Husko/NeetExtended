# Third-party libraries and the dev-console bridge

Focus: what a mod framework and the dev bridge (console commands) can use. Paths are relative to the project root. Line numbers refer to the decompiled files as they currently exist.

## 1. IngameDebugConsole (`decompiled/IngameDebugConsole.Runtime/`)

Third-party in-game console (yasirkula's IngameDebugConsole). It is compiled into `NEET_Data/Managed/IngameDebugConsole.Runtime.dll`. Game code uses it in two places only: `[ConsoleMethod]` attributes on `BrainBank` and `PickupBank` (see 1.6). No game code calls `DebugLogConsole` or `DebugLogManager` directly.

### 1.1 Public API of `DebugLogConsole` (static class)

Namespace `IngameDebugConsole` (`decompiled/IngameDebugConsole.Runtime/IngameDebugConsole/DebugLogConsole.cs:10-12`). Every member below is `public static` unless noted.

**Adding commands**

```csharp
// Delegate forms (command names are case-insensitive; "scene.load" style dotted names are fine)
void AddCommand(string command, string description, Action method);                       // :440
void AddCommand<T1>(string command, string description, Action<T1> method);               // :445
void AddCommand<T1>(string command, string description, Func<T1> method);                  // :450
void AddCommand<T1, T2>(string command, string description, Action<T1, T2> method);       // :455
void AddCommand<T1, T2>(string command, string description, Func<T1, T2> method);         // :460
void AddCommand<T1, T2, T3>(string command, string description, Action<T1, T2, T3> method);  // :465
void AddCommand<T1, T2, T3>(string command, string description, Func<T1, T2, T3> method);     // :470
void AddCommand<T1, T2, T3, T4>(string command, string description, Action<T1, T2, T3, T4> method); // :475
void AddCommand<T1, T2, T3, T4>(string command, string description, Func<T1, T2, T3, T4> method);   // :480
void AddCommand<T1, T2, T3, T4, T5>(string command, string description, Func<T1, T2, T3, T4, T5> method); // :485
void AddCommand(string command, string description, Delegate method);                    // :490
// Same delegate overloads with explicit parameter names, for example:
void AddCommand<T1>(string command, string description, Action<T1> method, string parameterName);  // :495
void AddCommand<T1, T2>(string command, string description, Action<T1, T2> method, string parameterName1, string parameterName2); // :500
// ... up to <T1..T5> (lines 495-530)
void AddCommand(string command, string description, Delegate method, params string[] parameterNames); // :535

// Reflection-based forms
void AddCommandInstance(string command, string description, string methodName, object instance, params string[] parameterNames); // :423
void AddCommandStatic(string command, string description, string methodName, Type ownerType, params string[] parameterNames);    // :435
```

**Removing commands**

```csharp
void RemoveCommand(string command);     // :656, removes every command with that name
void RemoveCommand(Action method);      // :671
void RemoveCommand<T1>(Action<T1> method);   // :676
void RemoveCommand(Delegate method);    // :721
void RemoveCommand(MethodInfo method);  // :726
// (plus generic overloads for Func/Action up to 5 parameters, lines 671-721)
```

**Executing and querying**

```csharp
void ExecuteCommand(string command);                                          // :765
void LogAllCommands();                                                        // :272
void LogAllCommandsWithName(string commandName);                              // :299
void FindCommands(string commandName, bool allowSubstringMatching, List<ConsoleMethodInfo> matchingCommands); // :926
string GetAutoCompleteCommand(string commandStart);                           // :741
void LogSystemInfo();                                                         // :331
```

`ExecuteCommand` trims the input, splits arguments with `FetchArgumentsFromCommand` (quoted strings and bracket pairs are kept together, `inputDelimiters`, set in the static constructor), then looks for overloads with a matching argument count. For each candidate in registration order it parses every argument. The first candidate where all arguments parse is invoked (`DebugLogConsole.cs:846-889`). If none match, it logs a warning (`ERROR: can't find command ...`, or `doesn't take N parameter(s)`).

**Custom parameter types**

```csharp
public delegate bool ParseFunction(string input, out object output);   // DebugLogConsole.cs:14

void AddCustomParameterType(Type type, ParseFunction parseFunction, string typeReadableName = null); // :398
void RemoveCustomParameterType(Type type);                                                          // :417
```

Built-in parsers, all reached through `ParseArgument(string, Type, out object)` (`:1161`):
- `string`, `bool`, `int`, `uint`, `long`, `ulong`, `byte`, `sbyte`, `short`, `ushort`, `char`, `float`, `double`, `decimal` (`:1183-1282`)
- `Vector2`, `Vector3`, `Vector4`, `Quaternion`, `Color`, `Color32`, `Rect`, `RectOffset`, `Bounds`, `Vector2Int`, `Vector3Int`, `RectInt`, `BoundsInt` (`:1289-1349`)
- `GameObject` (`ParseGameObject`, `:1354`), any `Component` subtype (`ParseComponent`, `:1360`)
- enums (`ParseEnum`, `:1367`) and arrays (`ParseArray`, `:1422`, with `IsSupportedArrayType`, `:1114`)

Custom types: `AddCustomParameterType(typeof(MyType), (string input, out object o) => { ...; o = value; return true; }, "MyType")`. The `ParseFunction` signature is `bool (string input, out object output)`.

Example (only calls with signatures read from the code above):

```csharp
using IngameDebugConsole;

// No parameters
DebugLogConsole.AddCommand("mod.hello", "Prints a greeting", () => UnityEngine.Debug.Log("hi"));

// One string parameter, named for the help text
DebugLogConsole.AddCommand<string>("mod.echo", "Prints text",
    (string s) => UnityEngine.Debug.Log(s), "text");

// Run from code, same path as typing into the console
DebugLogConsole.ExecuteCommand("mod.echo hello");

// Remove everything registered under that name
DebugLogConsole.RemoveCommand("mod.echo");

// Custom parser for a mod-defined type
DebugLogConsole.AddCustomParameterType(typeof(MyType),
    (string input, out object output) => { output = MyType.Parse(input); return output != null; },
    "MyType");
```

### 1.2 Attribute-based discovery (`[ConsoleMethod]`)

- Attribute: `ConsoleMethodAttribute(string command, string description, params string[] parameterNames)` (`IngameDebugConsole/ConsoleMethodAttribute.cs`). `AllowMultiple = true`, target `Method`.
- Discovery runs in the **static constructor** of `DebugLogConsole` (`DebugLogConsole.cs:30-270`). The constructor runs once, on first access of any `DebugLogConsole` member.
- It loops over `AppDomain.CurrentDomain.GetAssemblies()` at that moment, skips dynamic assemblies, and skips assemblies whose name starts with one of: `Unity`, `System`, `Mono.`, `mscorlib`, `netstandard`, `TextMeshPro`, `Microsoft.GeneratedCode`, `I18N`, `Boo.`, `UnityScript.`, `ICSharpCode.`, `ExCSS.Unity` (`:213-216`).
- For each remaining assembly it scans `GetExportedTypes()` (public types only) and, within them, `public static` methods declared on the type (`DeclaredOnly | Static | Public`). Methods with `[ConsoleMethod]` are registered via `AddCommand(command, description, methodInfo, null, parameterNames)`.
- Consequences:
  - Mod assemblies that are loaded before the first `DebugLogConsole` access are scanned. Assemblies loaded after that are not, so they must register commands with `AddCommand` directly.
  - Only `public` types and `public static` methods are discovered. `internal` or private methods with the attribute are ignored.
  - `Assembly-CSharp` and `Ink-Libraries` are not excluded, so game and library attributes are picked up.
- Built-in `help` and `sysinfo` are registered in the same constructor (`DebugLogConsole.cs:211-213`).

### 1.3 `DebugLogManager` (the window)

`decompiled/IngameDebugConsole.Runtime/IngameDebugConsole/DebugLogManager.cs` (`:12`): a MonoBehaviour that owns the console UI.

- **Singleton:** `public static DebugLogManager Instance { get; private set; }` (`:326`). It is set in `Awake` (`:342`). `[SerializeField] private bool singleton = true` (`:18`) means that when it is set, the object is `DontDestroyOnLoad`. A second instance is destroyed.
- **Show and hide:** `public void ShowLogWindow()` (`:657`), `public void HideLogWindow()` (`:675`), `public bool IsLogWindowVisible` (`:328`). Events: `OnLogWindowShown` and `OnLogWindowHidden` (`:322`, `:324`).
- **Toggle key:** `private KeyCode toggleKey = KeyCode.BackQuote` (`:62`), used in `Update` with `toggleWithKey && Input.GetKeyDown(toggleKey)` (`:497`). It uses the legacy `Input` API. The game code also uses legacy `Input` (7 files call `Input.Get...`), so the Input Manager is in use.
- **Popup mode:** `PopupEnabled` property (`:330`).
- **Log capture:** `OnEnable` subscribes to `Application.logMessageReceivedThreaded` (`:456-461`) with `ReceivedLog(string logString, string stackTrace, LogType logType)` (`:728`). `OnDisable` unsubscribes. So every `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` from any code (mods included) is shown in the console.
- **Other public API:** `ClearLogs()` (`:876`), `GetAllLogs()` (`:1275`, returns the log text), `SetSnapToBottom(bool)` (`:855`), `ExpandLatestPendingLog()`, `StripStackTraceFromLatestPendingLog()`.
- **Input:** the command input field calls `DebugLogConsole.ExecuteCommand(text)` (`DebugLogManager.cs:719`), so typed commands go through the same dispatcher as the API. Command history is kept in the manager.
- **Commands it registers itself:** `logs.save` with no parameters (`SaveLogsToFile`, saves to `persistentDataPath`) and `logs.save <file>` (`:463-464`, in `OnEnable`).

Usage (only members listed above):

```csharp
if (DebugLogManager.Instance != null)
{
    DebugLogManager.Instance.ShowLogWindow();
    DebugLogManager.Instance.OnLogWindowShown += () => UnityEngine.Debug.Log("console shown");
}
```

**Is it enabled in this build?** Not established from code. Findings:
- No game code (`decompiled/Assembly-CSharp`, `Assembly-CSharp-firstpass`) references `DebugLogManager` or `DebugLogConsole` except the `[ConsoleMethod]` attributes on `BrainBank` and `PickupBank`.
- The string `DebugLogManager` appears in `NEET_Data/globalgamemanagers.assets`, but as a MonoScript type name. That only shows the script is in the build. It does not show that a `DebugLogManager` component is placed in any scene or prefab. Scene and prefab data is not in the decompiled code.
- Because the `[ConsoleMethod]` attributes on `BrainBank` and `PickupBank` are in the game assembly, `DebugLogConsole` is still present and its static constructor will run when it is accessed. The commands can be run from code even if no window exists.
- To know if the window is in the build, check the scene and prefab files with a Unity asset tool (not done here).

**Creating the window from a mod:** `DebugLogManager` is a MonoBehaviour that depends on serialized references (UI prefabs and canvases, see the `[SerializeField]` fields from line 15 on). It cannot be created correctly from code alone. A mod would need to instantiate the prefab from an AssetBundle or from a copy of the scene. Not verified.

### 1.4 Built-in commands (`IngameDebugConsole.Commands`, `IngameDebugConsole.Commands/*.cs`)

All use `[ConsoleMethod]` with `[Preserve]`. They are registered by the discovery in 1.2 (the namespace is `IngameDebugConsole.Commands`, which is not excluded).

| Command | Method | File:line |
|---|---|---|
| `scene.load <name>` | `LoadScene(string sceneName)` | `SceneCommands.cs:9` |
| `scene.load <name> <mode>` | `LoadScene(string sceneName, LoadSceneMode mode)` | `SceneCommands.cs:16` |
| `scene.loadasync <name>` | `LoadSceneAsync(string sceneName)` | `SceneCommands.cs:23` |
| `scene.loadasync <name> <mode>` | `LoadSceneAsync(string sceneName, LoadSceneMode mode)` | `SceneCommands.cs:30` |
| `scene.unload <name>` | `UnloadScene(string sceneName)` | `SceneCommands.cs:53` |
| `scene.restart` | `RestartScene()` | `SceneCommands.cs:60` |
| `prefs.int <key>` | `PlayerPrefsGetInt(string key)` | `PlayerPrefsCommands.cs:8` |
| `prefs.int <key> <value>` | `PlayerPrefsSetInt(string key, int value)` | `PlayerPrefsCommands.cs:19` |
| `prefs.float <key>` | `PlayerPrefsGetFloat(string key)` | `PlayerPrefsCommands.cs:26` |
| `prefs.float <key> <value>` | `PlayerPrefsSetFloat(string key, float value)` | `PlayerPrefsCommands.cs:37` |
| `prefs.string <key>` | `PlayerPrefsGetString(string key)` | `PlayerPrefsCommands.cs:44` |
| `prefs.string <key> <value>` | `PlayerPrefsSetString(string key, string value)` | `PlayerPrefsCommands.cs:55` |
| `prefs.delete <key>` | `PlayerPrefsDelete(string key)` | `PlayerPrefsCommands.cs:62` |
| `prefs.clear` | `PlayerPrefsClear()` | `PlayerPrefsCommands.cs:69` |
| `time.scale` | `GetTimeScale()` (returns `float`) | `TimeCommands.cs:15` |
| `time.scale <value>` | `SetTimeScale(float value)` | `TimeCommands.cs:8` |

Notes:
- The `prefs.int`/`prefs.float`/`prefs.string` getters return `string` (the method signatures in `PlayerPrefsCommands.cs`), not the stored type.
- `help`, `help <name>` and `sysinfo` are in `DebugLogConsole` (`:211-213`). `logs.save` is in `DebugLogManager` (1.3).

### 1.5 Game-side commands (not part of the library)

These use the same `[ConsoleMethod]` attribute and are discovered by the same mechanism:

| Command | Method | File:line |
|---|---|---|
| `get <name>` | `BrainBank.SpawnbrainAtPlayer(string name)` | `decompiled/Assembly-CSharp/BrainBank.cs:51-52` |
| `spawnAt <name> <room>` | `BrainBank.SpawnbrainInRoom(string name, string room)` | `BrainBank.cs:57-58` |
| `get <name>` | `PickupBank.SpawnPickupAtPlayer(string name)` | `decompiled/Assembly-CSharp/PickupBank.cs:68-69` |
| `spawnAt <name> <room>` | `PickupBank.SpawnPickupInRoom(string name, string room)` | `PickupBank.cs:74-75` |

Surprise: both banks register the **same command names** (`get`, `spawnAt`) with the same parameter types. `ExecuteCommand` picks the first registered overload where all arguments parse (section 1.1). So `get <name>` runs whichever bank was registered first. The registration order depends on assembly and type enumeration order during the static constructor, which is not verified. A mod that adds `get` or `spawnAt` will also collide with these.

### 1.6 Minimal bridge pattern

```csharp
using IngameDebugConsole;

public static class ModDevBridge
{
    public static void Register()
    {
        // Attribute discovery only happens once, on the first DebugLogConsole access.
        // Register explicitly, because this mod's assembly may load after that.
        DebugLogConsole.AddCommand("mod.version", "Prints the mod version",
            () => UnityEngine.Debug.Log("mod 0.1"));
    }
}
```

## 2. Managed libraries (`NEET_Data/Managed/`)

Listing from `NEET_Data/Managed` (130 files). Only the non-UnityEngine, non-System libraries are described. Usage counts are from `grep` over `decompiled/Assembly-CSharp` and are not exhaustive.

| Library | What it is | Usable by mods? |
|---|---|---|
| `Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll` | Game code (decompiled in `decompiled/`). | Yes, through reflection, Harmony, or direct references. |
| `IngameDebugConsole.Runtime.dll` | In-game console (section 1). | Yes. `DebugLogConsole` and `DebugLogManager`. |
| `Ink-Libraries.dll` | Ink runtime and compiler (`Ink.Runtime`, `Ink.Compiler`). Story format `inkVersion 20`. Assembly version `0.0.0.0`. | Yes. Used for all dialogue (section `dialogue-and-rendering.md`). |
| `Newtonsoft.Json.dll` | Json.NET. Assembly version `13.0.0.0`; the package string in the DLL is `13.0.2`. Used in 80 game files for save data (`JObject`, `JToken`). | Yes. Save/load code uses it. |
| `SimpleFileBrowser.Runtime.dll` | Runtime file-browser UI. Used by `CustomItemShop.cs`, `CustomImageLoader.cs`, `Computer_FaceChanger.cs`, `Computer_PowerButton.cs`, `WallpaperPicker.cs`. | Yes, for file selection. |
| `Unity.InputSystem.dll` (1.7.0.0), `Unity.InputSystem.ForUI.dll`, `Unity.InputSystem.RebindingUI.dll` | Unity Input System. Game code uses `UnityEngine.InputSystem` in 5 files. The game also uses legacy `Input` in 7 files. | Yes. Both input APIs are present. |
| `Unity.TextMeshPro.dll` | TextMeshPro. Used in dialogue, text effects, and many UI files. Assembly version `0.0.0.0`. | Yes. |
| `Unity.Postprocessing.Runtime.dll` | Post-processing stack (`UnityEngine.Rendering.PostProcessing`). `HouseManager.cs` imports it. | Yes. |
| `Unity.Timeline.dll` (1.0.0.0) | Timeline. No direct `UnityEngine.Timeline` usage found in game code. | Yes, if a mod wants timelines, but nothing in the game uses it. |
| `Unity.AI.Navigation.dll` | NavMesh building package. Game code references `UnityEngine.AI` in 11 files, which is `UnityEngine.AIModule.dll` (the runtime). | Yes, through `UnityEngine.AI`. |
| `UnityEngine.Purchasing*.dll`, `Purchasing.Common.dll`, `UnityEngine.Purchasing.*Stub.dll` | Unity IAP (in-app purchase) plus per-store backends. No game code references them. | Not useful for a mod framework. |
| `Unity.Services.Core*.dll` (Analytics, Configuration, Device, Environments, Networking, Registration, Scheduler, Telemetry, Threading) | Unity Gaming Services core. No game code uses them. | Present but not used. Would need the services to be configured to work. |
| `UnityEngine.Advertisements.dll` | Old Unity Ads. Not referenced. | Not useful. |
| `UnityEngine.InputSystem.Samples.UIvsGameInput.dll` | Input System sample code. Not referenced. | Not useful. |
| `UnityEngine.XR.LegacyInputHelpers.dll`, `UnityEngine.SpatialTracking.dll`, `UnityEngine.ClusterInputModule.dll` | XR/cluster helpers. Not referenced by game code. | Not useful. |
| `Mono.Security.dll`, `System.*.dll`, `mscorlib.dll`, `netstandard.dll` | Mono/.NET base libraries. | Yes, these are the standard framework. |

### 2.1 UnityEngine modules relevant to a mod framework

- `UnityEngine.AssetBundleModule.dll` and `UnityEngine.UnityWebRequestAssetBundleModule.dll` are present. **The game code does not use `AssetBundle`** (grep: 0 files). So a mod can load asset bundles (`AssetBundle.LoadFromFile`, `UnityWebRequestAssetBundle`), but the bundles must be built for this exact Unity version (2022.3.17f1).
- `UnityEngine.IMGUIModule.dll` is present, and `OnGUI` is available. The only `OnGUI` in the game code is `PaintExample.cs:101` (a debug painter). A mod menu with `OnGUI` will work, but it is outside the uGUI UI the game uses.
- `UnityEngine.UI.dll` and `UnityEngine.UIModule.dll` are used by the game (35 files reference `UnityEngine.UI`).
- `UnityEngine.AIModule.dll` (NavMesh), `UnityEngine.PhysicsModule.dll`, `UnityEngine.ParticleSystemModule.dll`, `UnityEngine.AnimationModule.dll`, `UnityEngine.ImageConversionModule.dll` (used by `Texture2D.LoadImage` in `SaveLoadHandler.cs`), and `UnityEngine.ScreenCaptureModule.dll` are all present.
- `UnityEngine.UnityWebRequestModule.dll` and `UnityEngine.UnityWebRequestTextureModule.dll` are present. The game references `WebRequest` in 7 files, so networking is used in some places.

## Modding notes

- **Console command surface for a dev bridge:** `DebugLogConsole.AddCommand(...)` (any overload), `ExecuteCommand(string)` (to run a command from code, for example from a hotkey or a Harmony patch), `RemoveCommand`, and `AddCustomParameterType`. These are enough for a bridge that accepts typed arguments.
- **Register early:** attribute discovery happens once, in the `DebugLogConsole` static constructor. Mods that want `[ConsoleMethod]` discovery must be loaded before the first console access. Otherwise call `AddCommand` explicitly.
- **Name collisions:** `get` and `spawnAt` are already used by `BrainBank` and `PickupBank`. Use namespaced names (`mod.*`) for mod commands.
- **Logs:** `DebugLogManager` captures all `Debug.Log*` output through `Application.logMessageReceivedThreaded` (`DebugLogManager.cs:456-461`), so mod logs appear in the console without extra work.
- **Showing the window:** `DebugLogManager.Instance.ShowLogWindow()` works only if a `DebugLogManager` exists in the scene. Its existence is unverified (section 1.3).
- **Input:** the console's toggle uses legacy `Input.GetKeyDown` (`DebugLogManager.cs:497`). A mod that replaces the toggle should use the same input path the game uses.
- **Newtonsoft and Ink are available** to mods as normal references. Save-data edits must keep the game's `"Save"` array layout (`SaveLoadHandler.cs`).

## Open questions

1. Is a `DebugLogManager` component placed in any scene or prefab in this build? Code only shows that the script exists (`globalgamemanagers.assets` has the MonoScript name). Check the scene and prefab assets with a Unity asset tool.
2. Which of `BrainBank.get` and `PickupBank.get` wins when both are registered? The dispatch picks the first overload in registration order (`DebugLogConsole.cs:846-889`), and the order depends on assembly and type enumeration during the static constructor. Test in the game.
3. The Json.NET version: the assembly version is `13.0.0.0` and the DLL contains the string `13.0.2`. Confirm the package version with `monodis` on the full metadata if it matters.
4. `Unity.TextMeshPro.dll` and `Unity.Postprocessing.Runtime.dll` have assembly version `0.0.0.0`, and the version of `Unity.AI.Navigation.dll` was not checked. Their exact package versions are not known.
5. Whether the `prefs.*` getters returning `string` is intentional, or a bug in the console library.
