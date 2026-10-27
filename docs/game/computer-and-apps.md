# Computer & apps

Scope: the in-game computer (`Computer`), its apps and screens, the anime card idle game, the online store and custom item shops, email, stocks, taxes, and the other electronics (phone, TV, radio, cassette, camera, pictures, keyboard). All paths are relative to `decompiled/Assembly-CSharp/`. Line numbers refer to those files.

Confidence notes: statements are taken from the decompiled code. Anything that depends on the scene hierarchy or serialized prefab values is listed under "Open questions" and should be confirmed at runtime.

## Overview

### How the computer works

- **World-space UI canvas.** `Computer` (`Computer.cs:7`) sits on a `Canvas`-bearing GameObject (it reads `GetComponent<RectTransform>()`, `GetComponent<CanvasGroup>()`, `GetComponent<Usable>()`). The canvas is turned on and off by `Computer_PowerButton`, not by `Computer` itself. ``Computer.Start()` calls `base.gameObject.SetActive(false)` (`Computer.cs:120`), so the computer starts hidden.
- **Cursor.** `Computer.Update()` (`Computer.cs:186-210`) converts the mouse position (`Mouse.current.position`, new Input System) into the canvas local space via `RectTransformUtility.ScreenPointToLocalPointInRectangle` with `HouseManager._.mainCamera`. The cursor is clamped to x in [-45, 45] and y in [-38, 38] (`Computer.cs:195`). The visual cursor image is offset by (+2.1, -2.2) (`Computer.cs:188`). `Computer_FaceChanger.Update()` (`Computer_FaceChanger.cs:38-46`) uses the same cursor code.
- **Interactability.** `OnTriggerEnter` selects the first child `Button` through `EventSystem` when the `BrainPlayer` enters the trigger (`Computer.cs:162-168`). `OnTriggerStay` sets `CanvasGroup.interactable = true` while the player is inside, and `OnTriggerExit` sets it to false (`Computer.cs:170-184`). The collider and `CanvasGroup` must live on the same object as `Computer`.
- **Screensaver.** `Computer.Update()` turns the `screenSaver` GameObject on after 30 seconds (`Computer.cs:204-206`) when the cursor is outside the canvas rectangle. Inside the rectangle the timer resets and the screensaver is hidden (`Computer.cs:196-200`). The intended "mouse moved" test compares against a local `zero` that is reset to `Vector2.zero` on every call (`Computer.cs:189`, `Computer.cs:196`, `Computer.cs:209`). The effective test is therefore "cursor is inside the rectangle and not exactly at the local origin", not "the mouse moved". The `Time.time > 1f` guard is also in that condition.
- **Click audio.** Every `Button` under the computer gets a click-sound listener at `Start` (`Computer.cs:212-222`). Buttons created later need `Computer.RegisterButtonClickAudio(Button)` (`Computer.cs:224-230`). `PlayClickSound` uses `audios[0]` (`Computer.cs:232-236`).
- **Light/monitor glow.** The computer registers with its room's `LightSwitch` and toggles `monitorGlow` when the light changes (`Computer.cs:151-160`).
- **Singleton link.** `Computer.Awake()` sets `HouseManager._.mainComputer = this` if `HouseManager._` exists (`Computer.cs:97-104`). Most code in this area reaches the computer through `HouseManager._.mainComputer`.
- **Power.** `Computer_PowerButton` (`Computer_PowerButton.cs`) owns the on/off state. `Use()` plays a click and toggles. `StartComputer` runs a randomized boot sequence (`BootSequence`, `Computer_PowerButton.cs:137-184`). `StopComputer` disables `compCanvas`. Setting `IsPowered = false` stops the computer (`Computer_PowerButton.cs:46-60`).

### How apps and screens are switched

- **`Computer_Button`** (`Computer_Button.cs`) is the generic navigation component. It has parallel arrays `commands[]` (`Open`, `Close`, `SelectNav`) and `windows[]` (GameObjects). `ButtonCommand()` (`Computer_Button.cs:39-64`) walks the arrays by index and applies each command to the matching window. With `toggleOpenClose` set, `Open` and `Close` flip after each call. Note that `commands[i]` is indexed with the loop over `windows.Length`, so `commands` must be at least as long as `windows` or it throws.
- **`ScreenModule`** (`ScreenModule.cs`) is the base class for TV channels (see "Other electronics"). It is not used for computer screens. The computer screens are plain GameObjects toggled by `Computer_Button`.
- **`Computer_Browser_Addresser`** (`Computer_Browser_Addresser.cs`) is the browser. Its `InputField` is the address bar. `AddressWrapper` (`Computer_Browser_Addresser.cs:72-88`) hides every entry in `websites[]`, then shows `websites[urls.IndexOf(text)]` if the typed text is in `urls`. It then normalizes the text to `"http://" + text + ".com"` if it does not already start with `http://`. The home page is `LoadHome()` = `LoadWebsite("metscape")` (`Computer_Browser_Addresser.cs:67-70`). Because the lookup happens before normalization, `urls` holds bare names such as `metscape`, not full URLs. Typing `http://metscape.com` matches nothing (`Computer_Browser_Addresser.cs:79`).
- **Text entry.** `Keyboard` (`Keyboard.cs`) is an on-screen keyboard. Each `Keyboard_Key` (`Keyboard_Key.cs:14-27`) injects a `Event.KeyboardEvent(character)` into the current `input` and then calls `Computer.IInputReceiver.ProcessKey` on the same object. The browser implements `IInputReceiver`. The address bar is attached through `Computer_Browser_Addresser.SetRefListener()` (`Computer_Browser_Addresser.cs:27-31`).

### Money

- **Storage.** `Computer.money` starts at 50 (`Computer.cs:54`). Its public property `Computer.Money` (`Computer.cs:62-77`) is the only way the rest of the game changes it. The setter calls `OnMoneyChanged(oldValue, newValue)` before storing the new value, and logs with `print`.
- **Subscribers.** `Computer.Start()` subscribes three delegates (`Computer.cs:121-142`):
  1. Tax accrual. If the change is positive, `Taxes += delta * 0.12f`.
  2. `UpdateAppCashText`, which rewrites `moneyAmtAppText` with `Mathf.Round(money) + "$"`.
  3. Achievement tracking (`totalMoneyAchievement` gets the new value, `biggestMoneyGainAchievement` gets the delta).
  `StockGraph` also subscribes to `OnMoneyChanged` to update its cash label (`StockGraph.cs:92`).
- **Tax accrual.** Any positive money change adds 12% of the delta to `Taxes` (`Computer.cs:121-127`). Negative changes do not reduce `Taxes`. This means income from every source is taxed at 12%, including selling stock (`StockMarket.cs:123`), redeeming crystals (`AnimeCardGenerator.cs:159`), dialogue `AddMoney` (`Dialogue.cs:159`), package sales (`PickupPackageArea.cs:128`), and loading a save (see Modding notes).
- **Taxes.** `Computer.Taxes` (`Computer.cs:79-95`) is the accrued tax. Its setter also rewrites the app's money text and fires `OnTaxesChanged`. Taxes become bills in `TaxHandler`, see "Email, stocks, taxes".

### Ordering items: `Computer.OrderItem`

`OrderItem(GameObject obj, Transform deliverySpot = null, float priceOffset = 0f, Action<GameObject> OnUnboxingApplyToContents = null)` (`Computer.cs:238-285`):

1. Delivery spot defaults to `HouseManager._.packageDropOffPoint` (`Computer.cs:240-243`).
2. **Cop van roll.** If the item's `Usable.purchaseableAt` is `BlackMarket` or `OccultMarket`, there is a 20% chance `HouseManager._.SpawnCopVan()` is called (`Computer.cs:245-248`). This happens before the affordability check, so a failed order can still bring the police.
3. **Demo restriction.** The order goes through only if `!HouseManager._.demoMode` or the item is `StoreType.Normal` (`Computer.cs:249`). In demo mode, black-market and occult items cannot be ordered at all.
4. **Affordability and cooldown.** The order needs `Money > price + priceOffset` and at least 0.5 seconds since the last order (`Computer.cs:249`).
5. **Delivery.** A `shippingBox` prefab is instantiated above the delivery spot. Its `Cardboard_Box.content` is set to `obj`, and `OnUnboxingApplyToContents` is stored for unboxing (`Computer.cs:252-254`). The box is scaled from the item's mesh bounds (`Computer.cs:256-276`). The confirmation sound plays on `GetComponents<AudioSource>()[1]`.
6. **Charge.** `Money -= price + priceOffset` (`Computer.cs:279`). The return value is `obj` on success and `null` otherwise.

Known callers: `StoreGenerator` (`StoreGenerator.cs:47`), `CustomItemShop` (`CustomItemShop.cs:85` and `:91`, with `priceOffset = 10f` and a callback). `Cardboard_Box.cs` (not read) handles unboxing.

## Anime card game (deep dive)

This is the most important part for expansion. It is implemented by two classes:

- `AnimeCardGenerator` (`AnimeCardGenerator.cs`): the parent. It holds the crystal currency, the name list, the scene's card list, the redeem button, and the save entry.
- `AnimeCard` (`AnimeCard.cs`): one per card. Each card has its own level, medal, exp, income, and price, and unlocks the next card.

The game runs in real time: an unlocked card gains exp each frame and converts full exp bars into crystals.

### Scene layout (inferred from code)

