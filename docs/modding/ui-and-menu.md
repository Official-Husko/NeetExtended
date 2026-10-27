# UI and the mod menu

NeetModKit builds its UI from copies of the game's own uGUI elements. Nothing is drawn from scratch: a button in the mod menu is a clone of the pause menu's violet button, and a button on the in-game computer is a clone of a button on one of the game's websites. This page covers the themes that supply those copies, the `UiBuilder` API that lays them out, the mod menu and how mods add pages to it, settings pages, and the palette.

Namespaces used below:

- `NeetModKit.Ui.Toolkit`: `UiBuilder`, `UiTheme`, `UiThemes`, `UiTemplate`, `UiMetrics`, `UiScrollbarStyle`, `UiWindow`, `UiCanvas`
- `NeetModKit.Ui`: `UiCloning`
- `NeetModKit.Menu`: `ModMenu`, `MenuPage`, `MenuPalette`
- `NeetModKit.Settings`: `ModSettings`, `RangeAttribute`, `LabelAttribute`, `HiddenAttribute`

For the in-game computer (apps, sites, start menu), see [computer-apps.md](computer-apps.md).

## Themes and templates

A theme is a set of game elements to copy from. A template is a slot in a theme: one kind of element the builder can ask for, such as `UiTemplate.Button`.

On first use, `UiTheme.Ensure()` finds each source element by path under the theme's source object, instantiates a copy into a hidden holder that lives across scenes, and strips what would make the copy behave like the original (`EventTrigger`s, click/change listeners, and the text in input fields). Missing elements are logged as warnings and the template is skipped. A template a theme does not have is taken from its `Fallback`.

`UiThemes` defines two themes.

### `UiThemes.Menu`

Source: the pause menu's settings canvas (`Feedback_Sender._`, `Canvas_Settings/`). Used by the mod menu, the pause menu's MODS column and any window built with `UiWindow`.

| Template | Copied from (under `Canvas_Settings/`) | Looks like |
|---|---|---|
| `Backdrop` | `Image` | Dark purple full-screen background |
| `Header` | `Controls/Display_Text (1)` | Big orange display-font title ("CONTROLS") |
| `SubHeader` | `Display/Resolution_Text` | Small white caps label ("RESOLUTION") |
| `Text` | `DataDisclaimer` | Plain body text |
| `Button` | `Controls/ChangeControlsButton` | Violet button ("CHANGE") |
| `AccentButton` | `Quit Button` | Orange button ("GET OUT") |
| `Toggle` | `Bloom_Toggle` | Checkbox |
| `Slider` | `Brightness_Slider` | Slider |
| `Input` | `CommentInput` | Single-line text box |
| `Scrollbar` | `Quality_Dropdown/Template/Scrollbar` | Scrollbar, recoloured by the theme's `ScrollbarStyle` (below) |

The dropdown's own scrollbar is white, so the menu theme sets `ScrollbarStyle = UiScrollbarStyle.Menu`: the track and the handle both use the game's rounded `UISprite`, the track dark and see-through, the handle orange like the sliders and the selected tab, lighter when hovered and violet while dragged. `UiScrollbarStyle` has `Track`, `Handle`, `HandleHover` and `HandlePressed` colours; a theme with no style keeps the template's look (the computer theme does).

### `UiThemes.Computer`

Source: the in-game computer (`GameRefs.Computer`), using the elements of the game's own websites under `Browser/Scroll View (Site Container)/Viewport (Site Container)/Content (Site Container)/`. Used for app pages and anything added to a site.

| Template | Copied from (site, then element) |
|---|---|
| `Backdrop` | `LuckyCassette/Background` |
| `Header` | `LuckyCassette/Title` |
| `SubHeader` | `LuckyCassette/Static Track Header` |
| `Text` | `LuckyCassette/Slogan` |
| `Button` | `AnimeClicker/RedeemButton` |
| `AccentButton` | `AnimeClicker/RedeemButton` |

`Toggle`, `Slider`, `Input` and `Scrollbar` are not defined, so the computer theme falls back to `UiThemes.Menu` for them. The `Fallback` link is set in `UiThemes`; `Metrics` is `UiMetrics.Computer`.

`UiThemes.MenuScaler` is the pause menu's `CanvasScaler`. `UiCanvas.Create` copies its settings so overlay windows scale like the game's menus.

### `UiMetrics`

Default sizes, one set per theme. The computer screen is only about 174 units wide, so its controls need much smaller numbers.

