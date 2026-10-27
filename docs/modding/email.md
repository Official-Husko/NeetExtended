# Emails

The Email Expansion mod (`mods/EmailExpansion`) adds emails to the computer's inbox (Rail Mail: "Get railed by your mail without fail"). They arrive next to the game's own spam, and you can reply to them: replies are buttons under the email, and the email goes on depending on what you picked. An email can also make things happen, with the same actions phone calls use: pay you, take money, send something to the front door, set a flag a phone call checks later, ring the phone.

Emails are plain files. Any mod can ship them in an `email` folder; no code needed.

## What's in the mod

| Subject | When | What happens |
| --- | --- | --- |
| Welcom to Rail Mail!! | Soon after you get home, once | Free money, or a candy for emotional damage. The example email (`email/example.jsonc`). |
| RE: ur free money | The morning after you took the money | Rail Mail wants it back. |
| money on ur card | Now and then, at most every 45 minutes | Mom sends snack money. Written in Ink by hand (`email/from-mom.ink`). |
| love u bb | Some morning from day 2, once | The game's own `LoveYou` email, which the game never sends (`email/game-emails.jsonc`). |
| fuck you (from you) | 90 seconds after you trade soul for a clone, once | The game's own `HateYou`, signed "Your own clone." Sent by `Mail/GameMail.cs`. |
| TAX SEASON IS UPON YOU | With the first tax bill, once | The game's own `TaxReminder`. The game's reader would only show its first line; the mod's shows all of it. |

And about 70 more, one file per kind, most of them arriving at random and some only after another email, a reply, a phone call, a store order or a night's sleep:

| File | What's in it |
| --- | --- |
| `spam.jsonc` | A prince stuck in gravy, the Rail Lotery, phishing, a crypto coin, the uncledad's cow, a scammer in love and her mom, miracle pills. |
| `family.jsonc` | Mom's shower articles and snack money, grandpa in all caps, the cousin with a boat, a reply-all ham thread, your clone, dad's out-of-office. |
| `orders.jsonc` | After store orders: confirmations, a pizza review, the black market survey, a live occult package, a courier who ate your bread, refunds, Rail Mail's newsletter. |
| `cults.jsonc` | The Low Church of Brown, tithes, Satan's newsletter, Hell's HR, a ghost's house rules, a cult group chat, the Bishop. |
| `online.jsonc` | The 1337 gamer squad, a forum flame war, a dating site, the anime card game, a streamer, a guild kick, a virus pop-up, your fanfic. |
| `authorities.jsonc` | C.U.M. taxes and late fees, the power company, a court summons, jury duty, the census, an inspection, the water department, the HOA. |
| `creepy.jsonc` | The basement, the machine, your own account, dad's account, the walls, a photo of your back, your future self, a blackmail tape. |
| `neighbourhood.jsonc` | Follow-ups to the phone calls: Dot's list, smell complaints, Pumpkin Pizza, the Spookbusters invoice, the reunion, the stapler, a job rejection. |

Switch on the Mods page (F1): **New emails arrive**. In Settings, **Fewest/Most minutes between emails** set how often a random new email shows up (4 to 10 minutes by default).

The **Email** tab in the mod menu lists every email with a SEND NOW button, any problems in email files, and RELOAD EMAILS.

## Where email files go

Every `.jsonc`, `.json` and `.ink` file in `Mods/<any mod>/email/` (subfolders too) is loaded once all mods are loaded, and loaded again within a second of a file changing or a mod loading or unloading.

Content files are JSON with comments (`//` and `/* */`), named `.jsonc`; plain `.json` works too. If a folder has both `name.json` and `name.jsonc`, only the `.jsonc` is read (an update that renamed its files can leave the old `.json` behind), and the log says so.

```
Mods/MyEmails/
  mod.json          { "id": "MyEmails", "name": "My Emails", "version": "1.0.0", "dependencies": { "EmailExpansion": "0.1.0" } }
  email/
    landlord.jsonc
```

Problems (bad JSON, unknown actions, Ink compile errors) are logged and listed on the Email page with the file and the step they're in. An email with a problem is skipped; the rest of the file still loads.

## Email files

