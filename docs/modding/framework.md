# NeetModKit framework API

NeetModKit (`Mods/NeetModKit`) is the shared framework for NEET Demo mods. It wraps the game's main objects, adds items and cards, exposes game events, stores mod data in the save file, provides features (switchable parts of a mod), and supplies the mod menu. Mod menu pages, the desktop apps, and the in-game UI kit are covered by a separate guide.

A mod uses the framework by listing it in `mod.json`:

```json
"dependencies": { "NeetModKit": "0.1.0" }
```

Hot reload is enabled per mod with `"hotReload": true`. The framework has it too.

## Ownership and cleanup

Most framework APIs take an owner: a `NeetMod` or a `ModFeature`. Both implement `ITracker` from NeetLoader, which has one method:

```csharp
T Track<T>(T disposable) where T : IDisposable;
```

Anything registered against an owner is disposed when that owner goes away. For a mod, that means on unload or hot reload. For a feature, it means when the feature is switched off. Registrations include event subscriptions, items, cards, console commands, saved-data slots, save formats, menu pages, settings sections and Harmony patches in features.

The rule for mod code: pass `this` (the mod) or the feature as the owner, and do not keep manual lists of things to undo. Use `owner.Track(...)` for anything that has no framework wrapper, such as a `Disposable.Create(() => ...)` cleanup.

`NeetMod` also provides:

| Member | Purpose |
| --- | --- |
| `Info` | `ModInfo` (`Id`, `Directory`, `Manifest.DisplayName`) |
| `Log` | `ModLogger` with `Info`, `Warn`, `Error(message, exception)` |
| `Harmony` | Harmony instance named after the mod id |
| `PathTo(relative)` | Path inside the mod folder |
| `LoadConfig<T>(fileName = "config.json")` | `ConfigFile<T>` with `.Value`, `.Save()`, `.Changed` |
| `CreateObject(name)` | A `DontDestroyOnLoad` GameObject destroyed on unload |
| `StartCoroutine(routine)` | Coroutine on the loader's host, stopped on unload |

## KitMod

`KitMod` is the framework's own mod. Its `OnLoad` sets up in this order: the config file (`Mods/NeetModKit/config.json`, type `KitConfig`), `GameEvents`, `ModSaveData`, `ItemRegistry`, the mod menu, its pages, the settings section, `ModFeatures`, the computer apps, and the built-in "Mods" feature in the pause menu. It also restores store state after a reload.

`KitConfig` fields (edited on the Settings page):

| Field | Default | Meaning |
| --- | --- | --- |
| `MenuKey` | `"F1"` | Unity key name that opens the mod menu |
| `PauseWhileOpen` | `true` | Pause the game while the menu is open |
| `MenuScale` | `1` | Range 0.6 to 2 |
| `ShowWhatsNewOnUpdate` | `true` | Open the What's New page after an update |
| `LastSeenVersion` | `""` | Hidden bookkeeping |

## Game

Namespace `NeetModKit.Game`. These are static helpers over the game's objects. Nothing in them is safe to rely on outside the house scene, and most throw `InvalidOperationException` when the thing is missing.

### GameRefs

Null-safe access. Every property returns `null` outside the house.

- `House`, `Player` (`GameObject`), `PlayerStats`, `PlayerAbilities`, `PlayerMovement`, `PlayerBrain`
- `Computer`, `CardGame` (the `AnimeCardGenerator`)
- `SceneName`, `InHouse` (true when the active scene is `GameRefs.HouseScene`, which is `"NEET_House"`)

### PlayerControl

- `Dictionary<string, float> ReadStats()`: current value of every stat, keyed by stat name (Health, Hunger, ...)
- `Stat FindStat(string name)`: case-insensitive; throws with the list of known names
- `void SetStat(string name, float value)`: goes through the game's setter, so thresholds and UI react
- `bool GodMode { get; }` and `void SetGodMode(bool enabled)`
- `void Teleport(Vector3 target)` and `void Teleport(Location room)`: moves the player and held pickups, zeroes velocity
- `void Look(Vector3 direction)`: sets yaw and clamped camera pitch
- `const float StandingHeight = 0.91f`

