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

`KitMod` is the framework's own mod. Its `OnLoad` sets up in this order: the config file (`Mods/NeetModKit/config.json`, type `KitConfig`), `GameEvents`, `ModSaveData`, `ItemRegistry`, the dialogue helpers (`GameStory`, `InkFunctions`, `InkScripts`), the mod menu, its pages, the settings section, the version tag, `ModFeatures`, the computer apps, and the built-in "Mods" feature in the pause menu. It also restores store state after a reload.

`KitConfig` fields (edited on Mod Settings, under Framework):

| Field | Default | Meaning |
| --- | --- | --- |
| `MenuKey` | `"F1"` | Unity key name that opens the mod menu |
| `PauseWhileOpen` | `true` | Pause the game while the menu is open |
| `MenuScale` | `1` | Range 0.6 to 2 |
| `ShowVersion` | `true` | Show the pack's name, version and build in the bottom left corner |
| `ShowWhatsNewOnUpdate` | `true` | Open the What's New page after an update |
| `LastSeenVersion` | `""` | Hidden bookkeeping |

### Pack name and version

`NeetModKit.About.PackInfo` holds the pack's name (`PackInfo.Name`, "NeetExtended"; change it there to rename the pack in game) and the build: `Version` ("0.1.0", from `<Version>` in `Directory.Build.props`), `Commit` (the short commit hash, with `-dirty` when built from uncommitted changes, or null) and `Label` ("NeetExtended v0.1.0 (1a2b3c4)"). `Title` is the name with spaces between its words ("Neet Extended"), for the all-caps headers like the mod menu's. `build/Revision.targets` puts the hash into every assembly's informational version ("0.1.0+1a2b3c4") by running `git describe` before the SDK writes the assembly attributes; outside a git checkout the version stays plain.

