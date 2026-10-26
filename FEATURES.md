# NeetPlus features

Everything NeetPlus adds to NEET Demo, for players. Each mod can be switched on and off on the **Mods** page of the mod menu, and most mods have smaller switches (features) and settings of their own. What changed in each version is on the **What's New** page in game.

## Getting started

- **Installing**: unpack the release, close the game, run `install.sh` (Linux) or `install.bat` (Windows). The installer asks two questions:
  - **Switch to 64-bit?** Downloads Unity's 64-bit version of the game's engine from Unity (about 180 MB) and swaps it in. The game runs a little faster and can use more memory. You can switch back at any time.
  - **Incremental garbage collection?** The game cleans up memory in small steps instead of freezing for a moment every so often. Recommended.
- **Uninstalling**: run `uninstall.sh` or `uninstall.bat`. It puts the original game files back. Your saves and mod settings stay.
- **Version in the corner**: the bottom left corner shows the pack's name, version and build (for example `NeetPlus v0.1.0 (1a2b3c4)`), so you can see the mods are running and screenshots show which build you have. Hide it in Settings.

## Mod menu

Press **F1**, or pick **MODS** in the pause menu (where the feedback form used to be). The game pauses while it's open (you can turn that off in Settings).

| Page | What's on it |
| --- | --- |
| **Mods** | Every installed mod: switch it on or off, load or unload it while playing, and switch its features. NeetPlus's framework and No Feedback are always on. |
| **What's New** | The changes in each version. Opens by itself after an update (you can turn that off). |
| **PC Apps** | The addresses of the apps on the in-game computer; change them to whatever you like. |
| **Settings** | Every mod's settings in one place. |
| **Errors** | Anything that went wrong this session, newest first: red for errors, amber for problems a mod caught and handled. **COPY REPORT** copies it for a bug report; it's also saved as `NeetLoader/logs/errors.log`. |
| **Phone** | Every new phone call, with a button to make it ring now (Phone Expansion). |
| **TV** | Your TV stations (Custom TV Stations). |

## Unofficial NEET Patches

Fixes for bugs in the game itself. Each fix is its own switch, so you can turn one off to see the game's own behaviour.

- **Saving with house spiders**: the game couldn't save while a dead house spider was lying around, and dying got stuck. Fixed.
- **One broken thing doesn't stop saving**: one object that can't be saved no longer stops the whole save (and every save after it). That object is left out and the problem shows up on the Errors page.
- **Things that are gone stay gone**: fires you put out no longer start burning again (with the smoke alarms) when you load, and a leftover fuse no longer stops saving after a load.
- **Characters that come back**: after Grandpa dies, the thief's arrival no longer breaks the lounge window and lets the thief in over and over.
- **Story events that go wrong**: a story event that runs into an error stops instead of repeating every frame.
- **Status effects end on time**: food, pills, diarrhea and the like now each last their own time. Before, only the newest one counted down and the rest froze, for good while you were heavy.
- **Crouch and weight slowdowns don't stick**: saving while crouched no longer leaves you at 80% speed after loading.
- **Effects as strong at any frame rate**: lasting effects like diarrhea no longer work four times as fast at 120 fps as at 30.
- **Frame rate and vsync settings stick**: the frame rate and vsync you pick in the game's settings stay, instead of going back to 120 fps without vsync whenever the house loads or someone shows up.
- **Keep promoted card stats**: promoted cards in the computer's card game keep their doubled earnings after loading.

## Performance Fixes (experimental)

Makes the game smoother. Each fix is its own switch.

- **Faster saves**: saving no longer freezes the game. Only the paint (blood, dirt, water...) that changed since the last save is saved, in the background. In a messy house a save went from 2.5 seconds to well under a tenth of a second.
- **Free unused textures**: the game no longer keeps using more video memory every time the house loads (after dying or sleeping); that was about 1.5 GB per load in a messy house.
- **Peephole camera only near the door**: the front door's peephole only films the porch while you're close enough to look through it, which saves about a sixth of each frame everywhere else.
- **Fewer shadows**: choose which lights cast shadows in Settings, under **Shadows**: room lights (default: lights you switch on and off, the flashlight and the moon), none (fastest), or all lights like the original game.
- **Less log spam**: no more tens of thousands of warnings in the log while you're dead or hanging.
- **Idle paint surfaces rest**: surfaces only do work when something lands on them.
- **Characters plan their walk once**: trick-or-treaters, the cat, rats and others don't recalculate their path every frame.

