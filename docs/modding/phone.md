# Phone calls

The Phone Expansion mod (`mods/PhoneExpansion`) adds numbers to dial and calls that ring in. Calls play in the game's own dialogue box, exactly like the game's calls: same voice blips, same choices, same rich text. A call can also make things happen: deliver food, cut the power, unlock a door, call you back later.

Calls are plain files. Any mod can ship them in a `phone` folder; no code needed.

## What's in the mod

| Number | Who | What happens |
| --- | --- | --- |
| 555-0100 | Phone Company Test Line | The example call (`phone/example.json`). Tests the ringer (calls you back), the lights and flags. |
| 555-7492 | Pumpkin Pizza | Order a pizza ($15), delivered to the front door, and the driver calls when it's there. Free candy once. Prank them and you're on the list until you apologize. |
| 555-2368 | Spookbusters | The game's "call later" number. The second time you call, they pick up: they'll open the black basement door for $30 or put out a fire. |
| 666-6666 | Satan | The game's call plays the first time. After that he trades $666 for 20 soul, or a copy of you for 20 soul (the game's own TradeSoulForClone). |
| 555-3666 | The Scream Line | A random spooky fact; scream back for candy. Written in Ink by hand (`phone/scream-line.ink`). |

Calls that ring in by themselves:

| Caller | When |
| --- | --- |
| PRIVATE | Once per save, after 15 minutes of play, in place of a prank call. Ends with the fuses popping out. |
| MOM | When hunger is under 25, at most every 30 minutes. Sends food. |
| FEDUP | After a store order, at most every 20 minutes. |
| PIZZA | The pizza driver, 45 seconds after an order. |