`About/GameCompat` compares the running game with the build this version was tested with: the framework ships `compat/game.json` as `game-compat.json`, and its `gameCode.mvid` is compared with the module version id of the loaded `Assembly-CSharp`. `GameCompat.Status` is `Tested`, `Untested` or `Unknown` (no record), and the log says which (see [tools.md](tools.md#compat)).

The version tag (`Ui/VersionTag.cs`) shows `PackInfo.Label` plus any notes in the bottom left corner of every scene: a canvas at sorting order 900 (under the mod menu's windows), without a raycaster, so it never takes clicks. Mods add notes with `VersionTag.AddNote(owner, text)`; the label reads `NeetExtended v0.2.0 (1a2b3c4)  ·  note one  ·  note two`. The result is an `IDisposable`: dispose it to take the note back. It also goes when `owner` unloads, and the label updates either way. The framework adds "game build not tested" itself when `GameCompat.Status` is `Untested`; Update Checker adds "update available (v0.3.0)". Keep notes short (a few words), since they share one line.

```csharp
IDisposable note = VersionTag.AddNote(this, "update available (v0.3.0)");
// ...later
note.Dispose();
```

The What's New page uses `PackInfo.Name` for the pack's own section, and so does the framework's settings section.

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
- `bool NeedsFrozen { get; }` and `void SetNeedsFrozen(bool frozen)`: needs stop draining without god mode
- `float SpeedMultiplier { get; }` and `void SetSpeedMultiplier(float)`: walking speed (clamped 0.1-10). Speed is a "sticky" stat the game resets to its base value every frame, so this changes the base value
- `void Teleport(Vector3 target)` and `void Teleport(Location room)`: moves the player and held pickups, zeroes velocity
- `void Look(Vector3 direction)`: sets yaw and clamped camera pitch
- `const float StandingHeight = 0.91f`

God mode blocks `DieAccessor`, `Obliterate` and `SkeletonizeAccessor` on `Player_Abilities` (see Patches) and sets `invincible` and `pausedNeeds` on `PlayerStats`. God mode, frozen needs and speed are re-applied on each `HouseReady` because the player object is new after a load, so setting them before the house exists still takes effect.

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

### Sky

`Sky` (in `NeetModKit.Game`) is the handle on the house's weather: the lights, lightning, the thunder timer, the rain, fog and storm sound, and a weather state. The house is one fixed rainy night with thunder; it has no weather system of its own (see "Weather & sky" in `docs/game/world-environment.md`). Null-safe outside the house.

```csharp
// A storm: lots of rain, thick fog, loud wind, a strike every 8 to 15 seconds.
Sky.RainAmount.Set(this, 3f);
Sky.FogAmount.Set(this, 2f);
Sky.StormVolume.Set(this, 1.5f);
Sky.SetThunderDelay(this, (min, max) => UnityEngine.Random.Range(8f, 15f));
Sky.Reschedule();                              // use it for the wait that's running now
Sky.SetWeather(this, WeatherKind.Storm, 1f);   // tell everyone else
Sky.OnStrike.Subscribe(this, () => Log.Info("Lightning"));
```

| API | |
| --- | --- |
| `Sky.Moon`, `Sky.Sun` | The `Light`s: the moon (blue, 0.2, the only light on) and the orange sun the house keeps switched off. Null when the scene has none. |
| `Sky.MoonBaseIntensity`, `Sky.SceneFogDensity` | The scene's own values (0.2, 0.06), remembered for each house load. |
| `Sky.Strike()` | Lightning now: the game's flash, lamp flicker and clap (the instant `HouseManager.Thunder`). The random loop is left alone. False without a house, and on a co-op client (only the host's strikes are real there). |
| `Sky.OnStrike` | `GameEvent`, raised when a strike peaks, whoever started it (the loop, `Strike()`, the story, a co-op host's strike played on a client). Follows the house through loads and framework reloads. |
| `Sky.NextStrikeIn` | Seconds until the loop's next random strike; -1 when it isn't waiting (mid-strike, stopped, or a co-op client). |
| `Sky.ThunderLoopAlive` | The random loop is waiting or mid-strike. It ends for the session after the story's strike unless Unofficial NEET Patches' ThunderLoop fix is on. |
| `Sky.SetThunderDelay(owner, (min, max) => seconds)` | Takes over the wait between random strikes (the game's own range is 30 to 180 s; at least 1 s is used). `null` gives the timer back; the last mod to set one wins, and it goes away with its mod. Applies from the loop's next wait. |
| `Sky.StrikeIn(seconds)`, `Sky.Reschedule()` | Move the wait that's running now (to a fixed time, or to a new answer from the provider). False when the loop isn't waiting. |
| `Sky.RainAmount`, `Sky.FogAmount`, `Sky.StormVolume` | `SkyMultiplier`s. `Set(owner, factor)` (0 to 20), `Clear(owner)`, `Value` (the product of every mod's factor). Rain scales the emission of the four `Particle_Rain_*` systems (3 is a downpour, 0 is dry; it also stops rain putting out flames) and raises their particle cap, fog scales the scene's density, the storm volume scales the loop's volume after the game sets it on every room change (Unity keeps the result at 1 or less). A factor goes away with its mod; with none left the scene's own value is back. |
| `Sky.RainSystems` | The four rain particle systems, to read (counts, positions). Change them through `RainAmount`. |
| `Sky.Weather`, `Sky.WeatherChanged`, `Sky.WeatherOwner`, `Sky.SetWeather(owner, state)` | The weather state, below. |

`WeatherState` is a small value: `Kind` (`WeatherKind.Drizzle`, `Rain`, `Storm`), `Intensity` (1 is the kind's usual strength), `StartedAt`, an optional forecast (`NextKind`, `NextAt`), and helpers `Name` (the word players read), `NextName`, `Age`, `SecondsUntilNext`. Times are play seconds (`ModFlags.PlaySeconds`). With no mod driving it the weather is a steady `Rain`. `SetWeather` is for the mod that drives the weather (the Weather mod on the host; Co-op on a client that follows the host): the last mod to set it owns it, it goes back to the default when that mod unloads, and `WeatherChanged` is raised with each new state. It only keeps the state: reading mods apply their own effects (the multipliers above, a phone line, a mood).

Rules can read it: the `weather` condition (`"weather": "storm"`, `"weather": ["drizzle", "rain"]`), the `forecast` condition (`"forecast": false` for no forecast, `"forecast": "storm"` for one that says storm) and the text values `{{weather}}`, `{{weather:next}}` and `{{weather:in}}` (time until the change, in words).

Gotchas:

- The multipliers, the delay provider and the weather all belong to the mod that set them and go away when it unloads, so a mod that hot reloads sets them again in `OnLoad`. House loads keep them: they're applied again to each new house.
- The thunder timer is read from the loop's own code. If a game update changes it, the kit logs a warning once, `NextStrikeIn` stays -1 and `SetThunderDelay` does nothing.
- Without the ThunderLoop fix, the story's strike (Ghostboy, Night 1) ends the random loop. `Sky.Strike()` never does that.

### Doors

The house's doors (`HouseManager.doors`). Locked doors are matched by `doorId`: `BASEMENT` (the black door), `DADYARD`, `Attic`.

- `IEnumerable<Door> All`, `IEnumerable<Door> WithIds(params string[] ids)` (ids are case-insensitive)
- `int Unlock(string id)`, `int UnlockAll()`, `int SetLocked(IEnumerable<Door> doors, bool locked)`: go through the game's `Locked` setter so unlock listeners fire, and return how many doors changed

### Deliveries

Packages dropped at the front door the way the computer's stores deliver orders, but free.

- `GameObject Drop(string itemOrNpc)`: a box holding one item (or NPC) prefab, by name
- `GameObject Drop(GameObject prefab)`: same, for a prefab you already have

The box falls in at `HouseManager.packageDropOffPoint` and is sized to its content like the game's own boxes.

### WorldRole

`WorldRole.RunsWorld` is false while this game shows a house another game runs (a co-op client). Anything that happens on its own, like timed calls, emails or random events, should check it, or it happens once per player. `WorldRole.ShowOtherWorld(owner)` turns it off until the handle is disposed or the owner unloads.

`WorldRole.IsShared` is true while other players are in this game's house or this game is in theirs (a co-op mod holds `WorldRole.ShareWorld(owner)` for the session). Nothing should stop time then, or it stops for everyone: the framework's windows don't pause the game (`UiInputLock`). `WorldRole.Changed` fires when either flips.

### Who decides the mod setup

`NeetModKit.Sharing`. Normally the player decides which mods run, their features and their settings. A co-op client follows the host for the mods that shape the shared house, so the framework lets another mod take over that decision:

- `ModControl.HandOver(owner, reason, covers)` locks the mods `covers(modId)` says yes to until the handle is disposed; `ModControl.LockedBecause(modId)` gives the reason or null. The Mods page shows locked mods without their load and unload buttons and their features as ON or OFF, and hides RELOAD ALL and FIND NEW MODS while anything is locked. `ModControl.Version` and `Changed` move whenever a page should redraw.
- `ModFeatures.Override(key, on)` switches a feature (key `modId/featureId`, loaded or not) the way another game decided, without saving; `ClearOverrides()` goes back to the player's own choices. `ModFeatures.Changed` fires after any feature switches.
- `[Shared]` on a config field marks a setting that shapes the shared house (prices, chances, timings). `SharedSettings.Capture()` gives every such value as JSON; `Apply(json)` takes another game's values for locked mods, in memory only (the mod hears `Changed` as usual), and `Restore()` puts the player's own back. Saving a section while another game's values are in still writes the player's own. `SharedSettings.Saved` fires when a section with shared settings is saved.

### HostActions and SharedState

For player actions and state that have to be the same for every co-op player. In single player both simply work here.

- `HostActions.Register(owner, "MyMod.Thing", (args, player) => ...)` and `HostActions.Run("MyMod.Thing", args)`: the action runs where the house runs. On a co-op client the Co-op mod sends it to the host, which runs it with that player's slot (`player` is 0 for this game's own player).
- `SharedState.Publish(key, value)` where the house runs, `SharedState.TryGet(key, out value)` anywhere, and `SharedState.Changed` for every change (published here or arriving from the host). The Co-op mod passes values on and fills in a joining player. Values belong to the house: loading a scene clears them. Name keys after your mod.
- Email Expansion uses both: opening and replying are actions, and where each conversation stands is shared state, so every player sees the same emails and anyone can answer.

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
| `WokeUp` | none | Right after `HouseReady` when the player comes back from sleeping (the house loads after the DreamZone scene). The framework counts days with it (`ModFlags.Day`). |
| `ItemOrdered` | `StoreOrder` | After the player orders something from a computer store and the order went through. `Item` is the prefab name, `Store` is `Normal`, `BlackMarket` or `OccultMarket`. |

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

## Dialogue

Namespace `NeetModKit.Conversations`. The game keeps all its dialogue in one Ink story, `Dialogue._.masterStory`: NPC conversations, phone calls, everything the dialogue box shows. Emails have a second story of their own, `EmailHandler._.masterStory`. These helpers add to those stories, so modded lines play in the game's own dialogue box (or inbox) with its choices, tags, voices and text effects.

### InkStory

One of the game's Ink stories: `InkStory.Dialogue` or `InkStory.Email` (`InkStory.All` has both).

- `Story Current`: the live story, or `null` in scenes without it.
- `GameEvent<Story> Created`: raised when the game builds a new story (every house load builds both), before it runs and before a save is loaded into it.
- `WhenCreated(owner, Action<Story>)`: subscribes and runs right away for the current story.
- `GameFunctions`: the game's own external functions for that story (the email story has none).
- `HasKnot(path)`: whether the live story has the knot a path starts in. Knots from a mod that's gone fail it.

`GameStory` is the same for the dialogue story (`GameStory.Current`, `GameStory.Created`, `GameStory.WhenCreated`).

### InkScripts

```csharp
var script = InkScripts.Add(this, "hello.ink", "=== MyMod_Hello ===\nHi there!\n* Hi.\n- Bye.\n-> DONE");
Dialogue._.InitCharacterDialogue(speaker, "MyMod", "MyMod_Hello");
```

- `InkScript Add(owner, string name, string inkSource)`: compiles the source with the Ink compiler the game ships and adds its knots and functions to the dialogue story. They're removed when the owner cleans up, and added again to every new story.
- `InkScript Add(owner, InkStory target, string name, string inkSource)`: the same for another story, e.g. `InkStory.Email`.
- `InkScript` has `Name`, `Source`, `Knots` (top-level names it added), `Problems` (compiler errors and warnings, with line numbers counted in your source) and `Failed`.
- `bool IsModKnot(string knot)`, `bool IsModKnot(InkStory story, string knot)`, `bool IsModPath(string path)`: whether something came from a mod script.

Scripts compile when the game first creates their story (right away if one exists), once per script. Dialogue scripts can use the game's lists (`OfferingStates`, `PlayerStates`), global variables (`nightId`, `playerBody`, ...) and every function in `InkFunctions` without declaring them: a hidden header declares them for the compiler. Email scripts get the functions only; the game's email story has no lists or variables.

Rules:

- Knots go in at the top level of the story. A name that already exists (in the game or another script) fails the whole script with a problem saying so. Prefix knot names with your mod id.
- `VAR` declarations in a mod script don't create variables in the game's story; keep state in your own save data and read it through a function.
- Diverts into the game's own knots (`-> Phone.p411`) don't compile, because the compiler only sees your script.
- `INCLUDE` isn't supported.

### InkFunctions

Functions dialogue can call, like the game's `GiveItem` or `KnockoutPlayer`:

```csharp
InkFunctions.Register(this, "MyMod_Cash", new[] { "amount" }, args => { Money.Add(InkValues.Num(args, 0)); return null; });
// In Ink: ~ MyMod_Cash(20)
```

- `Register(owner, name, string[] parameters, Func<object[], object> body, bool lookaheadSafe = false)`. The function is bound in every story (dialogue and email). Return a value for functions used in expressions, `null` for statements. Leave `lookaheadSafe` off for anything with side effects: Ink may call lookahead-safe functions early, and more than once.
- `InkValues.Str/Num/Int/Bool(args, index)` read arguments whatever type Ink passed.
- `GameInkFunctions.All` lists the game's own 23 functions with their parameter names.

A function whose mod unloads keeps answering with `0` (and doing nothing), so a script that still calls it can't stop a conversation.

### Save safety

Ink saves where each conversation ("flow") stopped. A save that points into a mod script can't be loaded once the script is gone: the game stops with "Failed to find content at path". Even a finished conversation keeps its last position. So right before the game saves, after the mods' `BeforeSave` handlers, the framework drops every conversation that stopped inside a mod script and closes the dialogue box if one is open in a mod script. Dropped conversations start from the top next time, as finished ones do anyway. Mods that need to end their conversations in a particular way (the phone hangs up properly, for example) do that in `BeforeSave`.

## Rules

Namespace `NeetModKit.Rules`. Content files (phone calls, emails) share one set of conditions, actions and flags, so an email can set a flag a phone call checks, and a mod that adds an action adds it everywhere. The phone guide (`phone.md`) lists the built-in ones.

### Conditions and actions

```csharp
RuleConditions.Register(this, "stormy", value => { bool want = value.Value<bool>(); return _ => (Sky.Weather.Kind == WeatherKind.Storm) == want; }, "true during a storm");
RuleActions.Register(this, "confetti", args => Confetti.Burst(args.Num("amount", 10)), "amount", "Throws confetti.");
```

- `RuleConditions.Register(owner, name, compile, help)`: `compile` turns the value from the file into a test once, when the file loads, so mistakes show up right away (throw `FormatException`). The test gets the `IRuleSubject` it runs for.
- `RuleActions.Register(owner, name, run, mainValue, help)`: `run` gets `ActionArgs` (`Str`, `Num`, `Int`, `Bool`, `Has`, `Require`, `Values`, `Subject`, `Repeat(action)` for `"count"`). `mainValue` is what a bare value fills in Ink (`Act("deliver", "Pizza")`). `RuleActions.Later(owner, seconds, action)` runs something after a delay.
- `RuleTexts.Register(owner, name, read, help)`: a text value files write as `{{name}}` or `{{name:arg}}` in lines and choices (`read` gets the argument, "" without one, and runs when the line shows). `StepWriter` turns them into `{RuleText("name", "arg")}`; a choice with a value is written in brackets and said as its branch's first line, since Ink would read a leading `{...}` as a condition. Unknown names throw `FormatException` when the file loads. Built in: `money`, `flag`, `weather`.
- `Condition.Parse(json)`, `Condition.Of(func)`, `Test(subject)`: a whole condition object, with `any`, `all` and `not`.
- `ActionList.Parse(json)`, `Run(subject)`: a list of `{ "do": ... }` objects, checked when parsed.
- `Comparison.Parse("< 25")`, `NamedComparisons.Parse({ "name": ">= 2" })`: the number tests conditions use.
- `IRuleSubject` (`Id`, `Times`) is what's running: a phone call, an email. `RuleContext.Active` is the one playing right now, for actions called from hand-written Ink.
- `RuleIds.Find(items, idOf, id, nearMod)`: finds a call, an email, ... by id, where a short id ("pizza") means one from `nearMod` first.

### The player a rule is about

The built-in conditions and actions that are about "the player" (`stat`, `holding`, `room`; `stat`, `knockout`, and where `give` and `fire` happen when there's no speaker) go through `RulePlayer.Current`, an `IRulePlayer`: `Position`, `InFront(distance)`, `GetStat`, `SetStat`, `AddStat`, `Knockout(seconds)`, `Holds(names)`. Use it in your own conditions and actions too, so they work for whoever the rule is about.

Normally it's this game's player (`RulePlayer.Local`). A mod that runs a conversation for someone else adds a source that names that player while it runs; co-op does this for remote players' conversations, which run in the host's game:

```csharp
RulePlayer.AddSource(this, () => talking != null ? talking.Player : null);
```

Sources are asked newest first, every time a rule needs the player, and the first one that doesn't return null wins. `RulePlayer.Require()` throws when there's no player at all (outside the house). `Spawning.PrefabName(name)` strips "(Clone)" for comparing held items with item names.

### Triggers and schedules

For content that happens by itself (a call ringing in, an email arriving):

- `TriggerRules`: `Trigger` (`random`, `order`, `arrive`, `wake`, `manual`), `Weight`, `When`, `Once`, `CooldownMinutes`, `DelaySeconds`, `Store`. `TriggerRules.Parse(json, allowedTriggers, defaults)` reads `true` or `{ "trigger": ..., "weight": ..., ... }` from a file; `IsOn(json)` says whether the field turns it on; `Describe("Rings")` makes a line for menus.
- `Triggers.CanFire(subject, rules, trigger, store, secondsSinceLast)` and `Triggers.Pick(items, rules, canFire, buffer)`: what may fire now, and a weighted pick among them. Only `manual` triggers fire in a game that doesn't run the house (`WorldRole`, below).
- `ScheduleList`: ids waiting for some play time, kept in your save data. `Add(id, seconds)`, `Remove`, `Contains`, and `TakeDue(handle, retrySeconds)` once in a while.

### Flags

`ModFlags`: named numbers in the save, shared by every mod. `Get(name)`, `Set(name, value)` (0 removes it), `Add(name, amount)`, `All`. Also `PlaySeconds` (seconds the house was loaded in this save, across sessions) and `Day` (1 at the start, one more each time the player sleeps and wakes up; the game's own `nightId` never moves).

Console: `flags` lists them, `setFlag <name> <value>` sets one.

### Content files to Ink

`StepWriter.Write(knot, steps, addAction, addCondition, options)` turns a list of steps from a file (lines, choices, if, random, do, label, goto, end, ink) into an Ink knot. Pass `RuleSteps.AddAction`/`AddCondition` as the callbacks: they number each condition and action and the knot calls them as `RuleIf(n)` and `RuleDo(n)`. `StepWriterOptions` sets the lines written at the end (the phone ends calls on `# EndPhoneCall`), extra tagged line types (the phone's `{ "player": ... }`) and whether a picked choice is said as the next line (`SayChoices`; emails turn it off and show the reply themselves). `RuleSteps.Muted` skips actions, for replaying text that was already shown.

Ink functions in every story: `Act(action, values)`, `Check(condition, value)`, `Flag(name)`, `SetFlag(name, value)`, `AddFlag(name, amount)`, plus `RuleIf` and `RuleDo` for generated knots.

## Content packs

`NeetModKit.IO.ContentPack` loads a content folder from every loaded mod, the way the phone loads `phone/` and the email mod `email/`: `.ink` files go into an Ink story, `.jsonc` and `.json` files go to your reader, and it all loads again when a file changes or a mod comes or goes. `JsonFiles` has the rules for content JSON: `Pick(files)` (a `.json` with a `.jsonc` twin is left out) and `Find(folder, name)` (`name.jsonc`, else `name.json`).

```csharp
_pack = ContentPack.Watch(this, "phone", InkStory.Dialogue, CallParser.LoadFile, () => _pack.LogSummary("calls"));
public override void OnAllLoaded() => _pack.Reload();
```

The reader gets a `ContentFile`: `Display` (for messages), `ModId`, `Scope` (register what the file adds there; it's removed before the next load), `Read()`, `Items("calls")` (one object, a list, or `{ "calls": [...] }`), `Problem(message)` and `AddInk(source)` for generated Ink. `Problems()` lists every problem with its file, Ink compile errors included.

Conditions are compiled when a file loads, so every pack loads again when a mod registers or removes a condition or action: an email that checks a phone call's `called` keeps working when the phone mod reloads.

## Store pages

`NeetModKit.Items.StorePages.RefreshPriceLabels()` rewrites the price line of every store button from its item's current `Usable.price`. The game builds the buttons into the scene with the price in their label and maps button i to item i of the store's `pickupBank`; the order confirmation and the charge read the live price anyway, so a mod that changes prices only needs this for the labels. `WithPrice(label, price)` replaces the price in one label.

## Media folders

`NeetModKit.IO.MediaFolder.Read(folder, extensions, listFile, listKey, problems, out list)` reads a `ModContent` folder of media files (Custom TV Stations' videos, Mixtapes' songs): every file of the given types, sub folders included, as `MediaFile`s (`Path`, the real file; `Source`, for messages; `Name`, the file's name without extension as it is in the folder, also for a virtual folder whose files are read from elsewhere; and `Entry`, the file's object from the optional list file). Files the list names come first in its order, the rest follow by file name; list problems (bad JSON, a missing file) go into `problems`. `list` is the whole list file, for anything else in it (the TV's `hide`).

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

`SaveFiles.Newest()` is the newest `neet*` file in `Application.persistentDataPath` by write time, the one the game loads (null when there's none, a new game); `ReadNewest()` reads it as JSON in any save format. Checked in a `BeforeLoad` handler, `Newest() == null` tells a new game from a loaded one.

### Saves in memory: SaveSnapshot, SaveCapture, SaveSource, SaveLock

For handing a house from one game to another without a save file (co-op joins).

```csharp
SaveSnapshot snapshot = SaveCapture.Take();          // main thread, in the house
snapshot.Save["Save"]                                // the AllSaveDataToJSon object: edit it before using it
snapshot.Paint                                       // PaintTarget.saveId -> PNG
byte[] blob = snapshot.Encode();                     // GZip of minified JSON + paint
var copy = SaveSnapshot.Decode(blob);

SaveSource.Use(copy);                                // the next LoadAll reads this instead of disk
SaveSource.Loaded += error => { };                   // LoadAll is done (null) or threw
SaveSource.Clear();
using (SaveLock.Acquire(this, "joined a co-op game")) { }   // no SaveAll, no "reset save"
```

- `SaveCapture.Take()` writes no file and runs none of the save hooks (`BeforeSave`, the dialogue guard), so it's safe while the player is mid-call. Mod data is in the save object as usual. Paint comes from the surface's current texture, not the one the game's own save reads.
- While a snapshot is in use, `LoadAll` sees one marker file (`neet-join.marker` in the framework's folder) instead of the save folder, the save text, mod data and `SaveFiles.Newest()/ReadNewest()` come from the snapshot, and surfaces are painted from `Paint`. The player's own saves are not listed or read. The snapshot is cleared when `LoadAll` ends, so it applies to one load.
- It only works when `LoadAll` runs, which is when the house scene starts. To load one, set `SceneLoadingData.sceneNameToLoad = "NEET_House"` and load `Loading`; don't call `LoadAll` again inside a running house.
- A framework reload while a snapshot waits keeps it.
- `SaveLock` is ref-counted and released when the owner unloads. Scene changes the game does around saving (sleeping, dying) still happen; only the writing stops.

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
public static void Register(ITracker owner, ItemDefinition def);   // removed when owner cleans up (a mod, or a Scope)
public static bool IsModded(string name);
public static GameObject Template(string name);   // the template copies are made from, once built
public static IEnumerable<string> Names { get; }
```

`Register` throws if `Name` or `BasedOn` is empty, or if a modded item with that name is already registered. The item is removed when the owner cleans up: the mod unloading, or a `Scope` for items made from files, which is disposed before the files load again (Mixtapes does this).

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

This adds a section to the mod's entry on the mod menu's Mod Settings tab (its SETTINGS view). The page builds one control per public instance field of the config class. It writes the config a moment after the last edit.

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
- `[Shared]` (`SharedAttribute`): the setting shapes the shared house; in co-op the host's value counts and clients see it read-only (see "Who decides the mod setup").

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

`Scope` is a throwaway owner for things registered together, so they can be removed together and registered again. Pass it as the owner, dispose it to clean up. A scope created with a parent cleans up with the parent too.

```csharp
_scope?.Dispose();
_scope = new Scope(this);                 // also cleaned up when the mod unloads
InkScripts.Add(_scope, "calls.ink", source);
```

## Files

`FileWatch` (namespace `NeetModKit.IO`) calls back when files in a set of folders change:

```csharp
FileWatch.Start(this, () => new[] { PathTo("data") }, Reload, 1f, "*.json");
```

The folder list is asked for on every check, so it can change. It polls (once a second by default) and compares names, sizes and write times; file system events aren't reliable under Proton.

`ModContent` is for a folder any mod can ship for another mod to read, like `phone` (Phone Expansion's call files) or `tv` (Custom TV Stations' videos): `Mods/<any mod>/<name>/`.

```csharp
_videos = ModContent.Watch(this, "tv", LoadVideos, "*");   // in OnLoad
public override void OnAllLoaded() => _videos.Load();     // every mod's folder is there now

void LoadVideos()
{
    foreach (var folder in _videos.Folders())                // loaded mods that have the folder
        foreach (string file in Directory.GetFiles(folder.Path))
            Log.Info(folder.Display(file));                  // "SomeMod/tv/clip.mp4"
}
```

The load callback runs again on the next frame when a file in any of the folders changes, or a mod that has the folder loads or unloads, so a burst of changes loads once. `MarkDirty()` asks for that too. Watching starts after the first `Load()`.

Another mod can show other folders in place of every mod's own folders of a name: `ModContent.Substitute(owner, "tv", folders)` until it's called again with null or the owner unloads. Co-op uses it so every player sees the session's TV stations and tapes. A stand-in folder is usually virtual: `new ModContent.Folder(mod, path, files)` with a map from each file's path inside the folder to where the file really is (`folder.IsVirtual`, `folder.Files`, `folder.RealPath(file)`). Readers that list files themselves should go through `Files` for a virtual folder; `MediaFolder` does that already. A file of a stand-in folder may not be there yet: when it arrives, the stand-in's owner calls `ModContent.ReportArrived(realPath)` and readers that listen to `ModContent.FileArrived` can start using it (play the station, load the song) without loading the whole folder again.

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

`SceneEdits` (namespace `NeetModKit.Scenes`) makes reversible scene changes: `Hide(owner, gameObject)` deactivates an object until the owner (a mod or feature) cleans up, counting owners so an object only comes back when nobody wants it hidden; `RootsNamed(prefix)` finds scene root objects by name prefix. The Open House mod uses both to remove the demo's walls.

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
| `SaveLoadHandler.SaveAll` (pre/post) | `LifecyclePatches` | `BeforeSave`, `AfterSave`; drops conversations that stopped in mod scripts (after `BeforeSave`) |
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
| `Dialogue.Awake` (post) | `DialoguePatches` | Raises `InkStory.Dialogue.Created`, so mod scripts and functions are in the story before a save loads into it |
| `EmailHandler.Start` (post) | `DialoguePatches` | Raises `InkStory.Email.Created` once the email story is built |
| `Computer.OrderItem` (post) | `OrderPatch` | Raises `ItemOrdered` for orders that went through |
| `HouseManager.Start` (post), `Brain.OnTriggerEnter` (post) | `SkyPatches` | Finds the scene's sky; applies the storm volume right after the game sets it for a room |
| `HouseManager.Thunder` (transpiler, the loop's `MoveNext`) | `ThunderWaitPatch` | The wait between strikes comes from `Sky.SetThunderDelay` and can be looked at and moved |
| `EmailApp.LoadEmail` (pre, last) | `EmailGuardPatch` | An email whose knot is gone (its mod was removed) shows a note instead of throwing |

### Reasons for the less obvious ones

- `SaveLoadHandler.Start` raises `HouseReady` on the next update rather than immediately, so every other `Start` has finished when mods run.
- The store patches run after the store's own `Start`, because the store wires its buttons by index and must not see the framework's buttons.
- The card patches run before the game collects its cards, because the game collects them once.
- Mod scripts go into the story in `Dialogue.Awake`, not `Start`: the save loads the story's state in `SaveLoadHandler.Start`, and a state that mentions a mod knot must find it there. The game binds its own functions in `Start`, so mod functions (which use other names) don't collide with them.

## Hot reload

Hot reload runs an unload and a load for a mod. The framework keeps these things across it.

- Mod save data: the data at unload goes to `ReloadState` and is loaded by the new copy's `ModSaveData.Register`.
- Store state: `StoreListings` records which stores have started and restores that after reload, because Unity has no "Start already ran" flag.
- Features: each feature's patches and tracked objects are removed on unload and re-enabled by the new copy's `ModFeatures.Register`, using the saved choice.
- `ReloadState` is owned by the loader and is never reloaded, so values put there survive a reload of the framework itself. Store plain data (strings, lists of ids), not objects from the reloading assembly.