A file holds one email, a list of emails, or `{ "emails": [ ... ] }`. `//` and `/* */` comments are allowed.

```json
{
  "id": "landlord-rent",
  "subject": "RENT!!!!!!",
  "send": { "trigger": "wake", "when": { "day": ">= 2" } },
  "body": [
    "<size=150%>RENT IS DUE<size=100%>",
    "I can smell ur room from my car.",
    { "choices": [
      { "text": "Pay the $50.", "if": { "money": 50 }, "say": [ { "do": "money", "amount": -50 }, "Good boy." ] },
      { "text": "I don't have it.", "say": [ "Then I'm sending my nephew.", { "do": "npc", "name": "Grandpa_NPC" } ] }
    ] }
  ]
}
```

### Email fields

| Field | Meaning |
| --- | --- |
| `id` | Unique within the mod. Defaults to the subject. Other files refer to it as `id` (same mod) or `ModId/id`. |
| `subject` | What the inbox shows. Plain text, no rich text. |
| `body` | What it says: a list of steps (below). Every line is its own paragraph. |
| `ink` | Instead of `body`: the name of a knot from an `.ink` file (see "Writing emails in Ink"). |
| `send` | `true`, or an object (below), to make the email arrive by itself. Leave it out for an email that only something else sends (an `email` action, the Email page, code). |

### Steps