| Field | `Menu` | `Computer` | Used for |
|---|---|---|---|
| `ButtonHeight` | 26 | 12 | Height of every button |
| `RowHeight` | 22 | 10 | Height of toggles, sliders and row labels |
| `ButtonWidth` | 140 | 40 | Width of a button given width `0` |
| `LabelWidth` | 190 | 50 | Caption column in `Slider` and `LabeledInput` |
| `ReadoutWidth` | 60 | 18 | Value readout beside a slider |
| `Spacing` | 6 | 2 | Gap used by sliders, inputs and app pages |
| `ScrollbarWidth` | 10 | 3 | Width of scrollbars |

A builder takes its sizes from `Theme.Metrics`. A theme's `Metrics` defaults to `UiMetrics.Menu`.

### Custom themes

`UiTheme` is public, so a mod can build its own theme from another scene object by passing a `Func<Transform>` that returns the source and a dictionary of template to path. This is not used by the framework's own code and is not covered further here.

## `UiBuilder`

`UiBuilder` adds controls to a `RectTransform` top to bottom (a column) or left to right (a row). Each control is a copy of a template. Methods that create a container return a new `UiBuilder` for that container.

### Construction

```csharp
public UiBuilder(RectTransform root, UiTheme theme = null, bool horizontal = false)
public static UiBuilder Vertical(RectTransform root, float spacing = 6f, RectOffset padding = null, UiTheme theme = null)
```

- The constructor wraps an existing transform and adds no layout. `theme` defaults to `UiThemes.Menu`.
- `Vertical` adds a `VerticalLayoutGroup` to `root` unless it already has a horizontal or vertical layout group, then returns a builder for it. Use it to turn an object you created into a column.

Properties: `Root` (the `RectTransform`), `Horizontal`, `Theme`.

### Containers

```csharp
public UiBuilder Row(float spacing = 8f, float height = -1f)
public UiBuilder Column(float width = -1f, float flexibleWidth = 0f, float flexibleHeight = 0f, float spacing = 6f)
public UiBuilder Panel(Color? tint = null, float flexibleHeight = 0f, int padding = 8)
public UiBuilder Grid(float cellWidth, float cellHeight, float spacing = 6f)
public UiBuilder Scroll(out ScrollRect scrollRect, float flexibleHeight = 1f, float spacing = 6f)
```

| Method | What it makes |
|---|---|
| `Row` | A horizontal `HorizontalLayoutGroup`, children middle-aligned vertically. It takes the full width. A `height` of 0 or more fixes its height; negative leaves the height to the content. |
| `Column` | A vertical group. `width` of 0 or more fixes its width. Negative with no `flexibleWidth` makes it take the remaining width. `flexibleHeight` lets it grow. Children are upper-left aligned. |
| `Panel` | A vertical group with a background image (`tint`, default white at 5% alpha) and `padding` on all sides. It takes the full width. |
| `Grid` | A `GridLayoutGroup` of `cellWidth` x `cellHeight` cells, in as many columns as fit the width. Full width. |
| `Scroll` | A vertically scrolling area that takes the remaining height. It returns a vertical builder for the content and sets `scrollRect` to the `ScrollRect`. The scrollbar is a copy of the theme's `Scrollbar`, shown only when the content overflows. Mouse wheel works over the whole area. |

### Plain elements

```csharp
public void Space(float pixels = 8f)
public void FlexibleSpace()
public void Clear()
public static LayoutElement Size(Component element, float width = -1f, float height = -1f,
    float flexibleWidth = -1f, float flexibleHeight = -1f)
```

- `Space` adds an empty gap. In a row it is `pixels` wide; in a column, `pixels` tall.
- `FlexibleSpace` adds a gap that takes all the remaining room, pushing later elements to the far end. Same direction rules as `Space`.
- `Clear` removes every child of `Root`. Use it to rebuild a builder's content. Call it only on a builder whose children you own.
- `Size` sets a `LayoutElement` on any component, creating one if needed. Each argument is ignored when negative. `width` and `height` set both the preferred and minimum size. `flexibleWidth` and `flexibleHeight` set how much of the spare room the element takes. It returns the `LayoutElement`, which you can change further.

### Controls