## Open Saves

- **Compressed saves**: saves are about 16 times smaller (`.nsav` files, an open format). Old saves still load. Turn it off to save plain JSON like the original game.
- **Save cleanup**: keeps only the newest saves (25 by default, see Settings) instead of a new file piling up every time the game saves.

## Open House

Opens what the demo closes off. Each area is its own switch.

- **Open the pantry** (on): the pantry next to the kitchen, with everything inside usable.
- **Open the basement backroom** (on): the black door behind the boxes in the basement, which the demo has no key for.
- **Open dad's yard** (on): the yard door in the living room. Switch it off to open it the game's way, with the key lying nearby.
- **Open the attic** and **Open the roof** (off): areas the demo walls off; parts may be unfinished.
- **Lift demo limits** (off): turns off the demo flag, so the black and occult markets take orders. Things the demo held back may not be finished.

## Phone Expansion

New numbers to call and new calls that ring in, with choices and consequences.

| Number | Who | What happens |
| --- | --- | --- |
| 555-7492 | Pumpkin Pizza | Order a pizza ($15), delivered to the front door; the driver calls when it's there. Free candy once. Prank them and you're on the list until you apologize. |
| 555-2368 | Spookbusters | Call a second time and they pick up: they'll open the black basement door for $30 or put out a fire. |
| 666-6666 | Satan | After the game's own call, he trades $666 for 20 soul, or a copy of you for 20 soul. |
| 555-3666 | The Scream Line | A spooky fact; scream back for candy. |
| 555-0100 | Phone Company Test Line | Tests the phone: calls you back, flickers the lights. |

Calls that ring in by themselves: a mysterious **PRIVATE** caller (once per save), **MOM** when you're starving (sends food), **FEDUP** after you order something, and the **PIZZA** driver.

- **New calls ring in**: how often a random call is a new one instead of one of the game's prank calls is set in Settings (**New call chance**, 50% by default).
- **Caller ID**: the phone's screen shows who's calling; the game's prank calls show UNKNOWN.

Modders can add calls as JSON or Ink files in any mod's `phone` folder.

## Custom TV Stations

Your own videos on the living room TV. Put MP4 or WebM files into a mod's `tv` folder (there's an example station in `Mods/TvStations/tv`) and they show up as stations, next to the game's channels.

- **Keep the game's channels**: switch off to watch only your own stations. Single channels can be hidden with a `stations.json` in the `tv` folder.
- **Station volume** in Settings.

## Card Expansion

More for the computer's anime card game.

- **Spooky card set**: twelve Halloween cards after the game's 66.
- **Summons**: a Summon button that spends crystals (500 by default) on a random card payout, or a shiny card with free levels.
- **Auto-clicker**: buy it once with crystals (5,000 by default) and it clicks every unlocked card for you.
- **Better crystal exchange**: converting crystals pays $5 per 10K instead of $3 (set it in Settings).

Costs, chances and the click rate are in Settings.

## Pumpkin Smash

A whack-a-pumpkin game on the in-game computer (address `pumpkin`). Smash pumpkins for 30 seconds, beat your best score and earn a little cash for each one. Round length and cash per pumpkin are in Settings.

## Cheat Menu

Press **Insert** (changeable in Settings). Four tabs:

- **Player**: god mode, needs that don't drain, fill all needs, movement speed, teleport.
- **Spawn**: items, people and creatures.
- **World**: start or put out a fire, power on or off, unlock all doors, game speed.
- **Computer**: money, taxes, crystals and the card game.

The game keeps running while the menu is open unless you turn on pausing in Settings.

## No Feedback

Always on. The pause menu's feedback form would send reports about a modded game to the original developer, describing code they never wrote; this stops it and hides the form. The MODS section takes its place.