- `G` = the `AnimeCardGenerator` GameObject. Every `AnimeCard` is a direct child of `G` (`AnimeCard.Awake`, `AnimeCard.cs:205`, takes `transform.parent` as the generator). Cards are found with `GetComponentsInChildren<AnimeCard>(true)` (`AnimeCardGenerator.cs:109`), so the number of cards is set by the scene prefab. It is not computed at runtime.
- `P` = `G.parent`. Its RectTransform is sized to `176.7 x H` (`AnimeCardGenerator.cs:151`).
- `Q` = `P.parent`. Its height is set to `H` (`AnimeCardGenerator.cs:152`).
- These names are ours. In the code, `AnimeCardGenerator.OnEnable` uses `base.transform.parent` and `base.transform.parent.transform.parent`. `AnimeCard.BuyCard` uses `base.transform.parent.transform.parent` (= P) and its parent (= Q).

### Fields

`AnimeCardGenerator` (`AnimeCardGenerator.cs:27-53`):

| Field | Type | Role |
|---|---|---|
| `crystals` | float | Currency. Read and written through the `Crystals` property. |
| `crystalMeterText` | TextMeshProUGUI | Shows `Mathf.Round(Crystals) + "*"` (`AnimeCardGenerator.cs:64`, `:137`). |
| `animeCard` | GameObject | Template reference. Not read anywhere in this file. Probably the prefab for a card. |
| `cardParticle` | ParticleSystem | Plays on promotion (`AnimeCard.cs:275-276`). |
| `cards` | AnimeCard[] | All child cards, filled in `Start`. |
| `maxCardUnlocked` | int, default 3 | Index of the highest unlocked card. Drives which cards are visible. |
| `redeemBtn` | Button | Hooked to `RedeemCrystals` in `Start` (`AnimeCardGenerator.cs:141-144`). |
| `noSaveSubscribe` | bool | Guard so that `Start` runs its one-time work only once. |
| `animeNames` | List<string> | 72 names. Index = card position. See the list below. |

`AnimeCard` (`AnimeCard.cs:41-91`):

| Field | Type | Role |
|---|---|---|
| `picture` | Image | Card art. Turned black when locked (`AnimeCard.cs:152`). Its `mainTexture` is also the medal-5 gradient (`AnimeCard.cs:302`). |
| `frame` | Image | Gradient frame. Its material gets a `_Gradient` texture (`AnimeCard.cs:199-200`, `:292-297`). |
| `expBar` | Image | Exp bar. Scaled on x by `exp / expToLevel` (`AnimeCard.cs:137`, `:191`). |
| `levelTxt` | TMP_Text | Shows `"+" + level` (`AnimeCard.cs:102`). |
| `incomeTxt` | TMP_Text | Shows `Mathf.Round(income) + "*"` (`AnimeCard.cs:119`). |
| `girlName` | TMP_Text | Set from `animeNames[index]` in the generator's `Start` (`AnimeCardGenerator.cs:118`). |
| `buyButton` | Text (legacy UI) | Shows `"Buy N*"` before the card is first unlocked, then `"Up. N*"` (`AnimeCard.cs:201`, `:237`, `:244`, `:273`). |
| `promoteBtn` | GameObject | Shown when the card reaches its level cap (`AnimeCard.cs:103-106`). Hidden by `PromoteCard`. |
| `level` | int (private) | Upgrade level. Exposed via `Level`. |
| `expToLevel` | float | Exp needed for one crystal payout. Set by the prefab and reset by promotion. |
| `cardId` | int | Position used for unlocking and layout math. Set in the prefab. |
| `medalLvl` | int | Medal tier, 0 to 5. |
| `medalTex` | Texture[] | Gradient textures indexed by `medalLvl`. |
| `purchasePrice` | float | Current cost of the next purchase. Saved. |
| `exp` | float | Current exp toward `expToLevel`. Saved. |
| `income` | float | Crystals paid per full bar. Saved. |
| `initExpToLevel`, `initIncome`, `initPrice` | float | Base values used for formulas. Set once in `Awake` from the prefab values (`AnimeCard.cs:206-208`). Not saved. |
| `initExpBarColor` | Color | Restored after the click flash. |
| `locked` | bool (private) | Whether the card is unlocked. Exposed via `Locked`. |
| `nextCard` | AnimeCard | Card revealed when this one is unlocked. Set automatically in the generator's `Start` (see chain below). |
| `animeCardGenerator` | AnimeCardGenerator (private) | Set in `Awake` when `firstLoad` is true. |
| `mainCam` | Camera (private) | Not used in this file. |
| `audios` | AudioSource[] (private) | Set in `Awake` when `firstLoad` is true. Index 0 = click, 1 = promote, 2 = buy/unlock (from usage at `AnimeCard.cs:235`, `:245`, `:277`, `:312`). |
| `firstLoad` | bool | One-time initialization flag. The prefab value is not visible in code. |

### Full economy (exact formulas)

All numbers below come from the code. Prefab values (`initExpToLevel`, `initIncome`, `initPrice`, `medalTex`) are not visible in the code.

**Exp gain**

- Idle: each frame, an unlocked card adds `Time.deltaTime * 1` exp (`AnimeCard.cs:214-220`). That is 1 exp per second.
- Click: `ClickedCard()` (`AnimeCard.cs:305-314`) adds `1 + initExpToLevel * 0.008` exp, only when unlocked. It also plays a click animation and sound.

**Crystal payout**

- `Exp` setter (`AnimeCard.cs:123-139`): when `exp >= expToLevel`, exp is reset to 0 and `GenerateCrystals()` adds `Income` to the generator's crystals (`AnimeCard.cs:222-225`). Any overflow past `expToLevel` is discarded.
- Idle rate, per unlocked card: `Income / expToLevel` crystals per second (1 exp/s divided by the bar size, times the payout). Clicks add `(1 + 0.008 * initExpToLevel) / expToLevel` bars per click.
- `expToLevel` is never changed by `BuyCard`. It only changes in `Load` (from save) and is reset to `initExpToLevel` on promotion (`AnimeCard.cs:271`).

**Level-up (buy) cost**

- Cap per tier: a buy is allowed while `Level < Mathf.Max(1, medalLvl) * 50` (`AnimeCard.cs:229`). So tier 0 and tier 1 both cap at level 50. Tier m caps at `50 * m`.
- After a successful upgrade, `Level++` runs first, then `purchasePrice = initPrice * Mathf.Pow(1.07f, level)` (`AnimeCard.cs:241-242`). So the cost to go from level L to L+1 is `initPrice * 1.07^L`. The unlock cost is the `purchasePrice` as it stands (the initial "Buy" price, `AnimeCard.cs:232-237`).
- Income after upgrade: `Income = initIncome * 1.67f * level` (`AnimeCard.cs:243`). At level 0 the income is whatever was set before (the prefab or saved value). Level 1 gives `1.67 * initIncome`. Level L (L >= 1) gives `1.67 * L * initIncome`. The jump from level 0 to level 1 is therefore 1.67x, not 1x.

**Unlock**

- A locked card needs `Crystals >= purchasePrice` (`AnimeCard.cs:229`). It then sets `Locked = false`, sets `maxCardUnlocked = cardId` on the generator (`AnimeCard.cs:236`), and the cost is subtracted.
- Unlocking a card also sets `medalLvl` to at least 1 (`AnimeCard.cs:158-161`), sets the picture color back to white, and activates `nextCard` (`AnimeCard.cs:163-165`).

**Promotion (medals)**

- Visibility of `promoteBtn`: the `Level` setter activates it when `Level >= Mathf.Max(1, medalLvl) * 50` (`AnimeCard.cs:103-106`).
- `PromoteCard()` runs only if `Level >= medalLvl * 50` (`AnimeCard.cs:259`). Then:
  - `promoteBtn` is hidden.
  - The card texture is set to `medalTex[0]` (`AnimeCard.cs:262`).
  - `medalLvl++` if it is below 5 (`AnimeCard.cs:263-266`).
  - `Exp = 0`, `initIncome *= 2`, `initPrice *= 2`, `Level = 0`, `expToLevel = initExpToLevel`, `purchasePrice = initPrice`, and `Income = initIncome`.
  - The particle effect and sound play, and the medal texture is refreshed.
- **Important:** the cap of 5 applies only to `medalLvl`. `initIncome` and `initPrice` double on every promotion, even at medal 5. A medal-5 card can therefore be promoted repeatedly, each time reaching level 250 again and doubling income and cost. This is what the code does. Whether that was intended is unknown.
- The effective medal on a card drives the cap: at medal 5, the level cap is 250.

**Medal texture rules**

- `SetCardMedalTexture()` (`AnimeCard.cs:300-303`): locked cards use `medalTex[0]`; medal 5 uses `picture.mainTexture`; other medals use `medalTex[medalLvl]`.
- `Awake()` always reads `medalTex[medalLvl]` (`AnimeCard.cs:200`). If `medalTex` has only 5 entries (indices 0-4), a saved medal of 5 will throw. See Open questions.

**Crystals and redeem**

- `RedeemCrystals()` (`AnimeCardGenerator.cs:155-163`): if `Crystals >= 10000`, then `Computer.Money += 3` and `Crystals -= 10000`. The `Money` setter applies the 12% tax accrual, so each redeem also adds 0.36 to `Taxes`.
- Crystals are shown as `"N*"`. Income is shown as `"N*"`.

**Summary of rates**

- Crystals per second for one unlocked card at rest: `Income / expToLevel`.
- Costs for the next level: `initPrice * 1.07^level`.
- Income at level L (L >= 1): `1.67 * L * initIncome`, doubling on each promotion.

### Discovery of cards and the unlock chain

- `Start()` (`AnimeCardGenerator.cs:103-147`) fetches `cards` with `GetComponentsInChildren<AnimeCard>(true)`. This happens once (`noSaveSubscribe` guard, `:107-110`). The order of the array is the hierarchy order.
- For each card (in that order), `Start`:
  - Sets `previous.nextCard = current` (`AnimeCardGenerator.cs:114-117`). The `nextCard` chain is therefore built from hierarchy order. You do not set it by hand.
  - Sets `girlName.text = animeNames[num]` (`:118`). `num` is the card's array index. This throws if there are more cards than names.
  - Calls `Locked = true` and `Level = 0` on the first run (`:122-126`).
  - Hides cards whose 1-based position `num > maxCardUnlocked + 2` (`:128-131`). In practice cards with index `> maxCardUnlocked + 1` are hidden.