God mode blocks `DieAccessor`, `Obliterate` and `SkeletonizeAccessor` on `Player_Abilities` (see Patches) and sets `invincible` and `pausedNeeds` on `PlayerStats`. It is re-applied on each `HouseReady` because the player object is new after a load, so `SetGodMode` before the house exists still takes effect.

### Money

Cash is kept on `Computer`.

- `float Get()`
- `void Set(float value)`: sets `Computer.Money`, which the game taxes. Each increase adds 12% to the tax bill.
- `void SetUntaxed(float value)`: writes the private field and refreshes the cash text. No tax, no achievement tracking.
- `void Add(float amount, bool taxed = true)`: `Add` is taxed by default. Pass `taxed: false` for income that should not be taxed.

PumpkinSmash pays its round score with `Money.Add(cash)`, so that income is taxed. Use `Add(amount, taxed: false)` if the payout should not be.

### Rooms and Location

`Location` is the game's room enum. `Rooms` wraps it:

- `IEnumerable<Location> All`
- `bool TryParse(string name, out Location location)` and `Location Parse(string name)` (case-insensitive; `Parse` throws with the list)
- `bool Exists(Location)`: the room exists in the current scene
- `Vector3? Center(Location)`
- `Vector3? FloorPoint(Location, float lift = PlayerControl.StandingHeight)`
- `Location At(Vector3 point)`

### HouseSystems

- `bool IsOnFire(Location)`, `void StartFire(Location)` (throws if the room is not in the scene), `void StopFire(Location)`
- `void SetPower(Location room, bool on)` and `void SetHousePower(bool on)`: forces power, ignoring fuses

### Spawning

Items come from `PickupBank.pickupsDict`, NPCs from `BrainBank.brainsDict`. Both are filled only inside the house.

- `IEnumerable<string> ItemNames`, `NpcNames`, `bool IsItem(string)`
- `GameObject ItemPrefab(string)`, `NpcPrefab(string)`: throw with "Did you mean" suggestions
- `GameObject ItemNearPlayer(string name)`: spawns in front of the camera
- `GameObject ItemInRoom(string name, Location room)`: spawns at the room centre with a small random push
- `GameObject Item(string name, Vector3 position)`, `GameObject Npc(string name, Vector3 position)`
- `GameObject NpcInRoom(string name, Location room)`
- `Vector3 InFrontOfPlayer(float distance = 0.6f)`

```csharp
GameEvents.WhenHouseReady(this, () =>
{
    if (Spawning.IsItem("Mug") && Rooms.TryParse("Kitchen", out var room))
        Spawning.ItemInRoom("Mug", room);
});
```

`Rooms.TryParse` takes a name and returns false for anything that is not a `Location` member, so a wrong name fails quietly instead of throwing.

## Events

Namespace `NeetModKit.Events`. Handlers run on the main thread. An exception in one handler is logged with the name of the mod that threw it and does not stop the other handlers.

`GameEvent` has no payload. `GameEvent<T>` has one. Both have:

```csharp
IDisposable Subscribe(Action handler);            // or Action<T>
void Subscribe(ITracker owner, Action handler);   // removed when the owner goes away
```

`Subscribe` without an owner returns a handle that unsubscribes when disposed.

Raising iterates a snapshot, so a handler can unsubscribe, or its mod can unload, during a raise.

### GameEvents

| Event | Payload | When it fires |
| --- | --- | --- |
| `HouseReady` | none | Once per house load, on the main thread the update after `SaveLoadHandler.Start`. The save is loaded and `Player`, `Computer` and the registries are usable. Not raised outside the house. |
| `ItemsRegistering` | none | After `PickupBank.Init`, i.e. each time the game rebuilds its item registry. The framework registers its modded items here. |
| `BeforeSave` | none | Before `SaveLoadHandler.SaveAll` runs. |
| `AfterSave` | none | After `SaveAll` returns. |
| `BeforeLoad` | none | Before `SaveLoadHandler.LoadAll` runs. The save is not yet applied. |
| `AfterLoad` | none | After `LoadAll` returns. |
| `SceneLoaded` | `string` scene name | After every scene load. `IsHouseReady` is set false before it is raised. |

Also:

- `bool GameEvents.IsHouseReady` is true between `HouseReady` and the next scene change.
- `void GameEvents.WhenHouseReady(ITracker owner, Action action)` subscribes to `HouseReady` and runs `action` immediately if the house is already ready. Use it for anything a mod does with the house, since a mod can be loaded mid-game.

