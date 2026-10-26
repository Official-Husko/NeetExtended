# Computer apps

The in-game computer has a browser. Each website the browser can show is an app. NeetModKit adopts the game's own websites as built-in apps, lets the player change any app's address, and lets mods add their own apps and games. This page covers how the browser works, what the framework changed, the `ComputerApps` API, the built-in apps, the helpers for working on the computer screen, and a worked example (Pumpkin Smash).

Namespaces:

- `NeetModKit.Desktop`: `ComputerApps`, `ComputerApp`, `ComputerAppDefinition`, `ComputerAppContext`, `ComputerControl`, `ComputerUi`
- `NeetModKit.Ui`: `UiCloning`
- `NeetModKit.Ui.Toolkit`: `UiBuilder`, `UiThemes`, `UiTemplate`

For the UI toolkit used on app pages, see [ui-and-menu.md](ui-and-menu.md).

## How the browser works in the game

The browser is `Computer_Browser_Addresser`. Its address bar is an `InputField`. Two lists drive it:

- `urls`: the names the browser knows, such as `metscape` or `anime`. The address is matched against these bare names.
- `websites[]`: the page for each name. `urls[i]` shows `websites[i]`, so the two lists must line up.

`AddressWrapper` hides every page, shows the one whose name is typed, and writes the address back as `http://<name>.com`. The home page is `metscape`. The game's start menu entries open the browser on these names.

In the game, typing `http://metscape.com` matches nothing, because matching happens before the address is normalized. The framework normalizes the typed text first, so both forms work in the browser.

## What the framework changed

- **Routing.** `BrowserPatches` is a Harmony prefix on `AddressWrapper`. It calls `ComputerApps.Navigate` and skips the game's own routing. Navigation looks the typed name up in the list of apps (below), so the player can change an app's address and mods can add pages.
- **Built-in apps.** The first time the browser is used in a house, and at each house load, `ComputerApps` makes a `ComputerApp` for each of the game's `websites[]` entries. The id is the page object's name.
- **Aliases.** An app keeps its original address working after the player changes it. The start menu uses the original addresses, so it still finds every app.
- **Mod apps.** `ComputerApps.Register` adds an app. Its page is built on each house load, and it can get a start menu entry.
- **PC Apps tab.** The mod menu's PC Apps tab lists every app, with its address field and `SAVE` and `RESET` buttons.

## Concepts

| Term | Meaning |
|---|---|
| App | A `ComputerApp`: a page on the computer with an id, title, description and address. |
| Id | The stable key. Built-in apps use the game's page names (`AnimeClicker`). Mod apps choose their own. Ids are compared without case. Ids key the player's address choice, so they must not change between versions. |
| `DefaultUrl` | The address the app shipped with. It always works as an alias. |
| `Url` | The address in use: the player's choice, or `DefaultUrl`. |
| Built-in | An app adopted from the game. `BuiltIn` is true and `Owner` is null. |

An address is one to 32 lowercase letters, digits or dashes, without `http://`, `www.` or `.com`. The browser shows `http://<address>.com`.

## `ComputerApps`

```csharp
public static IReadOnlyList<ComputerApp> All { get; }
public static ComputerApp Find(string id)
public static ComputerApp Register(NeetMod owner, ComputerAppDefinition def)
public static string UrlOf(ComputerApp app)
public static string SetUrl(ComputerApp app, string url)
public static void Open(ComputerApp app)
```

`All` lists every app in display order: built-in apps first, in the game's order, then mod apps in the order they were registered.

`Find(id)` returns the app with that id, compared without case, or null.

### `Register`

```csharp
ComputerApps.Register(this, new ComputerAppDefinition { ... });
```

- Throws `ArgumentException` if `Id` is empty, `Build` is null, or an app with that id already exists (built-in or mod).
- `Url` defaults to `Id` when not set, and is normalized (see below). The address is not checked against other apps when registering. If two apps end up with the same address, the one earlier in `All` wins, and built-in apps come first.
- If the house is already set up, the app is built straight away. Otherwise it is built when the house is set up.
- The app is removed when `owner` unloads: it leaves `All`, its page is destroyed and its start menu entry is removed.
- Returns the `ComputerApp`.