- After the first run, `Start` subscribes `Save` to `SaveLoadHandler._`, hooks `redeemBtn`, and sets `noSaveSubscribe = true` (`:138-146`).
- Reveal chain: unlocking card k sets `maxCardUnlocked = k` and activates `nextCard` (card k+1). The `OnEnable` size formula also depends on `maxCardUnlocked`.
- The `Start` hide rule can be reached during `Load` as well, because `Load` calls `Start` (`AnimeCardGenerator.cs:84`, `:95`).

### Layout and the 3-per-row math

- `OnEnable` (`AnimeCardGenerator.cs:149-153`) sets `P.sizeDelta = (176.7, H)` and `Q.sizeDelta.y = H`, with `H = 170 + 65 * (maxCardUnlocked + 1) / 3` (integer division).
- `AnimeCard.BuyCard` (`AnimeCard.cs:247-253`) grows the same two rectangles when `(cardId + 1) % 3 == 0` and the current height is smaller than `170 + 65 * (cardId + 1) / 3`. It only grows, never shrinks.
- `176.7` is a hard-coded width, `170` a base height, `65` a row height, and `3` the number of columns. These values are not tied to the card art. Any change to column count needs the formulas changed in both places.
- `StoreGenerator.OnEnable` (`StoreGenerator.cs:53-54`) uses the same pattern with fixed `176.7 x 1233`.

### Save format and load order

Saves are handled by `SaveLoadHandler`. The generator subscribes with `SubscribeSave(Save)` (`AnimeCardGenerator.cs:140`).

**Generator entry** (`SaveDataAnimeCards`, `AnimeCardGenerator.cs:10-25`):

```
{
  "type": "AnimeCardGenerator",
  "subSaveData": [ <one entry per card, in hierarchy order> ],
  "saveId": 0,
  "maxCardUnlocked": <int>,
  "crystals": <float>
}
```

`saveId` is declared but never set. The generator's entry is found by its `type` string in the top-level `Save` array (`SaveLoadHandler.cs:378`).

**Card entry** (`SaveDataAnimeCard`, `AnimeCard.cs:11-39`):

```
{
  "type": "AnimeCard",
  "level": <int>,
  "medalLvl": <int>,
  "locked": <bool>,
  "exp": <float>,
  "expToLevel": <float>,
  "income": <float>,
  "purchasePrice": <float>
}
```

Cards are not identified by name or `cardId`. Each saved entry is matched to a card by position in `subSaveData`.

**Load order** (`SaveLoadHandler.cs:374-392`, called from `LoadComputer`):

1. `Computer_PowerButton.Load` (restores money, power, wallpaper; calls `Computer.Start` manually).
2. `StockMarket.Load`.
3. `AnimeCardGenerator.Load(list[0])` (or `Load({}, resetOnly: true)` if there is no save entry).
4. `AppDownloader.Load`.
5. `EmailHandler` and the rest.

`AnimeCardGenerator.Load` (`AnimeCardGenerator.cs:78-101`) works like this:

1. Sets `maxCardUnlocked` from the save.
2. Calls `Start()` (rebuilds `nextCard`, sets names, locks all cards, hides by index).
3. Sets `Crystals` from the save.
4. Calls `cards[i].Load(subSaveData[i])` for each saved entry.

`AnimeCard.Load(JToken)` (`AnimeCard.cs:175-195`):

1. Calls `Awake()` manually. This runs the `firstLoad` block and instances the frame material.
2. Reads the saved values in this order: `Level`, `medalLvl`, `Locked`, `exp`, `expToLevel`, expBar scale, `Income`, `purchasePrice`.

**Not saved:** `initIncome`, `initPrice`, `initExpToLevel`, the card's `cardId`, and the card's name. The `init*` values come from the prefab in `Awake` (`AnimeCard.cs:206-208`). Promotion doubles `initIncome` and `initPrice` at runtime. After a reload the doubled base values are lost, while the saved `income` and `purchasePrice` keep their values. The next upgrade then recalculates from the prefab base. This looks like a real bug. It needs to be checked at runtime.

### Name list (`animeNames`, 72 entries, `AnimeCardGenerator.cs:43-53`)

Index = card position in the hierarchy. Strings are copied verbatim from the source, in order. The entry at index 67 is written with the `\u00a0` escape in the source, so it contains a non-breaking space.

```
 0 Abulia   1 Yersinia   2 Lowry   3 Omskh   4 Landeau   5 Neisseria   6 Hirayama   7 Rickettsii
 8 Myiasiz   9 Troyere  10 Vybrio  11 Picca  12 Chapare  13 Zosteer  14 Rickettsial  15 Lafora
16 Dandi  17 Reyes  18 Enuresis  19 Anisakis  20 Pantotena  21 Agnosia  22 Fabry  23 Meralgia
24 Sotos  25 Lujo  26 Kleine  27 Paulio  28 Schilde  29 Kuru  30 Sidz  31 Machado
32 Dravette  33 Dystonias  34 Ohtahara  35 Lime  36 Shiga  37 Hansen  38 Marburg  39 Trichotille
40 Loupiz  41 Taenia  42 Munchausan  43 Menkes  44 Pelizaez  45 Bilharzia  46 Fregoli  47 Rabheez
48 Hanta  49 Lassa  50 Kyasanur  51 Kearns  52 Brodee  53 Meniere  54 Belle  55 Amaurosis
56 Sayre  57 Cee-Auris  58 Sella  59 Kooma  60 Prioon  61 Gouttee  62 Myotonia  63 Tetann
64 Tay-Sakhi  65 Viliusk  66 Fuwajil  67 Loa\u00a0Loa  68 Leigh  69 Chiari  70 Akinetoph  71 Bartonella
```

Exact source literal sequence (use this for copying into code):

```csharp
"Abulia", "Yersinia", "Lowry", "Omskh", "Landeau", "Neisseria", "Hirayama", "Rickettsii", "Myiasiz", "Troyere", "Vybrio", "Picca", "Chapare", "Zosteer", "Rickettsial", "Lafora", "Dandi", "Reyes", "Enuresis", "Anisakis", "Pantotena", "Agnosia", "Fabry", "Meralgia", "Sotos", "Lujo", "Kleine", "Paulio", "Schilde", "Kuru", "Sidz", "Machado", "Dravette", "Dystonias", "Ohtahara", "Lime", "Shiga", "Hansen", "Marburg", "Trichotille", "Loupiz", "Taenia", "Munchausan", "Menkes", "Pelizaez", "Bilharzia", "Fregoli", "Rabheez", "Hanta", "Lassa", "Kyasanur", "Kearns", "Brodee", "Meniere", "Belle", "Amaurosis", "Sayre", "Cee-Auris", "Sella", "Kooma", "Prioon", "Gouttee", "Myotonia", "Tetann", "Tay-Sakhi", "Viliusk", "Fuwajil", "Loa\u00a0Loa", "Leigh", "Chiari", "Akinetoph", "Bartonella"
```

Gotcha: `animeNames[num]` throws `IndexOutOfRangeException` for card 72 and up. The serialized prefab list in the scene can override the code initializer. Check the scene's serialized value at runtime.

### UI elements referenced

- `picture` (Image): card art. Black when locked.
- `frame` (Image) with a material that takes `_Gradient`. The material is instanced per card and recreated on each hover (`SetCardtexture`, `AnimeCard.cs:292-298`).
- `expBar` (Image): scaled on x.
- `levelTxt`, `incomeTxt`, `girlName` (TMP): text.
- `buyButton` (legacy `Text`): cost label.
- `promoteBtn` (GameObject): shown at the cap.
- `medalTex[]` (Texture[]): gradient set per medal.
- `cardParticle` (ParticleSystem): promotion effect on the generator.
- `redeemBtn` (Button): wired in `Start`.
- `crystalMeterText` (TMP): crystal total.
- Card buttons: `BuyCard`, `PromoteCard`, `ClickedCard` are public and are presumably wired in the prefab through `Button.onClick`. The wiring is not in code (see Open questions).
- Card hover: `OnPointerEnter` / `OnPointerExit` (`AnimeCard.cs:342-350`) show the card's own art as the frame gradient while hovered.

### Expansion opportunities

Concrete ideas, with the code points you would touch.

**1. Add more cards (clone a card GameObject)**

- Duplicate an existing card under `G` (the generator) in the prefab, or create one at runtime with `Instantiate(card, G.transform)`.
- Set these values on the clone:
  - `cardId`: the next index. It must match the card's position in the hierarchy, because `BuyCard` uses it for `maxCardUnlocked` and layout.
  - `firstLoad = true`. Otherwise `Awake` skips the setup of `audios`, `animeCardGenerator`, and the `init*` values, and `ClickedCard` / `BuyCard` throw.
  - `expToLevel`, `initExpToLevel`, `income`, `initIncome`, `purchasePrice`, `initPrice`, `medalTex`, `picture`, `frame`, `expBar`, `buyButton`, `promoteBtn`, `levelTxt`, `incomeTxt`, `girlName`, and `cardParticle` references are scene-only. The clone needs its own values.
  - `audios`: three `AudioSource` components on the card (indexes 0, 1, 2).