```csharp
public override void OnLoad()
{
    GameEvents.WhenHouseReady(this, SetUpButtons);
    GameEvents.BeforeSave.Subscribe(this, () => Log.Info("Saving"));
}
```

### Gotchas

- `HouseReady` fires once per house load, not once per session. Put per-house setup there, not in `OnLoad`.
- `WhenHouseReady` runs the action right away when the house is ready. If the action throws on that immediate call it is logged, not rethrown.
- `ItemsRegistering` is for item registration. Items registered in `OnLoad` are kept and added when the registry is built, so there is no need to wait for a scene.
- `SceneLoaded` fires for every scene, including the menus. Check `GameRefs.InHouse` or the scene name.

## Console

`ConsoleCommands` adds commands to the game's IngameDebugConsole.

```csharp
void Add(NeetMod owner, string command, string description, Action method);
void Add<T1>(NeetMod owner, string command, string description, Action<T1> method);
void Add<T1, T2>(...);
void Add<T1, T2, T3>(...);
void Execute(string commandLine);   // runs a line as if typed
List<string> List();               // every registered command, rich text stripped
```

Parameters are parsed by the console from the delegate's types (strings, numbers, bools, enums, vectors).

```csharp
ConsoleCommands.Add(this, "heal", "Refill health", () => PlayerControl.SetStat("Health", 100f));
```

Commands are removed by delegate, so a same-named command from another mod or the game is left alone. There is no overload with more than three parameters.

## Saves

Namespace `NeetModKit.Saves`.

### ModSaveData

Stores a mod's data inside the game's save file. Each mod's data lives in one extra entry, `{ "type": "NeetModKit.ModData", "mods": { "<key>": ... } }`, appended to the `Save` array. The game ignores entries with a type it does not know, so the save still loads without the mods installed.

```csharp
public static void Register<T>(NeetMod owner, Func<T> save, Action<T> load, string key = null)
    where T : class, new();
```

- `key` defaults to the mod id.
- `save` runs when the game saves. The return value is serialized with Newtonsoft.
- `load` runs after a save loads. If the save has nothing for this key (a fresh game, or the mod was just installed), `load` receives `new T()`.

```csharp
public sealed class MyData { public int Visits; }

public override void OnLoad()
{
    ModSaveData.Register(this, () => _data, d => _data = d);
}

private MyData _data = new MyData();
```

Hot reload: when the mod unloads, its current data is parked in the loader's `ReloadState` as JSON. The new copy's `Register` picks it up and calls `load` straight away, so nothing is lost.

### Gotchas

- Keys must be unique across mods. `Register` stores the slot in a dictionary, so a second registration with the same key silently replaces the first.
- The mod entry is appended to the save, never inserted first, because `Save[0]` holds the game's data.
- Nothing is written if no mod has registered a slot.
- The entry is read from the newest `neet*` save file before `LoadAll`, using the same rule the game uses.
- A failing `save` is logged and the game still saves. A failing `load` is logged; the slot keeps its previous value.

### SaveFormats

Lets a mod teach the game to read another save format. The game reads saves through `SaveLoadHandler.LoadAll` (the framework swaps its `File.ReadAllText` call for `SaveFormats.ReadText`).

```csharp
public interface ISaveFormat
{
    string Name { get; }
    bool CanRead(byte[] data);
    string Read(byte[] data);    // returns the save's JSON
}

SaveFormats.Register(ITracker owner, ISaveFormat format);
```

Formats are tried in registration order. If none claims the file and it looks like text (starts with `{` or `[`), it is read as UTF-8 JSON. Otherwise the load fails with a message that names the file and says to install the format's mod.

OpenSaves registers its `.nsav` format this way. It registers the reader for the whole time the mod is loaded. Writing is a separate feature.

### SaveFiles

Internal. Finds the newest `neet*` file in `Application.persistentDataPath` by write time, which matches the game's rule.

## Save safety

The framework changes what the game does with saves that mention content which no longer exists, typically because a mod was removed.

- Unknown items are skipped on load. In `GetTokensByType`, saved `Usable` entries whose `prefabName` is not in `PickupBank.pickupsDict` are removed. A warning lists them with counts.
- Unknown NPCs are skipped the same way. `Brain` entries whose `basicBrainData.prefabName` is not in `BrainBank.brainsDict` are removed.
- The card list is trimmed. If a save has more card entries than the game has cards, the extra entries at the end are removed and a warning is logged. Modded cards are injected before the check, so their saved progress is kept.