Switches on the Mods page (F1): **New calls ring in** and **Caller ID** (shows who's calling on the phone's screen; the game's prank calls show UNKNOWN). In Settings, **New call chance** sets how often a random incoming call is a new one instead of one of the game's 11 pranks (default 50%).

The **Phone** tab in the mod menu lists every call with a RING NOW button, any problems in call files, and RELOAD CALLS.

## Where call files go

Every `.json` and `.ink` file in `Mods/<any mod>/phone/` (subfolders too) is loaded once all mods are loaded, and loaded again within a second of a file changing or a mod loading or unloading. The game doesn't need to restart, and neither does the mod.

A call pack doesn't need code. This is a complete mod:

```
Mods/MyCalls/
  mod.json          { "id": "MyCalls", "name": "My Calls", "version": "1.0.0", "dependencies": { "PhoneExpansion": "0.1.0" } }
  phone/
    landlord.json
```

Problems (bad JSON, unknown actions, Ink compile errors) are logged and listed on the Phone tab with the file and the step they're in. A call with a problem is skipped; the rest of the file still loads.

## Call files

A file holds one call, a list of calls, or `{ "calls": [ ... ] }`. `//` and `/* */` comments are allowed.

```json
{
  "id": "landlord",
  "number": "555-0142",
  "name": "The Landlord",
  "callerId": "LANDLORD",
  "say": [
    "Rent's due.",
    { "choices": [
      { "text": "Here you go.", "if": { "money": 50 }, "say": [ { "do": "money", "amount": -50 }, "Pleasure." ] },
      { "text": "I don't have it.", "say": [ "Then I'll be by to check on the place.", { "do": "npc", "name": "Grandpa_NPC" } ] }
    ] }
  ]
}
```

### Call fields

| Field | Meaning |
| --- | --- |
| `id` | Unique within the mod. Defaults to the number's digits, or the name. Other files refer to it as `id` (same mod) or `ModId/id`. |
| `number` | What to dial: 3 to 7 digits, the dash is optional. Leave it out for a call that only rings in. |
| `name` | Who it is, for the Phone tab. |
| `callerId` | What the phone's screen shows while this call rings. About 10 characters fit. Defaults to the name. |
| `say` | What's said: a list of steps (below). |
| `ink` | Instead of `say`: the name of a knot from an `.ink` file (see "Writing calls in Ink"). |
| `when` | Only pick up while this condition holds. |
| `otherwise` | Steps for when `when` doesn't hold. Without it: the game's own call plays if the number is one of the game's, otherwise the line is busy. |
| `incoming` | `true`, or an object (below), to make the call ring in by itself. |

### Using the game's numbers

A call can use one of the game's 15 numbers (411, 911, 666-6666, ...; see `docs/game/computer-and-apps.md`). When its `when` holds, it plays instead of the game's call; otherwise the game's call plays as usual. `"when": { "dialed": ">= 1" }` keeps the game's call for the first time and takes over after that, which is what Spookbusters and Satan do.

Several calls can share a number. The first one (in load order) whose `when` holds plays.

### Steps

Steps run top to bottom. A step is one of these:

| Step | What it does |
| --- | --- |
| `"text"` | The caller says it. Line breaks split it into separate lines. Unity rich text works (`<color=red>`, `<size=150%>`), but not in a choice's text. |
| `{ "player": "text" }` | The player says it (the player's voice blips). |
| `{ "choices": [ ... ] }` | Choices. Each is `{ "text", "say", "if", "once" }`: `say` is what follows, `if` hides the choice unless a condition holds, `once` removes it after it's picked. The chosen text is said by the player, like in the game's calls. |
| `{ "choice": { "text": steps, ... } }` | Shorter form when no choice needs `if` or `once`. |
| `{ "if": condition, "then": steps, "else": steps }` | Branches. `else` is optional. |
| `{ "random": [ steps, steps, ... ] }` | One of the options, picked at random. |
| `{ "do": "action", ... }` | Runs an action (below). |
| `{ "label": "name" }` and `{ "goto": "name" }` | Jump around inside the call, for example back to a menu of choices. |
| `{ "end": true }` | Hangs up right there. |
| `{ "ink": "line" }` or `{ "ink": ["line", ...] }` | Raw Ink, for things the other steps can't do, like `~ TradeSoulForClone()`. |
| `[ steps ]` | A list inside a list is the same as writing the steps inline. |

Wherever steps are expected, a single step works too: `"then": "Oh hi."`. After a branch (a choice, an if, a random pick), the call carries on with the steps after it. Every call hangs up after its last step.

### Conditions

A condition is an object; every key in it must hold. Used by `when`, `if`, choice `if` and `incoming.when`. Conditions and actions are shared with every mod that reads content files (emails use the same ones), and flags are shared too: an email can set a flag a call checks. `called`, `dialed`, `give` and `ring` come from the phone; the rest from the framework.

| Key | Holds when |
| --- | --- |
| `money` | Cash compares: `20` means at least 20, or `"< 5"`, `">= 100"`, `"= 0"`, `"!= 3"`. |
| `day` | Which day it is compares: 1 at the start, one more every time the player sleeps and wakes up. |
| `minutes` | Minutes of play in this save compare. |
| `night` | The game's own night counter. The game never moves it on, so use `day`. |
| `stat` | Player stats compare: `{ "Hunger": "< 25", "Energy": "< 50" }` or `"Hunger < 25"`. Stats run 0 (empty) to 100 (full); there's also Health, Bladder, Hygiene, Soul (starts at 66) and more. |
| `holding` | The player holds this item in either hand: `"Pumpkin"`, or a list of names. |
| `room` | The player is in this room: `"Kitchen"`, or a list. Room names are the game's: Kitchen, Livingroom, Basement, Bedroom_Neet, ... |
| `flag` | A flag is set: `"name"`, or compares: `{ "name": ">= 2" }`. |
| `called` | Another call was played: `"pizza"` or `{ "pizza": ">= 2" }`. |
| `times` | How many times this call was played before compares. `"= 0"` means "the first time". |
| `dialed` | How many times this call's number connected before (the game's own call included) compares. |
| `chance` | A random chance from 0 to 1. |
| `any` | A list of conditions; one of them must hold. |
| `all` | A list of conditions; all must hold. |
| `not` | A condition that must not hold. |

```json
"when": { "day": ">= 2", "any": [ { "holding": "Pumpkin" }, { "flag": "pumpkin_friend" } ] }
```

### Actions

`{ "do": "name", ... }`. Problems while an action runs are logged and never stop the call.

| Action | Values | Does |
| --- | --- | --- |
| `money` | `amount` | Adds cash, or takes it with a minus. Increases are taxed like any income. |
| `give` | `item`, `count` | Pops items out next to the phone, like the game's GiveItem. |
| `deliver` | `item`, `count`, `after` | Drops a package with the item at the front door after `after` seconds. Item names are the cheat menu's spawn list. A delivery still waiting when the game saves is lost. |
| `npc` | `name`, `room` | Spawns a character (default room: FrontYard). |
| `fire` | `room` | Starts a fire (default: wherever the player is). |
| `fireout` | `room` | Puts out a fire, or every fire without a room. |
| `power` | `on`, `room`, `for` | Power off (`"on": false`) or on, for a room or the whole house. With `for`, it switches back after that many seconds. |
| `fuses` | | Pops every fuse out of the fuse box. The lights stay out until the player puts them back. |
| `unlock` | `door` | Unlocks doors by id: `BASEMENT` (the black door), `DADYARD`, `Attic`. |
| `site` | `url` | Opens a site or app on the computer. |
| `ring` | `call`, `after` | Rings a call after `after` seconds of play. Survives saving; if the line is busy it rings when it's free. Works from emails too (use the full id, `ModId/call`). |
| `flag` | `name`, `set` or `add` | Sets a flag (default 1) or changes it. `"set": 0` clears it. Flags are saved. |
| `sequence` | `name`, `arg` | Runs one of the game's scene sequences, like the game's TriggerSequence. |
| `knockout` | `seconds` | Knocks the player out. |
| `stat` | `name`, `set` or `add` | Changes a player stat. |

Other mods can add actions and conditions (see "From code").

### Ringing in

`"incoming": true` rings in at random with the defaults. As an object:

| Field | Default | Meaning |
| --- | --- | --- |
| `trigger` | `"random"` | `random`: in place of one of the game's random prank calls. `order`: after the player orders from a computer store. `arrive`: after the house loads. `manual`: only when something rings it (a `ring` action, the Phone tab, code). |
| `weight` | 1 | How likely it's picked among the calls that could ring. |
| `when` | | Only rings while this holds. |
| `once` | false | Only ever rings once per save. |
| `cooldown` | 0 | Minutes of play before it can ring again. |
| `delay` | 20 | `order` and `arrive`: seconds before it rings. |
| `store` | | `order`: only after orders from `Normal`, `BlackMarket` or `OccultMarket`. |

The game rings its phones every 5 to 12 minutes. Each time, with the "New call chance" from the settings, one of the `random` calls that can ring takes the slot instead.

## Writing calls in Ink

For anything bigger, write the call in Ink, the language the game's dialogue is written in. Put the `.ink` file in the same `phone` folder and give the knot a number from a JSON file:

```json
{ "id": "scream-line", "number": "555-3666", "name": "The Scream Line", "ink": "PhoneExpansion_ScreamLine" }
```

```ink
=== PhoneExpansion_ScreamLine ===
{~Welcome to the Scream Line!|You've reached the Scream Line.}
* [Scream back]
    AAAAAAAAH!
    ~ PhoneAct("give", "Candy_Bar")
* [Hang up]
- # EndPhoneCall
-> DONE
```

- End the call on the `EndPhoneCall` tag, like the game's calls. If a call runs out of lines without it, the phone hangs up when the player presses use on the last line.
- Choices without brackets (`* Hi.`) are said by the player, like the game's; with brackets (`* [Hi.]`) they aren't.
- Prefix knot names with your mod id. Names are shared with the whole game story.
- The game's lists, variables and functions are available (`nightId`, `playerBody ? naked`, `GiveItem("Mug")`, `TradeSoulForClone()`, ...; the list is in `docs/game/dialogue-and-rendering.md`), plus the framework's:

| Function | Does |
| --- | --- |
| `Act(action, values)` | Runs an action. `values` is `"key=value; key2=value2"`, or a single value for the action's main value: `Act("deliver", "Pizza")`, `Act("ring", "call=pizza-driver; after=30")`. |
| `Check(condition, value)` | Tests a condition: `{Check("money", ">= 20"): ...}`, `Check("holding", "Pumpkin")`, `Check("stat", "Hunger < 25")`. |
| `Flag(name)` | A flag's value (0 if unset). |
| `SetFlag(name, value)`, `AddFlag(name, amount)` | Change a flag. |

The older names `PhoneAct`, `PhoneCheck`, `PhoneFlag`, `SetPhoneFlag` and `AddPhoneFlag` still work.

`VAR` declarations don't work in mod scripts (they'd never reach the game's story); use flags. See `docs/modding/framework.md` ("Dialogue") for the other limits.

## From code

Namespace `PhoneExpansion.Calls`; conditions, actions and flags are in the framework's `NeetModKit.Rules` (see [framework.md](framework.md#rules)). Add `"PhoneExpansion": "0.1.0"` to your mod's dependencies and reference the project.

```csharp
// A call with hand-written Ink.
InkScripts.Add(this, "landlord.ink", "=== MyMod_Landlord ===\nRent's due.\n# EndPhoneCall\n-> DONE");
PhoneBook.Add(this, new PhoneCall
{
    Id = "MyMod/landlord",
    Number = "5550142",
    Name = "The Landlord",
    Knot = "MyMod_Landlord",
    When = Condition.Of(call => Money.Get() > 0),
    Incoming = new IncomingRules { Trigger = IncomingRules.Random, Weight = 2 },
});

// Ring it in two minutes of play.
PhoneRing.Ring("MyMod/landlord", 120f);

// New action and condition for call files (and emails, and anything else that reads them).
RuleActions.Register(this, "confetti", args => Confetti.Burst(args.Num("amount", 10)), "amount", "Throws confetti.");
RuleConditions.Register(this, "raining", value => { bool want = value.Value<bool>(); return _ => Weather.Raining == want; }, "true while it rains");
```

| API | |
| --- | --- |
| `PhoneBook.Add(owner, call)`, `All`, `Find(id)`, `WithNumber(digits)`, `Changed` | Every modded call. Calls are removed when the owner cleans up. |
| `PhoneCall` | `Id`, `Number`, `Name`, `CallerId`, `Knot`, `When`, `OtherwiseKnot`, `Incoming`, `Source`. It's an `IRuleSubject`, so conditions like `times` know which call they're checking. |
| `IncomingRules` | `Trigger`, `Weight`, `When`, `Once`, `CooldownMinutes`, `DelaySeconds`, `Store` |
| `PhoneRing.Ring(id or call, delaySeconds)`, `Cancel(call)`, `Ringing`, `Active` | Ring calls; what's ringing or being talked on now. |
| `PhoneMemory` | `TimesPlayed(id)`, `SecondsSincePlayed(id)`, `TimesDialed(digits)`. Saved with the game. Flags and play time are `ModFlags` in the framework. |

## Testing

- The Phone tab: RING NOW on any call, HANG UP, RELOAD CALLS.
- The in-game console (backquote): `phoneCalls`, `phoneRing <id>`, `phoneDial <number>` (dials on the phone nearest to you), `phoneHangUp`, `phoneReload`, and the framework's `flags` and `setFlag <name> <value>`.
- From a terminal through the dev bridge: `neet call console.exec '{"command":"phoneRing PhoneExpansion/pizza"}'`.

To pick up or talk on a call you need to be at a phone: one is in the kitchen, the other in the upstairs lobby.

## How it works

- The game starts every call with `Dialogue.InitCharacterDialogue(phone, "Phone", knot)`: `Phone.p` + digits when dialing, `Phone.pPrank` + 1..11 when answering. A prefix on that method swaps in a modded knot when a call fits, or plays the busy tone for a modded number that doesn't pick up.
- Modded numbers are added to each handset's list of known numbers, so they connect after the short ring like the game's numbers.
- When the phones ring, the first handset to start ringing picks the call for that ring; answering any handset plays it.
- Call files become Ink through the framework's `StepWriter`: each branch is its own stitch, conditions and actions become `RuleIf(n)` and `RuleDo(n)` calls into the framework's `RuleSteps` table. The whole file compiles with the game's Ink compiler and joins the game's story through the framework's `InkScripts`.
- Before the game saves, a modded call in progress hangs up, so saves never depend on the mod. Call counts and scheduled rings go into the save through `ModSaveData`; flags and play time are the framework's `ModFlags`. Saves from 0.1.0 kept flags with the phone; they move over to `ModFlags` when such a save loads.