```csharp
public Text Header(string text)
public Text SubHeader(string text)
public Text Text(string text, Color? color = null, int size = 0)
public Button Button(string label, Action onClick, float width = 0f, bool accent = false)
public Toggle Toggle(string label, bool value, Action<bool> onChange)
public Slider Slider(string label, float value, float min, float max, Action<float> onChange, bool wholeNumbers = false)
public InputField Input(string value, Action<string> onChange, string placeholder = null, float width = -1f)
public InputField LabeledInput(string label, string value, Action<string> onChange)
```

| Method | What it makes | Notes |
|---|---|---|
| `Header` | `Header` template | Wraps. Orange display font. |
| `SubHeader` | `SubHeader` template | Caps label. Used for captions. |
| `Text` | `Text` template | Wraps. Rich text is on, so `<color=...>` tags work. `color` and `size` override the template's colour and font size when given. |
| `Button` | `Button` or `AccentButton` | Height is the theme's `ButtonHeight`. `width` 0 uses the theme's `ButtonWidth`; negative takes the free width of the row or column. Label text shrinks to fit. Returns the `UnityEngine.UI.Button`. |
| `Toggle` | `Toggle` template | The label sits on the right of the box. Fires `onChange` on each change. The value is set without firing. |
| `Slider` | `SubHeader` caption, `Slider` template and a `Text` readout | Creates its own row: caption (`LabelWidth`), slider (takes the free width), readout (`ReadoutWidth`). Whole numbers when `wholeNumbers` is true. The readout updates as the value changes. |
| `Input` | `Input` template | Single line. `onChange` fires on every edit. `placeholder` is the hint text. `width` negative takes the free width. |
| `LabeledInput` | Row of `SubHeader` (`LabelWidth`) and `Input` | A caption and a text box on one row. |

The input's initial text is set without firing `onChange`, so building a page does not trigger it.

### Row vs column

| | Row (`Horizontal == true`) | Column |
|---|---|---|
| Direction | Left to right | Top to bottom |
| Alignment | Middle vertically | Upper left |
| `Space(n)` | `n` wide | `n` tall |
| `FlexibleSpace()` | Pushes later items right | Pushes later items down |
| `Button(w: -1)` | Fills free width | Fills free width |
| `Text`, `Header`, `Panel` | Take the free width | Take the full width |
| `Toggle` | Fixed width (box, label, gap) | Takes the full width |

`Row`, `Column`, `Panel`, `Grid` and `Scroll` each return a builder that the caller can keep using. The horizontal flag belongs to rows only, so a `Space` created from a column builder is vertical.

### Example

```csharp
var ui = UiBuilder.Vertical(frame, 8f);          // column
ui.Header("OPTIONS");
var bar = ui.Row(6f, 26f);                       // 26-unit tall row
bar.Button("SAVE", Save, 120f, accent: true);
bar.FlexibleSpace();
bar.Button("CLOSE", Close, 110f);

var list = ui.Scroll(out _);                     // scroll area that fills the rest
list.Toggle("Show hints", hints, on => hints = on);
list.Slider("VOLUME", volume, 0f, 1f, v => volume = v);
```

## `UiWindow`

A full-screen window in the pause menu's style: a dimmed backdrop, a title with a `CLOSE` button, a toolbar and a content area.

```csharp
public UiWindow(ITracker owner, string title, Action onClose, float widthShare = 0.92f, float heightShare = 0.9f, int sortingOrder = 1000)
public void Show();
public void Hide();
public bool Visible { get; }
public UiBuilder Toolbar { get; }   // row under the title, height 26
public UiBuilder Content { get; }   // column filling the rest, for the page
public Text TitleText { get; }
```

- The window sits on its own overlay canvas (`UiCanvas.Create`), so it draws above the game's UI. It is created hidden.
- `widthShare` and `heightShare` are the fraction of the screen the frame takes, centred.
- `CLOSE` calls `onClose`. The window does not hide itself; the callback should call `Hide()`, or whatever closes the window.
- The constructor throws `InvalidOperationException` if the pause menu is not loaded yet. Create windows in response to the game being in a scene (for example, from a `GameEvents` handler or a click).
- The canvas belongs to `owner` and is destroyed when the mod unloads. Higher `sortingOrder` draws on top.

## `UiTabbedWindow` and `UiPage`

A `UiWindow` with a tab per page. The mod menu and the Cheat Menu mod are both built on it.