Without these, one unknown name throws inside `LoadAll` and nothing after it loads.

## Items

Namespace `NeetModKit.Items`. A modded item is a copy of an existing item (`BasedOn`) with changes applied.

### ItemDefinition

| Field | Meaning |
| --- | --- |
| `string Name` | Required. Unique. The save key, the console `get` name and the store label. |
| `string BasedOn` | Required. Existing item to copy, e.g. `"Mug"`. |
| `string Description` | Store text. Null keeps the original. |
| `float? Price` | Store price. Null keeps the original. |
| `Usable.StoreType? Store` | Which store sells it. Null keeps the original. `Nowhere` hides it from stores. |
| `Texture2D Texture` | Replaces the main texture on every renderer's material. |
| `Color? Tint` | Multiplies each material's colour. |
| `float Scale = 1f` | Multiplies the copied item's scale. |
| `Sprite StorePicture` | Store picture. Null keeps the original. |
| `Action<GameObject> Setup` | Runs once on the template. Add components here. |

### ItemRegistry

```csharp
public static void Register(NeetMod owner, ItemDefinition def);   // owner-tracked
public static bool IsModded(string name);
public static GameObject Template(string name);   // the template copies are made from, once built
public static IEnumerable<string> Names { get; }
```

`Register` throws if `Name` or `BasedOn` is empty, or if a modded item with that name is already registered. The item is removed when the owner unloads.

```csharp
public override void OnLoad()
{
    ItemRegistry.Register(this, new ItemDefinition
    {
        Name = "Pumpkin Mug",
        BasedOn = "Mug",
        Tint = new Color(1f, 0.5f, 0.1f),
        Price = 12f,
        Store = Usable.StoreType.Nowhere,
        Setup = go => go.AddComponent<MyBehaviour>(),
    });
}
```

### How items are built

`ItemTemplateBuilder` (internal) instantiates the `BasedOn` prefab under `PrefabStash.Root`, an inactive object that keeps templates alive. It sets `Usable.prefabName` to `Name`, applies the store fields, recolours renderers, scales, and calls `Setup`. Spawned items are plain `Instantiate` copies of the template. `StoreListings` (internal) adds a button to the store page and an entry in the store's `PickupBank` list, when a store of that type exists in the scene.

### Gotchas

- Items must be in the registry before `SaveLoadHandler.LoadAll` reads the save. The framework calls `RegisterAll` in a prefix on `LoadAll`, and `ItemsRegistering` fires before it, so register in `OnLoad` or in the `ItemsRegistering` handler.
- `PickupBank` only exists in the house. Registration done before then is held and built when the registry is first filled.
- `Register` checks only the framework's own names. A `Name` that matches a game item overwrites that item's entry in `PickupBank.pickupsDict`. Choose a name that is not in `Spawning.ItemNames`.
- `Setup` runs once on the template. Spawned copies do not run it again.
- Store listings need a store of the matching `StoreType` in the scene. Without one, the item is still spawnable but not sold.
- If the mod is removed, saved copies are skipped on load (see Save safety).

## Cards

Namespace `NeetModKit.Cards`. Adds cards to the anime card game on the in-game computer.

### CardDefinition

| Field | Meaning |
| --- | --- |
| `string Name` | Required. Card name, also the name shown in the game. |
| `Texture2D Art` | Card picture. Null keeps the copied card's picture. |
| `Func<Texture2D> ArtProvider` | Builds the picture when the card is created (inside the house). Used when `Art` is null. |
| `float? Income` | Crystals per exp bar fill at level 0. Null uses the game's formula. |
| `float? Price` | Unlock price in crystals. Null uses the game's formula. |
| `float? ExpToLevel` | Exp to fill the bar. Null uses the game's formula. |

The game's progression for card index `i` is income `1 + 4i`, price `10 + 8i`, exp `5 + 4.8i` (`CardGame.DefaultIncome`, `DefaultPrice`, `DefaultExpToLevel`).

### CardGame