- `nextCard` is set automatically by the generator's `Start` from hierarchy order. You do not set it by hand.
- Add a `animeNames` entry for the new index (see the gotcha above).
- Card count: `Start` runs for all cards, including inactive ones (`GetComponentsInChildren(true)`), so new clones are included without further registration.
- Layout: the new card is laid out by the 3-column math (see "Layout"). Confirm that `maxCardUnlocked` reaches the new card before expecting it to show.

**Save and load consequences of changing the card count**

- Appending cards at the end is backward compatible. Old saves have fewer `subSaveData` entries. The extra cards keep the state set by `Start` (locked, level 0) because `Load` only overwrites the saved indices (`AnimeCardGenerator.cs:86-93`).
- Inserting cards in the middle or reordering the hierarchy shifts every saved entry by position. Each card then loads another card's level, medal, exp, and price. There is no ID in the save.
- Removing cards is unsafe. A save with more entries than cards makes `cards[num++]` throw `IndexOutOfRangeException` in `AnimeCardGenerator.Load` (`AnimeCardGenerator.cs:88-91`).
- If you need to reorder, add a stable ID to `SaveDataAnimeCard` (use `cardId`) and match on it in `Load`. That needs a new save field and a fallback for old saves.

**2. Custom card art and names**

- Names are read only from `animeNames` when `Start` runs. Changing the list at runtime before `Start` works, and names are not saved, so renaming is safe for existing saves.
- Art is `picture.mainTexture` (or `picture.sprite`). Custom art is not saved. To persist it, follow the pattern of `CustomTextureSaveLoader` (`CustomTextureSaveLoader.cs:21-60`), with a path field and a `SubscribeSave` call. That is a new save entry and must be loaded before `AnimeCardGenerator.Load` restores the card, or it is overwritten.
- Medal gradients come from `medalTex[]`. Adding textures means extending the array, and it must cover indexes 0 to 4 (or 0 to 5, see Open questions).

**3. New mechanics**

- **Gacha / pulls.** Spend crystals (`AnimeCardGenerator.Crystals -= cost`) and pick a random card, or a random tier. Raise the card's exp through `AnimeCard.Exp`, or call `ClickedCard`. Tier/rarity can map to `medalLvl` or to a separate field that you add to `SaveDataAnimeCard`.
- **Rarities.** Use `medalLvl` as the rarity. It already drives the frame gradient and the level cap (`AnimeCard.cs:229`, `:259`). Note that `Locked` resets `medalLvl` to 0 when locked (`AnimeCard.cs:153`), so store extra rarity data in a separate field.
- **Auto-clickers.** `ClickedCard()` (`AnimeCard.cs:305`) is public. A timer can call it on a card, or add exp directly through the `Exp` property. Note that `ClickedCard` plays a click animation and sound on every call, which may be too noisy at high rates.
- **Crystal shop / upgrades.** Multiply `Income` (`GenerateCrystals`, `AnimeCard.cs:222-225`) or change the 0.008 click factor (`AnimeCard.cs:310`). Note that the 1.67 income factor and 1.07 price factor are literal constants in `BuyCard` (`AnimeCard.cs:242-243`).
- **Events.** There are no C# events on `AnimeCard` or `AnimeCardGenerator`. For reactions to unlock, promotion, redeem, or payout, use Harmony postfixes on `AnimeCard.BuyCard`, `AnimeCard.PromoteCard`, `AnimeCard.GenerateCrystals`, and `AnimeCardGenerator.RedeemCrystals`.

**4. Risks**

- `Exp` setter overflow (`AnimeCard.cs:132-136`): excess exp above `expToLevel` is discarded, not carried over. High frame times (large `deltaTime`) lose more.
- Promotion at medal 5 has no cap on `initIncome` and `initPrice` (see "Promotion"). Income and cost can grow without bound.
- Base values are not persisted, so reloads change the prices and income that the formula uses (see "Save format").
- Each `Awake` call on a card instantiates `frame.material` again, and each hover (`SetCardtexture`) creates a new material. This is a memory leak over long sessions.
- `Load` sets `Level` before `medalLvl` (`AnimeCard.cs:186-187`). The `promoteBtn` visibility check in the `Level` setter therefore uses the previous medal value.
- `AnimeCardGenerator.Load` calls `Start()` manually and Unity calls `Start()` again. The second call skips the one-time block (`noSaveSubscribe`) but still rewrites names and visibility. Keep this in mind when adding cards.

## Apps & AppDownloader

### Buying apps

`AppDownloader` (`AppDownloader.cs`) is a small app store on the computer.

- `apps` is filled in `Start` with `GetComponentsInChildren<Computer_Button>()` (`AppDownloader.cs:58-61`). Each `Computer_Button` under the downloader GameObject is one app entry. The array index is the app id.
- `boughtApps` is reallocated in `Start` with length `apps.Length` (`AppDownloader.cs:61`). It is saved as a `bool[]`.
- `prices[]` (float) gives the price for each index. It is not sized automatically. A `prices` array shorter than `apps` throws in `BuyApp`.
- `BuyApp(int appId, bool enableOnly)` (`AppDownloader.cs:74-100`):
  - If `Money >= prices[appId]`:
    - Unless `enableOnly`, deducts the price through `Computer.Money -=`.
    - Calls `apps[appId].ButtonCommand()`. That runs the app's own `commands` and `windows`, which normally opens the app's window.
    - Sets the button's child index 1 text to `"Owned"` (`:83`). This depends on child order.
    - Disables the button (`:84`) and sets `boughtApps[appId] = true`.
    - Plays `clips[0]`.
  - Otherwise, plays `clips[1]` (no money deducted).
- On load, `AppDownloader.Load` (`AppDownloader.cs:40-56`) calls `Start()` and then `BuyApp(i, enableOnly: true)` for every saved `true`. `enableOnly` skips the charge and the sound. Because it still calls `ButtonCommand()`, any toggling command runs again on every load.

### Known apps and screens

Each row lists what the code shows. The scene hierarchy decides exactly which objects are apps.

| Component | File | What it does |
|---|---|---|
| `Computer_Browser_Addresser` | `Computer_Browser_Addresser.cs` | Web browser. Address bar and `websites[]` pages indexed by `urls`. Home page is "metscape". |
| `EmailApp` / `Email` / `EmailHandler` | `EmailApp.cs`, `Email.cs`, `EmailHandler.cs` | Email client. See "Email, stocks, taxes". |
| `StockGraph` / `StockMarket` | `StockGraph.cs`, `StockMarket.cs` | Stock trading app. See "Email, stocks, taxes". |
| `AnimeCardGenerator` | `AnimeCardGenerator.cs` | The anime card idle game. See the deep dive. |
| `AppDownloader` | `AppDownloader.cs` | App store. Buys app windows with money. |
| `StoreGenerator` | `StoreGenerator.cs` | Online shop page. See "Online store". |
| `CustomItemShop` | `CustomItemShop.cs` | Shop that customizes items with user files. See "Online store". |
| `TaxPaymentUI` | `TaxPaymentUI.cs` | Pay tax bills. See "Email, stocks, taxes". |
| `WallpaperPicker` | `WallpaperPicker.cs` | Desktop wallpaper picker. Stores the path in `Computer.wallpaperImgPath`. |
| `Computer_FaceChanger` | `Computer_FaceChanger.cs` | Player face image and body type picker. See "Other electronics". |
| `Computer_Fish` | `Computer_Fish.cs` | Decorative swimming fish sprite on a fish tank panel. It is not a function app. |
| `Computer_Button` | `Computer_Button.cs` | Generic open/close/nav button. Used everywhere to switch windows. |

`CustomImageLoader` (`CustomImageLoader.cs`) is a helper for the custom design shop. It shows a grid of random thumbnails from `StreamingAssets/CustomDesigns`. See "Online store".

## Online store

### StoreGenerator (`StoreGenerator.cs`)

- The store is a set of `Button`s already present in the scene. `Start` (`StoreGenerator.cs:24-37`) attaches a listener to every `Button` under the store (`GetComponentsInChildren<Button>()`, no `includeInactive`). Each listener captures its own index (0, 1, 2, ...) and calls `CacheItemToOrder(index)`.
- The index is used directly on `pickupBank.pickups[i]`, the scene's `PickupBank` asset (`StoreGenerator.cs:39-43`, `:47`). The button order in the hierarchy must line up with the `pickups` list. Nothing checks that. A missing entry throws.
- `CacheItemToOrder(i)` sets the confirmation labels: name plus price, and description (both from `Usable`). It replaces the confirm button's listeners and binds `Computer.OrderItem(pickups[cachedIdToOrder].obj)` (`StoreGenerator.cs:45-48`). Note the bind uses the stored index, not the captured one.
- `storeType` (`Usable.StoreType`) is declared on `StoreGenerator` but is not read in this file. The purchase rules come from each item's own `Usable.purchaseableAt` in `Computer.OrderItem`.
- `OnEnable` resizes the store's parent to `176.7 x 1233` and its grandparent's height to 1233 (`StoreGenerator.cs:51-55`).
- `PickupBank` (`PickupBank.cs`) is a `ScriptableObject` with `pickups: List<Spawnable>`. `pickupsDict` is a static dictionary built from each bank's `pickups` on `Init()` (`PickupBank.cs:30-41`). Keys are object names. Do not add two items with the same `obj.name`, because `Add` throws.

### CustomItemShop (`CustomItemShop.cs`)

This is the "customize your item" shop. The player picks a file, then buys an item with the file applied.