```csharp
public UiTabbedWindow(ITracker owner, string title, Func<IReadOnlyList<UiPage>> pages, Action onClose,
    float tabWidth = 150f, int sortingOrder = 1000)
public void Open();            // shows the window and (re)builds the selected page
public void Close();
public void Tick();            // call every frame while open: tabs, rebuilds, Refresh 4x a second
public void Select(string title);
public void PagesChanged();    // call when the page list changes
```

`UiPage` is the page base class (`Title`, `Order`, `Build`, `Refresh`, `OnHide`, `Rebuild`); `MenuPage` below is just a `UiPage` meant for the mod menu.

## `UiInputLock`

`UiInputLock.Acquire(bool pause)` takes the game's controls away while a window is open: player input off, cursor free, and the game paused if asked (never in a shared house, `WorldRole.IsShared`: in co-op it would stop for everyone, so the game keeps running behind the window). It returns an `IDisposable`; dispose it on close. Several windows can hold it at once (the mod menu and the cheat menu together, say) and everything comes back when the last one lets go, including the time scale from before the pause.

## `Hotkey`

`NeetModKit.Input.Hotkey` is a key picked by name in a settings file:

```csharp
private readonly Hotkey _key = new Hotkey("Insert", InputKey.Insert); // fallback if the name is unknown
// in OnUpdate:
if (_key.WasPressed) Toggle();
// when the setting changes:
_key.Name = config.Key;
```

The name is parsed once when set, so checking it every frame is cheap. `Hotkey.EscapePressed` is there for closing windows. (The game has its own global `Key` and `Keyboard` classes, so alias the Input System ones: `using InputKey = UnityEngine.InputSystem.Key;`.)

## `UiCanvas`

```csharp
public static Canvas Create(ITracker owner, string name, int sortingOrder = 1000, bool interactive = true,
    Vector2? referenceResolution = null)
```