```csharp
public static void AddCard(ITracker owner, CardDefinition card);   // owner-tracked

public static AnimeCardGenerator Generator { get; }
public static IReadOnlyList<CardDefinition> ExtraCards { get; }
public static AnimeCard[] Cards { get; }
public static float Crystals { get; set; }
public static int MaxUnlocked { get; }
public static int LevelCap(AnimeCard card);            // max(1, medalLvl) * 50
public static void SetLevel(AnimeCard card, int level); // clamped; updates price, income and the button text
public static float ClickExp(AnimeCard card);
```

`SetLevel` follows the game's own buying rules: price grows 7% per level, and income is `1.67 x base x level`.

```csharp
CardGame.AddCard(this, new CardDefinition
{
    Name = "Ghosta",
    ArtProvider = () => MakeArt(),
    Price = 300f,
});
```

### How cards are added

`CardInjector` (internal) clones the last built-in card for each extra card, gives it the next index and the name, and writes the name into the generator's `animeNames` list. Modded card objects are named `AnimeCard_Mod_<Name>`. The injector runs in a prefix on `AnimeCardGenerator.Start` before the cards are first collected, and in a prefix on `AnimeCardGenerator.Load` (see Patches). It does nothing if there are no extra cards or if the modded cards are already there.

### Gotchas

- Cards are collected once per house load. A card added while the house is loaded appears after the next house load.
- Extra cards are appended after the game's own. Removing a mod's cards does not change the index of any built-in card. Saved progress for the removed cards is dropped on load.
- `Name` is not checked for duplicates against the game's cards.
- Exp grows by 1 per second plus clicks, so `ExpToLevel` is how fast a card levels.

## Features

Namespace `NeetModKit.Features`. A feature is a part of a mod that the player can switch on and off, such as an auto-clicker or a set of extra cards. A feature is not a separate mod. Gameplay that can stand on its own should ship as its own mod. Use a feature for finer switches inside a mod.

### ModFeature

```csharp
public abstract class ModFeature : ITracker
{
    public abstract string Id { get; }          // unique within the mod; also the Harmony category
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual bool EnabledByDefault => true;
    public virtual string AppliesWhen => null;  // e.g. "after the house reloads"; shown next to the switch
    public NeetMod Owner { get; }
    public bool Enabled { get; }
    protected virtual void OnEnable() { }
    protected virtual void OnDisable() { }
    public virtual void OnUpdate() { }          // every frame while on, only if overridden
    public T Track<T>(T disposable) where T : IDisposable;
}
```

When a feature is switched on, the framework applies the Harmony patch classes marked `[HarmonyPatchCategory(Id)]` from the mod's assembly, then calls `OnEnable`. When switched off, it calls `OnDisable`, disposes everything passed to `Track`, then removes the category's patches. The mod's loader skips categorized patch classes at load, so they only exist while the feature is on.

```csharp
internal sealed class AutoClickerFeature : ModFeature
{
    public override string Id => "AutoClicker";
    public override string Name => "Auto-clicker";
    public override string Description => "Clicks the current card for you.";
    public override bool EnabledByDefault => false;

    protected override void OnEnable() => GameEvents.WhenHouseReady(this, Start);
}
```

Patch class for a feature:

```csharp
[HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.SaveAll))]
[HarmonyPatchCategory(Features.SaveCleanupFeature.FeatureId)]
internal static class SaveCleanupPatch
{
    private static void Postfix() => SaveCleanup.Prune();
}
```

### ModFeatures

```csharp
public static void Register(NeetMod owner, ModFeature feature);  // owner-tracked; enables if wanted
public static bool IsWanted(ModFeature feature);
public static void SetEnabled(ModFeature feature, bool on);      // switches now and saves the choice
public static IReadOnlyList<ModFeature> Features { get; }
public static List<ModFeature> Of(ModInfo mod);
```

The player's choices are saved in `Mods/NeetModKit/features.json` under `"<modId>/<featureId>"`. A feature with no saved choice uses `EnabledByDefault`.

### Gotchas

- `Id` must match the `[HarmonyPatchCategory]` value. A mismatch means the patches are never applied.
- `Register` enables the feature at once if the player wants it on, so anything `OnEnable` depends on must already exist when `Register` is called.
- If `OnEnable` or `OnDisable` throws, the feature is switched off and the error is logged. The choice in `features.json` is unchanged, so it comes back on next launch.
- If `OnUpdate` throws, the feature is switched off the same way. Only features that override `OnUpdate` are updated.
- `Track` cleans up on switch-off. Anything created in `OnEnable` that is not tracked stays alive.