The same steps as phone calls (see [phone.md](phone.md#steps)), with the same [text values](phone.md#text-values) like `{{money}}`, except there are no `player` lines:

| Step | What it does |
| --- | --- |
| `"text"` | A paragraph. Line breaks split it into separate paragraphs; `^n` inside a line is a plain line break. Unity rich text works (`<size=150%>`, `<color=red>`, `<i>`), like the game's own emails. Close a size with `<size=100%>` or the rest stays big. |
| `{ "choices": [ ... ] }` | Replies. Each is `{ "text", "say", "if", "once" }`. They show as buttons under the email; when one is picked, the email shows "You: " and the reply, then goes on with `say` and whatever follows the choices. Keep reply text plain (no rich text) and short. |
| `{ "if": condition, "then": steps, "else": steps }` | Branches. |
| `{ "random": [ steps, ... ] }` | One of the options at random. |
| `{ "do": "action", ... }` | Runs an action. |
| `{ "label": "name" }`, `{ "goto": "name" }` | Jump around inside the email. |
| `{ "end": true }` | Ends the email right there. |
| `{ "ink": "line" }` | Raw Ink. |

An email is read up to its first replies (or its end) the first time it's opened, and its actions run then, once. Reopening it shows the same text; nothing runs again. What's been read and replied is kept in the save.

### Conditions and actions

Emails use the same conditions and actions as phone calls; the full lists are in [phone.md](phone.md#conditions). The email mod adds:

| Condition | Holds when |
| --- | --- |
| `sent` | An email (by id) arrived: `"rent"` or `{ "rent": ">= 2" }`. |
| `read` | An email (by id) was opened: `"rent"`. |

| Action | Values | Does |
| --- | --- | --- |
| `email` | `email`, `after` | Sends an email (by id) now or after `after` seconds of play. Works from phone calls too (use the full id there: `EmailExpansion/rent`). |

Useful from emails: `money` (pay or charge the player), `deliver` (a package at the front door), `give` (pops an item out in front of the player at the desk), `flag` (remember something; phone calls can check it), `ring` (ring a phone call, full id: `PhoneExpansion/pizza-driver`), `site` (open a site on the computer), `power`, `fuses`, `npc`, `fire`.

Inside an email, `times` is how many times this email arrived before, and short ids in `sent`, `read` and `email` mean emails from the same mod.

### Arriving by themselves

`"send": true` arrives at random with the defaults. As an object:

| Field | Default | Meaning |
| --- | --- | --- |
| `trigger` | `"random"` | `random`: every few minutes one email that can arrive does (the settings say how often). `order`: after the player orders from a computer store. `arrive`: after the house loads. `wake`: when the player wakes up after sleeping. `manual`: only when something sends it. |
| `weight` | 1 | How likely it's picked among the emails that could arrive. |
| `when` | | Only arrives while this holds. |
| `once` | true | Only arrives once per save. `false` lets it come again (each copy is its own email in the inbox). |
| `cooldown` | 0 | Minutes of play before it can arrive again. |
| `delay` | 20 | `order`, `arrive` and `wake`: seconds before it arrives. |
| `store` | | `order`: only after orders from `Normal`, `BlackMarket` or `OccultMarket`. |

## Writing emails in Ink

Put an `.ink` file in the `email` folder and give its knot a subject in a JSON file:

```json
{ "id": "from-mom", "subject": "money on ur card", "ink": "EmailExpansion_FromMom", "send": { "once": false, "cooldown": 45 } }
```

```ink
=== EmailExpansion_FromMom ===
Hey sweetie its mom.
~ Act("money", "15")
+ [Thanks mom.]
    U are welcome. I love u even if u smell.
+ [Can I have more?]
    No.
- -> DONE
```

- Every line is a paragraph. Write replies in brackets (`+ [Thanks mom.]`) so the reply doesn't show up twice.
- Prefix knot names with your mod id. Names are shared with the whole email story.
- The shared functions work: `Act(action, values)`, `Check(condition, value)`, `Flag(name)`, `SetFlag(name, value)`, `AddFlag(name, amount)`. The email story has none of the dialogue story's lists, variables or functions.
- `VAR` declarations don't work in mod scripts; use flags.

## Writing them well

Emails should sound like the game's own: crude, loud, stupid on purpose, uncensored, with broken English for scammers. [writing-style.md](writing-style.md) has the voice.

## From code

Namespace `EmailExpansion.Mail`. Add `"EmailExpansion": "0.1.0"` to your mod's dependencies and reference the project.

```csharp
InkScripts.Add(this, InkStory.Email, "landlord.ink", "=== MyMod_Landlord ===\nRent's due.\n-> DONE");
MailBook.Add(this, new MailItem { Id = "MyMod/landlord", Subject = "RENT", Knot = "MyMod_Landlord" });
```

| API | |
| --- | --- |
| `MailBook.Add(owner, mail)`, `All`, `Find(id)`, `ByKnot(knot)`, `Changed` | Every modded email. Emails are removed when the owner cleans up. |
| `MailItem` | `Id`, `Subject`, `Knot`, `Send` (`SendRules`, the framework's `TriggerRules`), `Source`. It's an `IRuleSubject`. |
| `MailMemory` | `TimesSent(id)`, `WasRead(id)`, `SecondsSinceSent(id)`. Saved with the game. |

## Testing

- The Email page: SEND NOW on any email, RELOAD EMAILS.
- The in-game console (backquote): `emailList`, `emailSend <id>`, `emailReload`, `flags`.
- The dev bridge: `neet call console.exec '{"command":"emailSend welcome"}'`, `email.lists` for the whole inbox.

## How it works

- Emails are knots in the game's email story (`EmailHandler._.masterStory`, built from `Master_Email`). Email files become Ink through the framework's `StepWriter` and join that story through `InkScripts` with `InkStory.Email`.
- Sending puts a `KnotList.Knot` (the knot and the subject) into `EmailHandler.SendEmail`, like the game's spam. The game saves the inbox with each email's knot.
- The game shows one line of an email. For modded emails a prefix on `EmailApp.LoadEmail` reads the knot to its replies or its end instead, in an Ink flow of its own per inbox entry, and shows it in a scrolling copy of the email text box with reply buttons copied from the app's own. The game's emails still use the game's text box.
- The text and the replies picked are saved per inbox entry. After loading a save the flow is rebuilt by replaying the replies with actions switched off (`RuleSteps.Muted`).
- An email from a mod that's gone shows "This email came from a mod that isn't installed anymore." (a guard in the framework) instead of breaking the inbox.
- Opening and replying are the framework's `HostActions` (`EmailExpansion.Open`, `EmailExpansion.Reply`), run by `LetterRunner` where the house runs, and each inbox entry's conversation (text, replies on offer, finished) is published in `SharedState` (`SharedLetters`). The reader (`MailReader`) only shows that state. In single player it all happens here; in co-op the host's game runs every conversation, any player can answer, and everyone sees the same one.