- `customPickups` is a `PickupBank`. `customItemButtons` are the shop buttons (`CustomItemShop.cs:29-47`). Each button index selects a pickup.
- `filterForMusic` picks the mode:
  - **Texture mode** (false): `FileBrowserEnable` opens a SimpleFileBrowser dialog for `.jpg` and `.png`, starting in `StreamingAssets\CustomDesigns\` (`CustomItemShop.cs:63-71`). The chosen file is previewed in `previewImg` and saved as `latestFilePath`. The path is the original file path, not a copy in the game folder.
  - **Music mode** (true): opens the dialog for audio extensions, starting in `StreamingAssets\CustomAudio\` (`CustomItemShop.cs:51-60`). The chosen file is loaded as an `AudioClip` for preview, and the path is saved as `latestFilePath`. Again, this is the original file path.
- `CacheItemToOrder(i, type)` (`CustomItemShop.cs:75-95`) sets labels. In texture mode the shown price is `price + 10`. In music mode the shown price is just `price`. **Mismatch:** both modes call `OrderItem(..., priceOffset: 10f, ...)` (`CustomItemShop.cs:85`, `:91`), so the music item is charged `price + 10` while the label says `price`.
- Applying the customization happens when the box is unboxed, through `OnUnboxingApplyToContents`:
  - Texture: `CustomizeItem` (`CustomItemShop.cs:97-111`) sets `_MainTex` on the first `Renderer` of the item through a `MaterialPropertyBlock`, using `previewImg.mainTexture`. It also sets the child `CustomTextureSaveLoader.persistentTexturePath` to `latestFilePath` if that component exists.
  - Music: `CustomizeMediaURLsOnItem` (`CustomItemShop.cs:113-121`) sets `CustomMusicSaveLoader.persistentMusicPath` and calls `CustomizeItem`, which loads the clip into `Cassette.musicClip`.
- Note that `previewImg.mainTexture` is applied directly, but the save path comes from `latestFilePath`. If the user picks a new file after the preview, the texture and the saved path can disagree.

### CustomImageLoader (`CustomImageLoader.cs`)

- `FileBrowserEnable` opens an image picker. `CopyFilesToStreamingAssets` copies each chosen file into `StreamingAssets\CustomDesigns\` (`CustomImageLoader.cs:29-42`). This is the only code that copies user art into the game folder.
- `LoadBackgroundImages` (`CustomImageLoader.cs:49-83`) destroys the grid children, reads `CustomDesigns/` for `.png` and `.jpg`, picks 4 random textures, and fills the grid with 18 `Image`s, each using one of the 4 textures at alpha 0.4. The texture picks are random with replacement.
- `OnActive()` (`CustomImageLoader.cs:44`) is not a Unity message. It is never called by Unity, so the grid is not refreshed on enable. Treat it as dead code unless something calls it by name.
- Copying uses backslash paths, so this is Windows-only as written.

### Persistence

- `CustomTextureSaveLoader` (`CustomTextureSaveLoader.cs`) stores `persistentTexturePath` (an absolute file path, or a `Resources` name when `getTexFromResources` is true). On load, it reads the file with `UnityWebRequestTexture` and sets `_MainTex` through `Renderer.material` (`:73-86`). If the path is empty, `LateUpdate` destroys the component (`:34-40`).
- `CustomMusicSaveLoader` (`CustomMusicSaveLoader.cs`) does the same for audio. It stores `persistentMusicPath`, loads the clip with `UnityWebRequestMultimedia` and `Commons.GetAudioType`, and assigns `Cassette.musicClip` (`:61-71`). An empty path destroys the component (`:36-42`).
- Both loaders save an absolute path, not a copy. A moved or deleted source file silently fails to load after the next game start (the code logs nothing and leaves the default texture or no clip).
- The `StreamingAssets/CustomAudio` folder is only the browser's start folder. Nothing copies audio there.
- `StreamingAssets/CustomDesigns` is the start folder for the texture picker, and the target of `CustomImageLoader`'s copies. The two uses are not connected in code.

## Email, stocks, taxes

### Email (`EmailHandler.cs`, `EmailApp.cs`, `Email.cs`)

- `EmailHandler` is the singleton `EmailHandler._` (`EmailHandler.cs:60`, set in `Awake`, `:99-113`). It holds an Ink `Story` built from `masterEmails` (`:117`). Each email is an Ink knot, referenced by `KnotList.Knot` (fields `title`, `inkName`, `read`).
- `pendingEmails` is a `Queue`. `deliveredEmails` is a list. `SendEmail` enqueues and fires `OnEmailReceived` (`EmailHandler.cs:155-162`). `EmailApp.UpdateInbox` moves all pending mail to `deliveredEmails` and creates a button for each (`EmailApp.cs:24-30`, `:32-42`).
- **Random mail.** `SendSpam` (`EmailHandler.cs:131-153`) first waits 15 seconds, sends one random unlocked knot from `randomEmailsInstance`, and then waits `Random.Range(200, 550)` seconds before the next one. It stops when `UnlockedKnots()` is empty (`:145-148`).
- **Opening mail.** `Email.OpenEmail` (`Email.cs:19-27`) calls `EmailApp.LoadEmail`, which runs `masterStory.ChoosePathString(knot.inkName)` and writes the text to `emailBody` (`EmailApp.cs:56-64`). The `^N` and `^n` markers are turned into newlines. `read` is set to true and the icon is swapped to `readIcon`.
- **Save and load.** `EmailHandler.Save` (`EmailHandler.cs:62-65`) writes `randomEmails` (the asset reference), not `randomEmailsInstance` (the runtime copy created in `Awake`), so sent spam comes again after a load. `SaveDataEmailHandler`'s constructor (`EmailHandler.cs:23-29`) never assigns `timeToNextRandom`, so the spam timer starts over (`Load` restarts `SendSpam` with no remaining delay, `:96`). `namesOpened` is never assigned.
- **Start order.** The save loads in `SaveLoadHandler.Start`, before `EmailHandler.Start` and `EmailApp.Start` run. So `EmailHandler.Start` starts a second spam coroutine after `Load` started one, and `EmailApp` hasn't subscribed to `OnPostLoad` yet, so the inbox isn't rebuilt. These two and the two save bugs above are fixed in Unofficial NEET Patches (see `known-bugs.md`).
- **Modded emails** are the Email Expansion mod (`docs/modding/email.md`): knots grafted into `masterStory` with `InkStory.Email`, sent with `SendEmail`, read by a prefix on `EmailApp.LoadEmail`.

### Stocks (`StockMarket.cs`, `Stock.cs`, `StockGraph.cs`)

**Market**

- `StockMarket` is a singleton (`StockMarket._`). `Awake` builds 15 stocks: CUMY, GRUG, CUMS, GABN, KEEP, DUNG, EFLO, ZOOP, CRAB, SUNY, PPER, NIGT, MORN, DUSK, KATT (`StockMarket.cs:45`).
- `UpdatePrices` runs forever as a coroutine (`StockMarket.cs:87-97`). Every 0.9 seconds, each stock calls `ShiftPriceForward()`.
- `maximumAllowedPrice = 10000` and `priceHistoryCount = 100` (`StockMarket.cs:25-27`). `priceHistoryCount` is not used in this file. The history length is 100 in `Stock.LoadPrices` (`Stock.cs:155`).

**Buying and selling**

- `BuyStock(ticker, qty, free)` (`StockMarket.cs:99-115`) needs `Money >= currentPrice * qty` (or `free`). It deducts `Money` and adds `qty` to `quantityOwned`. It returns the cost, or 0 on failure. Unknown tickers throw (dictionary indexer).
- `SellStock` (`StockMarket.cs:117-133`) needs `quantityOwned >= qty` (or `free`). It credits `Money += currentPrice * qty` and subtracts `qty`. Because `Money` gains are taxed, a sale's proceeds add 12% of the gain to `Taxes` (`Computer.cs:121-127`). The tax is on the full proceeds, not only the profit.
- `free: true` is used only during load (`StockMarket.cs:61-74`).

**Price formula**

- Each stock has a 100-value price list, `prices`. `currentPrice` is the last value (`Stock.cs:80`).
- Each tick (`ShiftPriceForward`, `Stock.cs:210-219`) removes the oldest value, appends `GenerateNewPrice()`, and fires `OnPriceChanged`.
- `GenerateNewPrice()` (`Stock.cs:221-224`):

  `P_new = clamp( P_last * (1 + X / 30), 0, 10000 )`

  where `X` is drawn from the stock's profile:

  | Profile | Draw for X (`Stock.cs:56-78`) |
  |---|---|
  | Normal | `N(0, 0.1)` |
  | Stable | `N(0, 0.04)` |
  | Volatile | `N(0, 0.25)` |
  | Doomed | `RandomRangeSlope(-0.3, 0.12, 1, Left)` (skewed draw in [-0.3, 0.12]) |
  | RisingStar | `RandomRangeSlope(-0.12, 0.3, 1, Right)` (skewed draw in [-0.12, 0.3]) |

  `RandomRangeSlope` is in `NormalDistribution.cs:60-75`. It returns `min + t * (max - min)` for a skewed `t` in [0, 1], pushed toward one end.

- The profile is chosen once per stock (`Stock.cs:116-151`): 8% RisingStar or Doomed (50/50), 32% Volatile (65%) or Stable (35%), and 60% Normal.
- The first price is drawn from `RandomRangeSlope(1, 2500, 400, Right)` and clamped (`Stock.cs:161`). The history is then filled by the same random walk (`:155-175`).
- `min` and `max` are maintained on each `AddPrice`. They are used for the graph scaling.

**Saving**

- Each stock saves only `ticker`, `lastPrice` (current price), `currentProfile`, and `quantityOwned` (`Stock.cs:9-27`, `:104-107`).
- On load, the 100-value history is regenerated around `lastPrice` (`Stock.cs:109-114`, `:116`). The past history is not restored.

**Graph** (`StockGraph.cs`)

- The graph is a `LineRenderer` with 100 points, x spacing 1.3, y scaled by `(price - min) / (max - min) * 100 / max` (`StockGraph.cs:156-175`). It is green when the last price is above the previous one, red otherwise.
- `LoadTickerButtons` (`StockGraph.cs:50-94`) builds one button per stock. It subscribes `OnPriceChanged`, `OnBought`, and `OnSold` on each stock to update the labels. The first stock is focused on start.
- `ticker (N)` shows the quantity owned. The cash label is `"$$$CASH$$$"` plus the money, rounded to 2 decimals (`StockGraph.cs:96-99`).
- `BuyFocusedStock(qty)` and `SellFocusedStock(qty)` call the market and play the confirm or cancel sound (`StockGraph.cs:101-127`).
- Hooks: `OnPreLoad` clears the buttons, `OnPostLoad` rebuilds them and unfocuses (`StockGraph.cs:29-38`).
- Note: `LoadTickerButtons` subscribes to `mainComputer.OnMoneyChanged` each time it runs (`StockGraph.cs:91-93`). `OnPostLoad` runs it repeatedly, so the cash label callback is registered multiple times.

### Taxes (`TaxHandler.cs`, `TaxBill.cs`, `TaxPaymentUI.cs`)

**Accrual**

- Taxes come from `Computer.Taxes`, which grows at 12% of positive money changes (see "Money").

**Bill creation** (`TaxHandler.Update`, `TaxHandler.cs:80-95`)

- `nextBillTimer` counts down in game time. When it is `<= 0` and `Taxes >= 1`, a bill is spawned and the timer is set to `Random.Range(1300, 1600)` seconds (`:89`).
- If `Taxes < 1` when the timer expires, the bill is delayed. The check repeats every frame until taxes reach 1 (`:86`).
- `SpawnTaxBill` (`TaxHandler.cs:97-116`) instantiates `billObj` at `packageDropOffPoint`. The bill's amount is the whole current `Taxes`. Then `Taxes -= amount`, which resets `Taxes` to about 0 (`:115`). The bill's `OnSpawn` hook writes the text `"Your amount in taxes due is:" + amount + "$"` and sets the stage material (`:103-114`).

**Bill stages** (`TaxBill.cs:37-67`)

- Each bill has a timer that starts at 666.66 and counts down in game time (`TaxHandler.cs:94`, `TaxBill.cs:72`). When the timer reaches 0, `stage` increases and the timer resets to 666.66.
- Stages 1 and 2 switch the bill's material to `stageMaterials[stage]`.
- When stage reaches 3, the bill is removed from `taxBills`, `HouseManager._.SpawnCopVan()` is called, a fire effect plays, and the bill object is destroyed.
- The full sequence from spawn to police is about 3 x 666.66 = 2000 seconds of game time.
- There is no money penalty. The penalty is the cop van.

**Paying** (`TaxPaymentUI.cs`)

- `TaxPaymentUI.OnEnable` (`:17-21`) rebuilds one button per bill. Each button shows `"Pay\n" + amount` and is colored by `stageButtonColors[min(2, stage)]`.
- `PayBill(billId)` (`:47-56`) requires `Money >= amount`, deducts it through `Money -=` (no tax accrual on the negative change), removes the bill from the list, and plays the sound on `GetComponents<AudioSource>()[1]`.
- Paying does not destroy the bill's GameObject. Whether the physical bill is removed elsewhere is not in this file. See Open questions.

**Save and load**

- `TaxHandler.Save` (`TaxHandler.cs:30-33`) saves `nextBillTimer` and each bill (`TaxBill.Save`, `TaxBill.cs:77-80`, which saves the bill object's `Usable.saveId`, `timer`, `stage`, `amount`).
- `TaxHandler.Load` (`TaxHandler.cs:35-68`) recreates each bill with `new TaxBill(this, amount, loadedUsablesById[id])`. The constructor sets `Timer = 666.66`, so **the saved timer is ignored and every loaded bill restarts its stage timer**. The saved `stage` is restored.
- `Computer.Taxes` itself is not saved. Only the bills are. After a load, accrued taxes start at 0 and the value that was accrued is lost. See Modding notes for the money-setter effect on load.

## Other electronics

These are the non-computer devices in this area. None of them stores money or the card game.

**Computer_PowerButton** (`Computer_PowerButton.cs`)

- Power switch for the computer. Saves `isPowered`, `isOn` (whether the canvas is active), `money`, and `wallpaperImgPath` (`:9-28`, `:62-65`). Load sets the money through `Computer.Money`, which fires the tax and achievement subscribers.
- `BootSequence` (`:137-184`) fades an audio clip in and out with random waits (0.7 to 1.3 seconds, 2 to 4 seconds, 1 to 3 seconds). `instant` skips the animation. At the end it calls `FileBrowser.HideDialog()`.
- `BootCursorFlash` (`:186-202`) blinks a boot cursor at random positions. This is cosmetic.

**Computer_FaceChanger** (`Computer_FaceChanger.cs`)

- Lets the player pick a face image. The image is saved into `StreamingAssets\Faces\Face.<ext>`. The old file is renamed with a timestamp first (`:57-87`). The image is shown on `faceImg`, and `PlayerStats.SetPlayerFace()` is called.
- `ResetFaceFile` renames the current face and writes the `Resources` "Face" texture back to `dataPath/StreamingAssets/Faces/Face.jpg` (`:89-112`).
- `PickBodyType(b)` sets `PlayerStats.stats[typeof(BodyType)]` and `PlayerPrefs.SetFloat("DesiredBodyType", b)` (`:120-124`). The `BodyType` stat is defined elsewhere.
- Paths use backslashes (Windows only as written).

**Computer_Fish** (`Computer_Fish.cs`)

- A decorative sprite that swims inside its parent RectTransform. Each swim picks a random target in x [-40, 40] and y [-32, 35] (`:44`), moves at a random speed of 3, 4, or 5 (`:24`), then pauses 0 to 4 seconds (`:37`). It has no gameplay effect.

**WallpaperPicker** (`WallpaperPicker.cs`)

- Opens an image picker. `LoadWallpaper(path)` loads the texture with `UnityWebRequestTexture` and sets `wallpaperImg`. Only after a successful load is `Computer.wallpaperImgPath` set to the path (`:58`). Saved and reloaded by `Computer_PowerButton`.
- An empty path clears the image and sets the default blue color (`:39-44`).
- `CopyFilesToStreamingAssets` does not copy. It only loads from the original path. The name is misleading.

**Keyboard and Keyboard_Key** (`Keyboard.cs`, `Keyboard_Key.cs`)

- See "Ordering and text entry" under the overview. Keys are `Usable` objects with `IUsable.Use`. `Keyboard_Key.Use` injects the character into the current `InputField`.

**Phone** (`Phone_Interface.cs`, `Phone_Dispatch.cs`, `Phone_Button.cs`)

- `Phone_Button` (`Phone_Button.cs:7-13`): a button with `buttonId`. On collision with a `Usable`, it invokes the `Usable.useEvent`. Each phone button's `Usable.OnUse` calls `ComposeNumber(buttonId)` (`Phone_Interface.cs:59-67`).
- `buttonId` values: -1 is call/answer, -2 is clear, other values are digits 0 to 9. Digits are not checked beyond the phone button id.
- `ComposeNumber` (`Phone_Interface.cs:70-98`) adds digits up to 8 characters (with a dash after the third digit), but only when the player is within 2.2 units of the phone.
- `Call` and `CallRoute` (`:126-165`):
  - Fifteen numbers are known: `411, 911, 999, 4206969, 1111111, 6666666, 3333333, 3141592, 5552368, 5555555, 5551234, 4279683, 9829643, 6686262, 5551337` (`:43-47`).
  - A known number rings for 1 to 3 seconds, then starts the dialogue `Phone.p<digits>` (`:161-163`).
  - Any other number rings for 5 to 8 seconds, then plays a disconnect tone (`:154-158`).
- `Phone_Dispatch` (`Phone_Dispatch.cs`) schedules prank calls with `SchedulePrankCall`. The first is after 275 to 700 seconds. The ring lasts 20 to 30 seconds. Each phone gets a `prankId` between 1 and 11 (`:79-83`) and plays the dialogue `Phone.pPrank<id>` if answered (`Phone_Interface.cs:212`). Prank calls are skipped if a call is in progress, and rescheduled.
- `LineIsBusy` (`Phone_Dispatch.cs:16-33`) stops or restarts scheduling. `AnsweredPhone` stops all ringing and prank coroutines (`:35-53`).
- Each handset rolls its own `prankId`, so which prank plays depends on which phone is picked up.
- The phones are `Phone` (upstairs lobby) and `Phone (1)` (kitchen). The screen is a `Text` (`Phone_Canvas/Phone_GreyScreen/Phone_ScreenText`) whose idle text, `initText`, is "gud fone". Each handset has four `AudioSource`s: 0 voice, 1 button beep, 2 ringing, 3 busy tone.
- `Call` dials whatever is on the screen if it isn't `initText`, so anything else shown on the screen while a call rings would be dialed by the call button.
- The lines are in the Ink story (the `Phone` knot of the master story, one stitch per number), not in this code. All of `Phone.pPrank1` to `Phone.pPrank11` exist. Every call ends with the `EndPhoneCall` tag on an empty line after the last line, so the call hangs up when the player presses use on the last line.

What each number is, from the compiled story:

| Number | Knot | Who |
| --- | --- | --- |
| 411 | `p411` | Directory assistance, shut down for good. |
| 911 | `p911` | The police, unhelpfully. |
| 999 | `p999` | A British cop asking for a licence. |
| 420-6969 | `p4206969` | A dealer. |
| 111-1111 | `p1111111` | Someone tired of getting calls on that number. Hangs up. |
| 666-6666 | `p6666666` | Satan, who hangs up quickly. |
| 333-3333 | `p3333333` | God. |
| 314-1592 | `p3141592` | The pie store; delivery in pi weeks. |
| 555-2368 | `p5552368` | Spookbusters: "call later". |
| 555-5555 | `p5555555` | The movie phone number registry. |
| 555-1234 | `p5551234` | "The bird isn't here." |
| 427-9683 | `p4279683` | A stranger who knew dad: "Don't use the machine." Runs `TriggerSequence("Enable", "EventConnector_NakedTrigger_DiningWindow")`. |
| 982-9643 | `p9829643` | A scammer. Has choices; giving him a card number calls `SetMoney(0)`. |
| 668-6262 | `p6686262` | Mom. Diverts to `mom_fake2` once `PoopmanNight1` was visited, or `mom_fake1` once `EmoNight1` was. |
| 555-1337 | `p5551337` | Gamer tips hotline. |

Of the 11 pranks only `pPrank3` has choices. The Phone Expansion mod adds numbers and calls on top of these (`docs/modding/phone.md`).

**TV** (`TV.cs`, `TV_Remote.cs`, `ScreenModule.cs`)

- `TV` (`TV.cs:4-206`) has `ScreenModule` children as channels, in hierarchy order. It sets `HouseManager._.tv` in `Awake` (`:101-117`).
- `Control(int)` (`:140-205`):
  - `-1` toggles power (on goes to channel 0).
  - `-2` turns on and goes to channel 0.
  - `-3` turns off.
  - Any other value, while on, switches to that channel. Out-of-range values fall back to channel 0.
- Switching channels disables all channels, plays the channel's `music` clip at a random time, and calls `InitScreen()`.
- `TV_Remote` (`TV_Remote.cs`): a usable remote. Releasing it sends `-1` (toggle). Punching it sends `++displayedScreenModule` (next channel).
- `ScreenModule` (`ScreenModule.cs:4-31`): base class for channels. `InitScreen` starts a loop that plays a random `randomClips` sound every 3 to 19 seconds (`Random.Range(3, 20)` on ints).
- `TV_Bingo` (`TV_Bingo.cs`): shows a random number from 0 to 100 once per second, in red or blue.
- `TV_Snail` (`TV_Snail.cs`): an animated snail screen. After `Random.Range(4, 4)` (always 4) seconds it plays a clip, speeds up the grass and the snail animation for 3 seconds and turns the snails red, then repeats the cycle (`TV_Snail.cs:31-43`).
- In the house the TV is `HOUSE/FlatScreen/FlatScreen/Canvas`: a world-space canvas (facing -Z) with the `TV`, an `AudioSource` and six channel children, `Channel1_Static`, `Channel2_Bingo`, `Channel3_Snail`, `Channel4_Church`, `Channel5_Sports` and `Channel6_Rap`. Each is a full-screen UI `Image` with a `ScreenModule`; the channel list is private (`TV.channels`) and filled once in `Awake` with `GetComponentsInChildren<ScreenModule>()`.
- The remote has `Remote_Btn_Power` and `Remote_Btn_Channel (1)` to `(6)`, which pick channels 0 to 5 of the list. Channels after the sixth are only reached by hitting the remote (next channel). The Custom TV Stations mod adds video channels to the list (`docs/modding/tv-stations.md`).
- `Screensaver` (`Screensaver.cs`): the computer's screensaver. `Update` applies a texture offset or scale animation. `currAnim = (ScreenAnim)Random.Range(2, 3)` always yields `Zoom` (`:53`), because the int range upper bound is exclusive. Its timer is `Random.Range(3f, 3f)` (always 3 seconds). So the screensaver is always the zoom effect.

**Radio** (`Radio.cs`)

- A battery-powered radio. Power comes from a `Fuse` (`Powered` is true when `Fuse.BatteryPower > 0`, `:78-88`). Removing the fuse stops the music (`:58-76`, `PunchTrigger` at `:186-195`).
- It has two `AudioSource`s: `musicAudio` (index 0) and `clickAudio` (index 1).
- `on` toggles through `MusicSwitch`. Music plays only when powered, switched on, and not already playing (`canStartMusic`, `:90-100`).
- `Opened` (`:32-56`) ejects the cassette when opened. A cassette dropped into an open radio is inserted automatically through `OnTriggerEnter` (`:197-207`).
- `InsertCassette` (`:102-118`) copies the cassette's `musicClip` into `musicAudio.clip`. A mouth animation follows the spectrum of the music (`:151-159`).

**Cassette** (`Cassette.cs`)

- A holds a `musicClip`. Using it while holding an open `Radio` in the other hand inserts it (`:9-21`).
- This is the object that custom music is applied to (see "Online store").

**PhotoCamera and Picture** (`PhotoCamera.cs`, `Picture.cs`)

- `PhotoCamera.Use` (`:18-27`) has a 1.8 second cooldown. `PrintPicture` (`:29-50`) renders a child `Camera` to a 512x512 `RenderTexture`, reads it into a `Texture2D`, and writes the bytes to `StreamingAssets/photograph_<n>.jpg`. The bytes come from `EncodeToPNG`, so the file is PNG data with a `.jpg` extension (`:38-39`).
- A new `picPrefab` (a `Picture`) is spawned and put in the other hand if free. `loadImage(Path, fadeIn: true)` loads it and fades it in (`:44-49`).
- `Picture` (`Picture.cs:6-82`) stores `storedImagePath`. Save writes it with the `Usable.saveId` (`:32-35`). On load, `Start` calls `loadImage()` (`:24-30`, `:44-81`). Note the path logic (see Open questions): the photo branch stores the full path, and the reload branch expects a name.
- `loadImage` uses the deprecated `WWW` class (`:63`).

## Modding notes

### Add a store item to an existing store page at runtime

- Items are referenced by index. `StoreGenerator.CacheItemToOrder(i)` reads `pickupBank.pickups[i]` (`StoreGenerator.cs:39-43`). To add an item you must:
  1. Add a `Spawnable` to the store's `PickupBank.pickups` list. This is a `ScriptableObject`, so the change exists only in memory unless you save it. Add it at the end, so existing indices stay valid.
  2. Add a button to the store's hierarchy, below the others, so its index matches. Clone an existing store `Button` and its label.
  3. Attach the listener. `StoreGenerator.Start` attaches listeners only to buttons that exist when `Start` runs, and `GetComponentsInChildren<Button>()` excludes inactive ones. For a button added later, call `StoreGenerator.CacheItemToOrder(index)` from your own `onClick` listener.
  4. Give the item a `Usable` with `price`, `description`, and `purchaseableAt`. `Computer.OrderItem` reads all three (`Computer.cs:245-249`).
- The `pickupsDict` (`PickupBank.cs:19`, `:30-41`) is built once, by name, from the first `Init()` call. Items added later are not in it, so `PickupBank.GetSpawnable(name)` and the dev console spawn commands will not find them. Add them to `pickupsDict` yourself.
- Confirmation and delivery use `pickups[i].obj` directly. `OrderItem` puts that object into the box (`Computer.cs:253`). Whether the box instantiates a copy on unboxing is in `Cardboard_Box.cs`, which was not read. Verify before relying on it.
- The StoreGenerator's `OnEnable` sizing (`176.7 x 1233`) is fixed. A longer store needs a different height, or the grid will overflow its frame.

### Add a new app or screen to the computer

1. Add a window GameObject under the computer canvas, sized to match the other windows.
2. Add a `Computer_Button` to the app store (`AppDownloader`'s hierarchy), so `apps` gets one more entry. Its `windows` and `commands` arrays must have matching lengths. The app's `commands` run on purchase (`AppDownloader.cs:82`).
3. Extend `AppDownloader.prices` with one entry for the new app. `prices[appId]` throws if it is too short.
4. `boughtApps` is sized from `apps.Length` on `Start`. Old saves have a shorter `boughtApps` array. The new app is not owned in those saves, which is correct.
5. Give the window its own `Button`s. `Computer.RegisterButtonClickAudio(Button)` (`Computer.cs:224-230`) adds the click sound. Buttons under the computer at `Start` are already wired by `RegisterButtonsClickAudio`.
6. For a browser site, add its name to `Computer_Browser_Addresser.urls` and its window to `websites[]` at the same index. Lookup uses `urls.IndexOf`, so the lists must stay aligned.
7. For an app with its own save data, implement `ISaveableObj`/`ISaveable` and subscribe with `SaveLoadHandler._.SubscribeSave` (the pattern used in `AppDownloader.Start`, `AppDownloader.cs:62-66`). Add a load call to `SaveLoadHandler.LoadComputer` (`SaveLoadHandler.cs:374-400`). Without that, the state is never restored.

### Harmony patch targets

| Target | Why |
|---|---|
| `Computer.set_Money` (property setter) | Every money change goes through it. Postfix for logging or achievements. A prefix can override the amount or block it. |
| `Computer.OrderItem` | Prefix to change price, block orders, or change the cop van roll. Note the cop van roll happens before the affordability check. |
| `Computer.Start` | Postfix to add subscribers to `OnMoneyChanged`. Subscribers are added only once (`alreadyStarted`). |
| `Computer.Update` | Cursor and screensaver logic. Postfix to change the 30 second screensaver rule. |
| `Computer_PowerButton.BootSequence` | Skip or change the boot time. |
| `AnimeCard.BuyCard`, `AnimeCard.PromoteCard`, `AnimeCard.GenerateCrystals`, `AnimeCard.ClickedCard` | The card economy. Postfix for events, prefix for cost changes. |
| `AnimeCardGenerator.RedeemCrystals` | Change the 10000 crystal rate or the 3 money reward. |
| `AnimeCardGenerator.Load` / `Save` | Add extra save data for new mechanics. |
| `StockMarket.BuyStock` / `SellStock` | Trade hooks. |
| `Stock.GenerateNewPrice` | Change the market simulation. |
| `TaxHandler.SpawnTaxBill` / `TaxBill.Timer` (setter) | Tax bill timing and cop van. |
| `Computer_Browser_Addresser.AddressWrapper` | Browser routing. |

### Events to subscribe

- `Computer.OnMoneyChanged(float prev, float next)`: fires on every money change. Subscribe to react to income or spending.
- `Computer.OnTaxesChanged(float prev, float next)`: fires on every tax change.
- `EmailHandler.OnEmailReceived`, `OnPreLoad`, `OnPostLoad`: email lifecycle.
- `StockMarket.OnPreLoad` / `OnPostLoad`: stock lifecycle.
- `Stock.OnPriceChanged(float)`, `OnBought(int)`, `OnSold(int)`: per-stock events.
- `Usable.OnUse` (used by phone buttons, `Phone_Interface.cs:63`).
- Be careful with duplicate subscription. `StockGraph.LoadTickerButtons` subscribes to `OnMoneyChanged` every time it runs (see Stocks). Unsubscribe on your side if you rebuild.

### How a dev tool should read and set money and crystals

- **Money (read):** `HouseManager._.mainComputer.Money`. Also `Computer.Taxes` for tax.
- **Money (set, normal):** `HouseManager._.mainComputer.Money = value`. This fires `OnMoneyChanged`, so the 12% tax accrues on a positive change. Setting a value above the current one adds tax to `Taxes`.
- **Money (set, no tax):** the setter always accrues. To avoid it, set the private `money` field through reflection (`Computer.money`, `Computer.cs:54`), or temporarily unsubscribe the tax delegate. The tax delegate is an anonymous method, so you cannot unsubscribe it by name. Use reflection on the field, then update the text yourself (`moneyAmtAppText`).
- **Crystals (read):** `AnimeCardGenerator.Crystals`. Reach the generator through `HouseManager._.mainComputer.animeCardGenerator`.
- **Crystals (set):** the setter (`AnimeCardGenerator.cs:61-65`) updates the text and has no events. It is safe to set directly.
- **Card state:** each card is in `AnimeCardGenerator`'s `cards` array (private). Read `Level`, `Locked`, `Income`, and `medalLvl`. Setting `Level` or `Locked` directly will not run the full unlock logic, so prefer calling `BuyCard` or `PromoteCard`.
- **Taxes:** `Computer.Taxes`. Setting it does not change any bill.
- Always check `HouseManager._ != null` and `HouseManager._.mainComputer != null`.

### Gotchas

- **Index-based lookups everywhere.** Changing a list shifts meaning:
  - `StoreGenerator` buttons vs `PickupBank.pickups` (`StoreGenerator.cs:24-47`).
  - `AnimeCardGenerator.animeNames[num]` and the saved `subSaveData` (`AnimeCardGenerator.cs:118`, `:88-91`).
  - `AppDownloader.apps` vs `prices` vs `boughtApps` (`AppDownloader.cs:58-100`).
  - `Computer_Button.commands` vs `windows` (`Computer_Button.cs:41-63`).
  - `Computer_Browser_Addresser.urls` vs `websites` (`Computer_Browser_Addresser.cs:79-81`).
  - `TV.channels` (hierarchy order of `ScreenModule`s, `TV.cs:112`).
  - `Phone_Dispatch.phones` and `phones[i]` (`Phone_Dispatch.cs:43-53`).
- **Hard-coded sizes.** These will break if the layout changes:
  - Cursor clamp `(-45..45, -38..38)` (`Computer.cs:195`), duplicated in `Computer_FaceChanger.cs:44`.
  - Screensaver and cursor hit rectangle, same numbers.
  - Card grid: `176.7`, `170`, `65`, `3` (`AnimeCardGenerator.cs:151-152`, `AnimeCard.cs:249-252`).
  - Store grid: `176.7 x 1233` (`StoreGenerator.cs:53-54`).
  - Fish bounds `(-40..40, -32..35)` (`Computer_Fish.cs:44`).
  - Phone proximity `2.2` (`Phone_Interface.cs:83`, `:107`).
  - Money tax rate `0.12` (`Computer.cs:125`).
  - Card price and income factors `1.07`, `1.67`, `0.008`, `50` per tier (`AnimeCard.cs:229`, `:242`, `:243`, `:310`).
- **Money setter side effects.** Every positive change accrues tax. This applies to save loads (`Computer_PowerButton.Load`, `Computer_PowerButton.cs:81`). Loading a save with more money than the current value adds 12% of the difference to `Taxes`, and `Taxes` is not saved. The net effect on load is that taxes can be inflated. Check at runtime.
- **Save load order matters.** `AnimeCardGenerator.Load` depends on `Start` having run, and on `cards` being filled. `Computer.Start` must have run before `Money` is set (it is called manually in `Computer_PowerButton.Load`, `:71`).
- **Duplicate singletons.** `EmailHandler` and `StockMarket` only warn on duplicates (`EmailHandler.cs:104-111`, `StockMarket.cs:37-41`). A second instance is not fully disabled.
- **Windows paths.** `CustomImageLoader`, `CustomItemShop`, `Computer_FaceChanger`, and `PhotoCamera` (`Application.streamingAssetsPath + "\\..."`, `"/StreamingAssets/..."`) mix separators.
- **Dead code.** `CustomImageLoader.OnActive` is never called by Unity.
- **Debug output.** `Stock.LoadPrices` writes to `System.Console` for `DUSK` (`Stock.cs:167`). Harmless, but noisy.
- **`Random.Range` with ints.** Several calls pass ints, so the upper bound is exclusive. Examples: `Screensaver.cs:53` (always 2 = Zoom), `TV_Snail.cs:33` (always 4), `ScreenModule.cs:24` (3 to 19), `Phone_Dispatch.cs:68` (float range, so 275 to 700), `AnimeCard.cs:320` (-5 to 4). Keep this in mind when changing values.

## Open questions

These depend on the scene hierarchy, serialized prefab values, or code that was not read. Check them at runtime.

**Anime card game**

1. Number of cards in the scene. `animeNames` has 72 entries. If the scene has more cards, `Start` throws on the extra index.
2. Whether the serialized `animeNames` list in the scene matches the code initializer (Unity serialization can override it).
3. Length of each card's `medalTex` array. `Awake` reads `medalTex[medalLvl]` for medals up to 5. If there are only 5 entries, a saved medal of 5 throws.
4. Initial values per card: `expToLevel`, `income`, `purchasePrice`, `cardId`, `firstLoad`, and whether `maxCardUnlocked` in the scene is 3.
5. What wires `BuyCard`, `PromoteCard`, `ClickedCard` to the UI (`Button.onClick` in the prefab, presumably).
6. Whether the card's `cardId` matches its hierarchy index in every card.
7. Whether the `Exp` overflow and the frame-rate dependence matter in practice.
8. Confirm the base-value reset on reload (`initIncome` and `initPrice` after promotion). If it happens, a promoted card's next purchase will be cheaper and its income will drop.
9. Whether the generator's `animeCard` template field is used by anything outside this file.

**Computer and apps**

10. The `Computer` GameObject: which Usable it belongs to, its `CanvasGroup`, and the trigger collider (`Computer.cs:112`, `:162-184`).
11. `Computer_PowerButton.compCanvas` and the power/fuse relationship (`IElectronic`, not read).
12. The screensaver condition. Confirm in play that the screensaver turns off only when the cursor is inside the rectangle.
13. `AppDownloader.apps` order and whether each app button's child index 1 is a `Text` (`AppDownloader.cs:83`).
14. Whether `Computer_Button.commands` is always the same length as `windows` (an IndexOutOfRange otherwise).
15. The `Computer_Browser_Addresser.urls` list contents, and which websites exist.

**Store and custom items**

16. Whether `Cardboard_Box` instantiates the `Spawnable.obj` copy on unboxing. If not, the store item would be moved, not copied (`Cardboard_Box.cs`, not read).
17. What `Cardboard_Box.OnUnboxingApplyToContents` receives (the new object, or the box content).
18. The `Commons.GetAudioType` mapping for extensions, and whether `.flac`, `.aiff`, `.mod`, `.it`, `.s3m`, and `.xm` load through `UnityWebRequestMultimedia`.
19. Whether the `CustomDesigns` and `CustomAudio` folders exist in the build's `StreamingAssets` folder.

**Email, stocks, taxes**

20. Whether `EmailHandler.Save` should write `randomEmailsInstance` instead of `randomEmails` (`EmailHandler.cs:64`).
21. Whether the `timeToNextRandom` save bug matters in practice (it is always saved as 0).
22. What `KnotList.GetRandomKnotAndLock` and `UnlockedKnots` do (not read).
23. Whether paying a bill removes the physical bill object. `TaxPaymentUI.PayBill` does not destroy it (`TaxPaymentUI.cs:47-56`).
24. Initial `TaxHandler.nextBillTimer` and `billObj` prefab. The first bill appears as soon as `Taxes >= 1` if the timer starts at or below 0.
25. Whether `StockGraph` subscribes to `OnMoneyChanged` once or many times in practice (see Stocks).

**Other electronics**

26. The `Phone.p*` and `Phone.pPrank*` dialogue keys in the Ink data.
27. The `Picture` reload path. `PhotoCamera` stores the full `.jpg` path, and `Picture.Start` reloads with `storedImagePath + ".jpg"` under `StreamingAssets`. Confirm whether saved photos reload after a restart.
28. The `Fuse` and `IBatteryPowered` behavior for the radio (not read).
29. Whether the `TV` has exactly one set of channels, and that channel 0 is the default.
30. Whether `Screensaver` is the only screensaver effect used (the `Random.Range(2, 3)` always selects Zoom).