## Settings

Namespace `NeetModKit.Settings`.

```csharp
ModSettings.Register<T>(NeetMod owner, ConfigFile<T> config, string title = null) where T : class, new();
```

This adds a section to the mod menu's Settings page. The page builds one control per public instance field of the config class. It writes the config a moment after the last edit.

### Field types and controls

| Field type | Control |
| --- | --- |
| `bool` | Toggle |
| enum | One button per value |
| `float` or `int` with `[Range(min, max)]` | Slider. `int` snaps to whole numbers. |
| `string` | Text box |
| `float`, `int`, `double` without `[Range]` | Text box. Bad input is ignored. |
| anything else | Text line: "edit in the config file" |

Attributes on fields:

- `[Range(min, max)]` (`RangeAttribute`): slider. Only applies to `float` and `int`.
- `[Label("text")]` (`LabelAttribute`): replaces the prettified field name ("PauseWhileOpen" becomes "Pause while open").
- `[Description("text")]` (`DescriptionAttribute`, from System.ComponentModel): help text under the control.
- `[Hidden]` (`HiddenAttribute`): leaves the field off the page.

Properties are not shown. Use public fields.

```csharp
public sealed class MyConfig
{
    [Description("Crystals one summon costs.")]
    [Range(50, 5000)]
    public int SummonCost = 500;

    [Label("Shiny chance")]
    [Range(0f, 0.5f)]
    public float ShinyChance = 0.05f;

    public Mode Mode = Mode.Normal;   // buttons
    [Hidden] public string Seen = "";
}
```

### Gotchas

- A `[Range]` on a `double` is ignored and the field becomes a text box.
- A field of a class type (other than string) only appears as a text line. Store such data in `ModSaveData` or in the config file.
- A registered section is removed when the mod unloads.

## Registry

`OwnedList<T>` (namespace `NeetModKit.Registry`) is a list whose entries belong to a mod. The framework uses it for settings sections and menu pages.

```csharp
public void Add(NeetMod owner, T item);   // removed when owner unloads
public void Remove(T item);
public void Sort(Comparison<T> comparison);
public IReadOnlyList<T> Items { get; }
public event Action Changed;             // after an add or remove
```

## Reflection

Namespace `NeetModKit.Reflection`. Used by the dev tools; available to any mod.

- `TypeFinder.Find(string name)` and `Require(string name)`: finds a type by full or short name across all loaded assemblies. Game types win. Assemblies from old reloaded copies are skipped.
- `MemberAccess.FindMember(Type, string)`: a field or property, searching base classes. `Get(object, MemberInfo)` and `Set(object, MemberInfo, object)` read and write it. `FindMethods(Type, string)` returns every overload. `Inspectable(Type)` lists readable members, excluding Unity base classes. Results are cached.
- `MethodInvoker.Call(Type type, object target, string name, JArray args)`: picks an overload whose parameters fit the JSON arguments, fewest defaulted parameters first. A static method has a `null` target.

## Values

`JsonValues` (namespace `NeetModKit.Values`) converts between game values and JSON.

- `JToken ToJson(object value, int depth = 1)`: vectors and colours become arrays (`[x,y,z]`, `[r,g,b,a]`), enums become names, Unity objects become `{name, type, id, path}`, and other objects are expanded field by field up to `depth`. Collections stop at 100 items.
- `object FromJson(JToken token, Type type)`: the reverse for the same types. A string for a Unity object type is resolved as a scene target (see Scenes).

## Scenes

`SceneQuery` (namespace `NeetModKit.Scenes`) finds GameObjects from text.

- `GameObject Resolve(string target)`: `"#12345"` is an instance id. Anything else is a path such as `"Root/Child"`. A bare name that is not a root falls back to a search of the whole scene.
- `List<GameObject> Roots()`: roots of every loaded scene plus `DontDestroyOnLoad`.
- `string PathOf(GameObject go)`
- `List<GameObject> Find(string name, Type component, bool includeInactive, int max)`: name substring and/or component type.

## Assets

`ModAssets` (namespace `NeetModKit.Assets`) loads files from the mod folder. All results are freed when the owner unloads.