Creates a `ScreenSpaceOverlay` canvas with a `CanvasScaler` (copied from the pause menu's, or a fixed 1148.7 x 436.8 reference if the menu is not loaded) and a `GraphicRaycaster`. The object survives scene changes and is destroyed with `owner`. Higher `sortingOrder` draws on top. Use it for any overlay that is not a `UiWindow`.

- `interactive: false` leaves out the raycaster, so nothing on the canvas takes clicks (the version tag uses this).
- `referenceResolution` scales the canvas on its own (scale with screen size, matching width and height equally) instead of like the game's menus.

## `UiCloning`

Helpers for working with the game's own objects. Used by the toolkit, the computer helpers and the pause menu integration. Anything created with a `ITracker` owner is destroyed when that owner unloads.

```csharp
public static GameObject Clone(ITracker owner, Transform template, string name, Transform parent = null)
public static void Remove(GameObject go)
public static Button SetClick(Button button, Action onClick)
public static void SetText(Transform target, string text)
public static void Place(Component element, Vector2 anchoredPosition, Vector2? size = null)
public static void Hide(ITracker owner, GameObject go)
```

### `Clone`

Copies `template` (next to it, or under `parent`), names the copy, activates it and tracks it on `owner`. Use it to copy an existing game element that is already in the scene. `UiTheme` copies templates from a hidden holder once, while `Clone` copies a live object.

### `Remove`

Hides the object, detaches it from its parent, then destroys it. Detaching and hiding happens at once because `Destroy` only takes effect at the end of the frame; a reloaded copy of a mod in the same frame would otherwise still find the old object. `UiBuilder.Clear` uses it for each child.

### `SetClick`

```csharp
var button = UiCloning.SetClick(button, () => Open());
```

Replaces the whole `onClick` event, including the listeners saved in the scene, which `RemoveAllListeners` cannot remove. Use it on a copied button instead of `onClick.AddListener`, which would keep the game's handler. `UiBuilder.Button` already does this on its own copies.

### `SetText`

Sets the first text under the object, TextMeshPro or legacy `Text`, whichever is found first. Works on a copied button whose label is nested.

### `Place`

Sets `anchoredPosition` and, if given, `sizeDelta` on a `RectTransform` element.

### `Hide`

```csharp
UiCloning.Hide(this, canvas.Find("ReportType")?.gameObject);   // canvas = the pause menu's Canvas_Settings
```

Hides a game object until `owner` unloads. Several owners can hide the same object: a count is kept, and the object comes back only when all of them have cleaned up. If the game itself hid the object, `Hide` leaves it alone and nothing is restored. `PauseMenuMods` uses it for the feedback form's parts.

## Mod menu

The mod menu is opened with `F1` (set by `KitConfig.MenuKey`), with `Escape` while it is open, or with `MODS` in the pause menu (the framework's `PauseMenuModsFeature` puts that column where the feedback form was).

While it is open:

- The game's player input is disabled and the cursor is free.
- If `KitConfig.PauseWhileOpen` is on (the default), `Time.timeScale` is 0. The previous scale is restored on close.

The menu is `ModMenu`:

```csharp
public static void AddPage(NeetMod owner, MenuPage page)
public static void Open(string pageTitle = null)
public static void Close()
public static void Toggle()
public static bool IsOpen { get; }
public static IReadOnlyList<MenuPage> Pages { get; }
```

- `AddPage` adds a tab. Tabs are sorted by `Order`, then `Title`. A page is removed when its owner unloads, and the tab bar updates.
- `Open(title)` opens the menu on the page with that title. With no title it keeps the current page, or shows the first tab. The menu can only open once the pause menu is loaded; otherwise it logs a warning.

The built-in tabs and their `Order`:

| Title | `Order` | Purpose |
|---|---|---|
| Mods | 0 | Installed mods, load/unload, features, logs |
| What's New | 10 | Changelog |
| PC Apps | 20 | Computer app URLs (see [computer-apps.md](computer-apps.md)) |
| Settings | 30 | Every registered settings object |
| Errors | 40 | The error report (below) |

Phone Expansion adds a Phone tab (30).

### Errors tab

Shows the loader's error report (`ErrorReport`, see [loader.md](loader.md#error-report)) newest first, and updates while it's open: new problems appear, counts go up. Each problem is a tinted panel:

- **ERROR** (red): something was logged as an error, by the game, Unity or a mod. Uncaught exceptions land here.
- **CAUGHT** (amber): a mod noticed a problem and handled it, then reported it with `ErrorReport.Add`. Unofficial NEET Patches does this for story events it stops or skips.

Under the title: the source, how often and when, and the scene. SHOW DETAILS expands the details (`StackText` drops Mono's offsets and hashes, names Harmony's patched copies "(patched)", and dims Unity and .NET frames so the game's and mods' frames stand out). COPY REPORT puts the whole report on the clipboard as plain text; CLEAR empties it. The Mods page shows how many problems there are, with a button to this tab.

Cheats live in the separate Cheat Menu mod (Insert key), not in the mod menu.

### `MenuPage`

```csharp
public abstract class MenuPage : UiPage { }

public abstract class UiPage
{
    public abstract string Title { get; }
    public virtual int Order => 500;
    public abstract void Build(UiBuilder ui);
    public virtual void Refresh() { }
    public virtual void OnHide() { }
    public void Rebuild();
}
```

| Member | When it runs |
|---|---|
| `Build(ui)` | Each time the page is shown: when its tab is selected, and when the menu opens with that page still selected. `ui` is the page's content column, already cleared. |
| `Refresh()` | Right after `Build`, then every 0.25 seconds of unscaled time while the page is visible (so it runs while the game is paused). Use it to update values. |
| `OnHide()` | When the user switches to another tab, or the menu closes. |
| `Rebuild()` | Asks for `Build` to run again on the next frame. Call it when the layout has to change, such as after adding or removing items. |

The sequence when a tab is selected: the previous page's `OnHide`, then the new page's `Build`, then its `Refresh`. Each frame while the menu is open, the menu checks the rebuild request first. If it is set, the page is built again instead of refreshed.

`Order` default is 500. The framework's built-in pages use 0 to 40 (see the table above).

### Example page

A page with a counter. It keeps its count in a static field, so the count survives closing the menu. It refreshes its readout and rebuilds only when reset.

```csharp
using NeetModKit.Menu;
using NeetModKit.Ui.Toolkit;
using UnityEngine.UI;

namespace MyMod;

public sealed class CounterPage : MenuPage
{
    private static int _count;
    private Text _readout;

    public override string Title => "Counter";
    public override int Order => 600;

    public override void Build(UiBuilder ui)
    {
        ui.Header("COUNTER");
        ui.Text("Counts presses on this page. The count is kept while the game runs.");
        _readout = ui.Text("");

        var row = ui.Row(8f, 26f);
        row.Button("ADD", () =>
        {
            _count++;
            Refresh();
        }, 120f, accent: true);
        row.Button("RESET", () =>
        {
            _count = 0;
            Rebuild(); // next frame; Refresh runs after the build
        }, 120f);
    }

    public override void Refresh()
    {
        _readout.text = $"Presses: {_count}";
    }
}
```

Register it from the mod's `OnLoad`:

```csharp
public override void OnLoad()
{
    ModMenu.AddPage(this, new CounterPage());
}
```

The page's controls are destroyed and rebuilt by `Build`, so do not keep references to them across builds. `Refresh` can rely on the controls from the latest `Build`.

## Settings pages

Settings are shown on the Settings tab. Register a config object with `ModSettings`:

```csharp
public static void Register<T>(NeetMod owner, ConfigFile<T> config, string title = null) where T : class, new()
```

`config` is what `LoadConfig<T>()` returns. `title` is the section heading; it defaults to the mod's display name. The section is removed when the mod unloads.

```csharp
Config = LoadConfig<CardExpansionConfig>();
Config.Save();
ModSettings.Register(this, Config, "Card Expansion");
```

`ConfigEditor` reads the object's public instance fields and builds one control per field, in declaration order. Properties are ignored.

### Field types

| Field type | Control | Notes |
|---|---|---|
| `bool` | Toggle with the label on the right | |
| enum | One button per enum name, in a row. The current value is accent-coloured. | Each button is 120 wide. |
| `float` or `int` with `[Range(min, max)]` | Slider with a readout | `int` uses whole numbers and rounds. |
| `string` | Labelled text box | Every edit is saved to the field. |
| `float`, `int` or `double` without `[Range]` | Labelled text box | Text that does not parse is ignored and the old value stays. |
| anything else | Muted text: "`<label>`: edit in the config file" | |

Attributes (all in `NeetModKit.Settings`, except `DescriptionAttribute`):

| Attribute | Effect |
|---|---|
| `[Range(min, max)]` | Makes a `float` or `int` field a slider. Ignored on other types. |
| `[Label("text")]` | Replaces the label. Without it, the label is the field name split at capitals and sentence-cased (`PauseWhileOpen` becomes "Pause while open"). |
| `[Description("text")]` | Muted help text under the control. This is `System.ComponentModel.DescriptionAttribute`, so add `using System.ComponentModel;`. |
| `[Hidden]` | Leaves the field off the page. The field is still saved. |
| `[Shared]` | The setting shapes the shared house: in co-op the host's value counts, and while another game decides it the page shows it read-only with the reason. |

Labels of sliders, text boxes and enum headings are shown in capitals; toggle labels are not. Put the wording you want in `[Label]` and it will be capitalised where the control does that.

Example, from `CardExpansionConfig`:

```csharp
using System.ComponentModel;
using NeetModKit.Settings;

public sealed class CardExpansionConfig
{
    [Description("Crystals one summon costs.")]
    [Range(50, 5000)]
    public int SummonCost = 500;

    [Label("Shiny chance")]
    [Description("Chance that a summon is shiny and gives the card 5 free levels.")]
    [Range(0f, 0.5f)]
    public float ShinyChance = 0.05f;
}
```

### Save timing

- An edit changes the object at once, so other code sees the new value immediately.
- The file is written 0.5 seconds after the last edit, or when the user changes to another section, or when the Settings tab is left (including by closing the menu).
- Saving writes the config file in the mod's folder.

Mods do not need to call `Save` after the menu changes a value.

## Palette

`MenuPalette` holds the colours used for text emphasis, matched to the game's menus.

| Member | Value | Use |
|---|---|---|
| `Accent` | orange (1, 0.25, 0) | Emphasis |
| `Good` | slime green (0.56, 0.9, 0.29) | Running, success |
| `Warn` | candy-corn yellow (1, 0.8, 0.22) | Notes, things to do |
| `Bad` | red (0.95, 0.25, 0.25) | Errors |
| `Muted` | lilac grey (0.7, 0.64, 0.78) | Secondary text |
| `Text` | white | |

```csharp
public static string Hex(Color c)
public static string Paint(string text, Color c)
```

`Paint` wraps text in a rich-text colour tag. `Hex` gives the `#rrggbb` form for other tags. Rich text works in `Text` and `Header`, and in the labels that copy the game's text templates.

```csharp
ui.Text(MenuPalette.Paint("Restart the game to apply on/off changes", MenuPalette.Warn));
ui.Text(MenuPalette.Paint("v1.2.0  by pawbeans", MenuPalette.Muted));
```

To colour a control's own text after creating it, set `Text.color` on the component (the framework does this for list buttons, by finding the button's `Text`).