### `UrlOf` and `SetUrl`

`UrlOf(app)` returns the player's saved address for the app, or `DefaultUrl`.

`SetUrl(app, url)` changes the address and returns null on success, or the reason it was refused:

1. The text is normalized. Refused if it is not one to 32 lowercase letters, digits or dashes: "Use 1-32 letters, digits or dashes."
2. Refused if another app's current address or `DefaultUrl` is the same: "`<Title>` already uses "`<address>`"."
3. Setting an app's own `DefaultUrl` removes the saved choice. Otherwise the address is saved.

The saved choices are stored in `Mods/NeetModKit/app-urls.json` (the framework's folder). `SetUrl` writes that file at once.

Because of rule 2, a player cannot take the address that another app shipped with, even if that app has moved. Mods should not rely on taking addresses of built-in apps.

### `Open`

```csharp
ComputerApps.Open(app);
```

Turns on the computer's parent objects up to the screen and loads the app's current address in the browser. Throws `InvalidOperationException` if the computer is not loaded. It does not turn on the computer; use `ComputerControl.PowerOn` for that (see below).

### `Normalize` (internal)

`ComputerApps.Normalize` is internal, so mods cannot call it, but its rules decide what the player and mods can type:

- Trims, lowercases.
- Removes a leading `http://`, `https://` or `www.`.
- Removes trailing `/`, and a trailing `.com`.

`"HTTP://www.Anime.com/"` becomes `anime`.

When the player types in the browser, `Navigate` matches the typed address against, in order: the current address of each app, then its `DefaultUrl`, then its id (without case). If nothing matches, every page is hidden and the address bar shows what was typed.

## `ComputerAppDefinition`

| Field | Type | Default | Meaning |
|---|---|---|---|
| `Id` | `string` | | Required. Stable and unique across apps. |
| `Title` | `string` | `Id` | Shown in the PC Apps tab and the start menu, unless `StartMenuLabel` is set. |
| `Description` | `string` | | Shown in the PC Apps tab. |
| `Url` | `string` | `Id` | Default address, without `http://` and `.com`. |
| `Height` | `float` | `120` | Page height in computer units. The screen shows about 120 at a time. The framework does not scroll the page; content that does not fit is cut off unless the `Build` callback adds a `Scroll` area. |
| `InStartMenu` | `bool` | `true` | Adds an entry to the start menu. |
| `StartMenuLabel` | `string` | | Text of the start menu entry. Uses `Title` if empty. |
| `Build` | `Action<ComputerAppContext>` | | Required. Builds the page. |

## The `Build` callback and `ComputerAppContext`

`Build` runs every time the house's computer is set up, which is each house load. It is also run once straight away when a mod registers an app while the house is ready.

```csharp
public sealed class ComputerAppContext
{
    public ComputerApp App { get; }
    public RectTransform Panel { get; }   // the page, background included
    public UiBuilder Ui { get; }          // a vertical builder on the page's content, in the computer's style
}
```

What the framework does around `Build`:

- It creates the page, inactive, the same width as the game's own pages and `Height` tall, with the computer's backdrop behind it.
- It makes a `UiBuilder` with `UiThemes.Computer`, on a content area inset by 6 units, with spacing 2.
- It shows the page when the browser navigates to the app, and hides it when the browser goes elsewhere.
- It destroys the page when the mod unloads.

Because `Build` runs on each house load, the objects from one house are gone by the next. Do not keep references to the previous page's controls, and do not keep state only in them. Keep game-independent state in the mod (see [Pumpkin Smash](#worked-example-pumpkin-smash)).

Use `ctx.Ui` for the page's elements. The computer's sizes are small (`UiMetrics.Computer`): buttons are 12 units tall and 40 wide by default, and text is small. Give `Height` enough room for the page's content, or put longer content in a scroll area:

```csharp
Build = ctx =>
{
    var list = ctx.Ui.Scroll(out _);
    foreach (var item in items)
        list.Text(item.Name);
}
```

## Start menu entries

With `InStartMenu` true, `Register` adds an entry to the computer's start menu. The entry copies the game's own start menu button (`Btn_AnimeClicker`). The start menu grows by one row per entry, and new entries go below the game's `Btn_Stocks`. When clicked, the entry runs the game's button command (which closes the start menu), then calls `ComputerApps.Open` on the app.

Each house load starts the start menu again and adds the mod's entries. The entries are removed when the mod unloads.

## Built-in apps

The framework does not set these addresses. They are the game's own `urls`, read at runtime. The ids are the game's page names, which are the keys of `BuiltInApps.Known`. The table pairs each address with its page by the game's list order (`urls[i]` with `websites[i]`).

| Id (page name) | Title | Default address | Description |
|---|---|---|---|
| `Home` | Metscape | `metscape` | The browser's home page |
| `PunStore` | Pun Store | `pun` | The normal online store |
| `BlackMarket` | Costello's | `costellos` | The black market |
| `OccultMarket` | Katacum | `katacum` | The occult market |
| `AnimeClicker` | Anime Clicker | `anime` | The anime card idle game |
| `Downzone` | Downzone | `down` | Buys and installs computer apps |
| `BuddyPortal` | Buddy Portal | `buddy` | The Pig Buddy site |
| `KustomSHT` | Kustom SHT | `kustom` | Items printed with your own pictures |
| `STRONK` | STRONK | `stonk` | The stock market |
| `CUM` | CUM | `cum` | A site from the original game |
| `LuckyCassette` | Lucky Cassette | `cassette` | Cassettes with your own music |

Built-in apps have no `Build` and no owner. A mod cannot unregister them. A page that is not in `BuiltInApps.Known` gets its panel name as its title and "A site from the original game." as its description.

Built-in apps have no start menu entry from the framework, because the game's own start menu entries already exist for them.

## `ComputerControl`

Helpers for testing and for code that acts on the computer. Most mods only need `OpenSite` and `Site`.

```csharp
public static Computer Main { get; }
public static Transform Screen { get; }
public static Computer_Browser_Addresser Browser { get; }
public static List<string> SiteUrls { get; }
public static void PowerOn()
public static void WakeScreen()
public static void OpenSite(string urlOrId)
public static GameObject Site(string id)
public static void StandInFront(float distance = 0.7f)
```

| Member | What it does |
|---|---|
| `Main`, `Screen`, `Browser` | The computer, its screen transform (the world-space canvas), and the browser. All null outside the house. |
| `SiteUrls` | The current address of every app. |
| `PowerOn()` | Turns the computer on with no boot animation, then wakes the screen. Throws `InvalidOperationException` if there is no power button (not in the house). |
| `WakeScreen()` | Hides the screensaver and restarts its 30-second countdown. |
| `OpenSite(urlOrId)` | Opens an app by its current address, its original address or its id. Throws `ArgumentException` if nothing matches. Wakes the screen. |
| `Site(id)` | The app's page object, or null. Use it to find elements on a page. |
| `StandInFront(distance)` | Teleports the player in front of the monitor, facing it. Only useful for tests. |

Buttons on the computer only work while the player is at the desk (the computer's canvas is not interactable from elsewhere). For a test, call `StandInFront()` first, or click the button in code.

## `ComputerUi`

Copies game elements onto a website, so new buttons and labels match the site they are on. Everything created is destroyed when the mod unloads.

```csharp
public static Button CloneButton(ITracker owner, Transform template, string name, string label, Action onClick)
public static TMP_Text CloneLabel(ITracker owner, Transform template, string name, string text)
public static void SetText(Transform target, string text)
public static void Place(Component element, Vector2 anchoredPosition, Vector2? size = null)
public static void PushTopEdge(ITracker owner, RectTransform rect, float pixels)
public static IDisposable PushTopEdge(RectTransform rect, float pixels)
```

- `CloneButton` copies `template` (which must be a button), sets its label and click action, and gives it the computer's click sound.
- `CloneLabel` copies `template` (which must have a TextMeshPro text) and sets its text.
- `SetText` and `Place` are shortcuts for `UiCloning`.
- `PushTopEdge` moves the top edge of a stretched element down by `pixels` (positive) to make room. The owner form undoes it when the owner unloads; the other form returns an `IDisposable` that undoes it when disposed.

`ITracker` is the owner a copy is tracked on (a `NeetMod`, or a feature, or any object implementing `ITracker`).

### Example: buttons on the Anime Clicker site

`mods/CardExpansion/UI/CardSiteUi.cs` adds a row of buttons under the Anime Clicker site's header, with a status line. The key parts:

```csharp
private const string SiteApp = "AnimeClicker";   // by id, so it works whatever address the player chose

// Locate the site's elements (page object children of the game's site)
var site = ComputerApps.Find(SiteApp)?.Panel;
_redeem = site.transform.Find("RedeemButton");
_meter = site.transform.Find("CrystalMeter");
_cards = site.transform.Find("Cards") as RectTransform;

// Make room: move the card grid's top edge down
_rowSpace = ComputerUi.PushTopEdge(_cards, RowHeight);

// Status line: copy the crystal meter's label
_status = ComputerUi.CloneLabel(_mod, _meter, "CardExpansion_Status", "");

// A button: copy the Redeem button, place it in a slot
var button = ComputerUi.CloneButton(owner, _redeem, "CardExpansion_" + name, "", onClick);
ComputerUi.Place(button, new Vector2(FirstX + slot * SlotWidth, RowY), ButtonSize);
```

`SummonFeature` calls `AddButton` from `GameEvents.WhenHouseReady`, so the button is added again on every house load. The row is removed when its last button is removed, which is handled by the tracked disposals.

## Worked example: Pumpkin Smash

`mods/PumpkinSmash` is a complete game added through the apps API. It has four parts:

| File | Role |
|---|---|
| `Game/SmashRound.cs` | The rules. No UI. Uses only `Mathf` and `Random` from Unity. |
| `UI/SmashBoard.cs` | The app page. Builds the page and shows the round. |
| `PumpkinSmashMod.cs` | The mod. Registers the app, runs the round, pays out, keeps the best score. |
| `PumpkinSmashConfig.cs` | Settings. |

### 1. The rules (`SmashRound`)

The game logic has no UI. A pumpkin pops up in one of nine holes and stays for a while; hitting it scores a point and pops the next one, and hitting an empty hole costs a point (never below 0). The time a pumpkin stays up shortens as the score rises.

```csharp
public sealed class SmashRound
{
    public const int Holes = 9;

    public bool Running { get; }
    public int Score { get; }
    public float TimeLeft { get; }
    public int Pumpkin { get; }          // hole with the pumpkin, or -1

    public event Action Changed;         // after every change
    public event Action<int> Finished;   // when the timer runs out, with the score

    public void Start(float seconds);    // resets the score, starts the timer
    public void Tick(float dt);          // advances time and pumpkins
    public void Hit(int hole);           // a hole was clicked
}
```

Keeping the rules separate means the mod can be checked without a game scene, and the UI only reads the state and calls `Hit`.

### 2. The page (`SmashBoard`)

`Build` runs on every house load. It is the `Build` callback, so the page and its controls are new each time.

```csharp
public void Build(ComputerAppContext ctx)
{
    Page = ctx.Panel.gameObject;      // used to know whether the page is on screen
    var ui = ctx.Ui;
    ui.Header("PUMPKIN SMASH");
    _score = ui.Text("");

    var grid = ui.Grid(46f, 16f, 3f);
    for (int i = 0; i < _holes.Length; i++)
    {
        int hole = i;
        _holes[i] = grid.Button("", () => _round.Hit(hole), -1f);
    }

    _start = ui.Button("START", StartRound, 60f);
    Refresh();
}
```

The page shows three things:

- A score line. While the round runs it shows the score and the time left. Otherwise it shows the best score and the number of rounds, from the mod's save data.
- Nine hole buttons. The lit hole is orange and reads `SMASH!`; the others are dark.
- A START button, shown only when no round is running.

`Refresh` updates these. It is connected to the round's `Changed` event, so the page redraws each time something happens.

The page holds references to its buttons, which are destroyed when the page is rebuilt. `Refresh` checks each reference is still there before using it.

### 3. The mod (`PumpkinSmashMod`)

```csharp
public override void OnLoad()
{
    Instance = this;
    Config = LoadConfig<PumpkinSmashConfig>();
    Config.Save();
    ModSettings.Register(this, Config, "Pumpkin Smash");
    ModSaveData.Register(this, () => Best, record => Best = record);

    _board = new SmashBoard(_round);
    _round.Changed += _board.Refresh;
    _round.Finished += EndRound;

    ComputerApps.Register(this, new ComputerAppDefinition
    {
        Id = "PumpkinSmash",
        Title = "Pumpkin Smash",
        Description = "Whack-a-pumpkin. Smash as many as you can before time runs out.",
        Url = "pumpkin",
        Height = 120f,
        StartMenuLabel = "Smash",
        Build = _board.Build,
    });
}
```

Points to note:

- The app is registered in `OnLoad`, and the framework builds its page on each house load through `Build`.
- The `Url` is `pumpkin`, so the page opens at `http://pumpkin.com`. The player can change it on the PC Apps tab, and the start menu still finds it, because the start menu entry opens the app by its object, not its address.
- `StartMenuLabel` is shorter than the title, so the start menu entry fits.
- Settings are registered with `ModSettings` (see [ui-and-menu.md](ui-and-menu.md#settings-pages)).

### 4. Ticking only while the page is visible

```csharp
public override void OnUpdate()
{
    // Only play while the page is on screen; leaving the site pauses the round.
    if (_board.Page != null && _board.Page.activeInHierarchy)
        _round.Tick(Time.deltaTime);
}
```

The round does not advance while the browser shows another site. It is not reset: the timer is still where it was when the player returns. `Page` is null before the page is built and after the house is unloaded; the check handles both.

When the round ends, `EndRound` runs:

```csharp
private void EndRound(int score)
{
    Best.Rounds++;
    if (score > Best.BestScore)
        Best.BestScore = score;

    float cash = score * Config.Value.CashPerPumpkin;
    if (cash > 0f && GameRefs.Computer != null)
        Money.Add(cash);
    Log.Info($"Round over: {score} pumpkins, ${cash:0.00}");
    _board.Refresh();
}
```

`Money.Add` pays the game's money, taxed like other income.

### 5. Save data

```csharp
public sealed class SmashRecord
{
    public int BestScore;
    public int Rounds;
}
```

`ModSaveData.Register(this, () => Best, record => Best = record)` keeps the record in the game's save file, under the mod's id (`PumpkinSmash`, the default key). Its contents are written when the game saves and read back when the game loads. If the save has nothing for the mod (a new game, or the mod was just installed), the load callback gets a new `SmashRecord` with zeroes. Hot reloads hand the record to the new copy of the mod, so the score is not lost.

### 6. Settings

```csharp
public sealed class PumpkinSmashConfig
{
    [Label("Round length (seconds)")]
    [Range(10, 120)]
    public int RoundSeconds = 30;

    [Label("Cash per pumpkin")]
    [Description("Paid at the end of a round, taxed like other income.")]
    [Range(0f, 2f)]
    public float CashPerPumpkin = 0.25f;
}
```

`RoundSeconds` is read when a round starts (`StartRound`). `CashPerPumpkin` is read when a round ends.

### Adding a game like this

1. Put the rules in a class with no UI (events for changes and the end of a round).
2. Build the page in a `Build(ComputerAppContext)` method. Keep a reference to the page object to check whether it is on screen.
3. Register the app in `OnLoad` with a unique `Id`, a `Url`, and a `Build` callback.
4. Tick the rules in `OnUpdate`, only while the page is active.
5. Keep anything the player should keep in `ModSaveData`, and settings in a config registered with `ModSettings`.
6. To test in a running game, `ComputerControl.PowerOn()`, `StandInFront()` and `OpenSite("pumpkin")` put the computer on the page.