```csharp
Texture2D LoadTexture(NeetMod owner, string path);    // PNG or JPG; relative to the mod folder
Sprite LoadSprite(NeetMod owner, string path);
Sprite ToSprite(NeetMod owner, Texture2D texture);    // 100 pixels per unit, pivot centre
AssetBundle LoadBundle(NeetMod owner, string path);   // build with Unity 2022.3 for Windows
Texture2D ReadableCopy(NeetMod owner, Texture source);// readable copy of any texture, via a render texture
```

Absolute paths are used as they are. A missing file throws `FileNotFoundException`.

Scripts inside an asset bundle must come from an assembly the mod has already loaded.

## Patching

`CallSwap.Replace(code, original, replacement)` (namespace `NeetModKit.Patching`) is a transpiler helper. It replaces each `call` to `original` with a `call` to a static `replacement` that takes the same stack values. For an instance method the instance comes first.

`SaveReadPatch` uses it on `LoadAll` to route `File.ReadAllText` through `SaveFormats.ReadText`.

## Which game methods are hooked, and why

The framework's patches live in `src/NeetModKit/Patches/`. Each one is an applied Harmony patch when the framework loads.

| Game method | Patch | Why |
| --- | --- | --- |
| `PickupBank.Init` (postfix) | `LifecyclePatches` | Raises `ItemsRegistering` |
| `SaveLoadHandler.SaveAll` (pre/post) | `LifecyclePatches` | `BeforeSave`, `AfterSave` |
| `SaveLoadHandler.LoadAll` (pre/post) | `LifecyclePatches` | `BeforeLoad`, `AfterLoad` |
| `SaveLoadHandler.LoadAll` (pre) | `ItemPatches` | Registers modded items before the save is read |
| `SaveLoadHandler.LoadAll` (transpiler) | `SaveReadPatch` | Reads any save format through `SaveFormats` |
| `SaveLoadHandler.LoadAll` (pre/post) | `ModSaveDataPatches` | Reads the mod entry before load, delivers it after |
| `SaveLoadHandler.AllSaveDataToJSon` (post) | `ModSaveDataPatches` | Appends the mod entry to the save |
| `SaveLoadHandler.Start` (post) | `LifecyclePatches` | Queues `HouseReady` for the next main-thread update |
| `SaveLoadHandler.GetTokensByType` (post) | `SaveGuardPatches` | Drops unknown items and NPCs |
| `AnimeCardGenerator.Start` (pre) | `CardPatches` | Injects modded cards before the card list is collected |
| `AnimeCardGenerator.Load` (pre) | `SaveGuardPatches` | Injects modded cards, then trims extra saved card entries |
| `StoreGenerator.Start` (post) | `ItemPatches` | Adds store buttons for modded items, after the store's own wiring |
| `StoreGenerator.OnEnable` (post) | `ItemPatches` | Grows the store page for the extra rows |
| `Player_Abilities.DieAccessor`, `Obliterate`, `SkeletonizeAccessor` (pre) | `GodModePatches` | Blocks death while god mode is on |
| `CameraControl.Update` (post) | `MenuCursorPatch` | Keeps the cursor free while the mod menu is open |
| `Computer_Browser_Addresser.AddressWrapper` (pre) | `BrowserPatches` | Lets the framework choose the page for a URL (see the desktop guide) |

### Reasons for the less obvious ones

- `SaveLoadHandler.Start` raises `HouseReady` on the next update rather than immediately, so every other `Start` has finished when mods run.
- The store patches run after the store's own `Start`, because the store wires its buttons by index and must not see the framework's buttons.
- The card patches run before the game collects its cards, because the game collects them once.

## Hot reload

Hot reload runs an unload and a load for a mod. The framework keeps these things across it.

- Mod save data: the data at unload goes to `ReloadState` and is loaded by the new copy's `ModSaveData.Register`.
- Store state: `StoreListings` records which stores have started and restores that after reload, because Unity has no "Start already ran" flag.
- Features: each feature's patches and tracked objects are removed on unload and re-enabled by the new copy's `ModFeatures.Register`, using the saved choice.
- `ReloadState` is owned by the loader and is never reloaded, so values put there survive a reload of the framework itself. Store plain data (strings, lists of ids), not objects from the reloading assembly.
