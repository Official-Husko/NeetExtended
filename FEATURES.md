# NeetExtended features

Everything NeetExtended adds to NEET Demo, for players. Each mod can be switched on and off on the **Mods** page of the mod menu, and most mods have smaller switches (features) and settings of their own. What changed in each version is on the **What's New** page in game.

## Getting started

- **Installing**: unpack the release, close the game, run `install.sh` (Linux) or `install.bat` (Windows). The installer asks two questions:
  - **Switch to 64-bit?** Downloads Unity's 64-bit version of the game's engine from Unity (about 180 MB) and swaps it in. The game runs a little faster and can use more memory. You can switch back at any time.
  - **Incremental garbage collection?** The game cleans up memory in small steps instead of freezing for a moment every so often. Recommended.
- **Uninstalling**: run `uninstall.sh` or `uninstall.bat`. It puts the original game files back. Your saves and mod settings stay.
- **Version in the corner**: the bottom left corner shows the pack's name, version and build (for example `NeetExtended v0.1.0 (1a2b3c4)`), so you can see the mods are running and screenshots show which build you have. Hide it in Settings.
- **Game version check**: every release is tested with a specific build of NEET Demo. If your game is a different build (after a Steam update, say), the corner adds "game build not tested". Most things usually still work; report anything that doesn't.

## Mod menu

Press **F1**, or pick **MODS** in the pause menu (where the feedback form used to be). The game pauses while it's open (you can turn that off in Settings).

| Page | What's on it |
| --- | --- |
| **Mods** | Every installed mod: switch it on or off, load or unload it while playing, and switch its features. The Framework and No Feedback are always on. |
| **What's New** | The changes in each version. Opens by itself after an update (you can turn that off). |
| **PC Apps** | The addresses of the apps on the in-game computer; change them to whatever you like. |
| **Settings** | Every mod's settings in one place. |
| **Errors** | Anything that went wrong this session, newest first: red for errors, amber for problems a mod caught and handled. **COPY REPORT** copies it for a bug report; it's also saved as `NeetLoader/logs/errors.log`. |
| **Phone** | Every new phone call, with a button to make it ring now (Phone Expansion). |
| **Email** | Every new email, with a button to send it now (Email Expansion). |
| **TV** | Your TV stations (Custom TV Stations). |
| **Mixtapes** | Your tapes, with a button to get one (Mixtapes). |
| **Randomizer** | This save's seed, the visitors in the order they'll come and how fast your needs drain, with a button to roll again (Randomizer). |

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
- **Emails stay in the inbox after loading**: loading a save used to leave the computer's inbox empty until new mail came in. Now every email you had is there.
- **Spam emails only come once**: the game's spam no longer arrives all over again after every load.
- **Spam keeps its timer through saves**: after loading, the next spam comes when it was due instead of 15 seconds in.
- **Spam doesn't double up after loading**: the game ran its spam timer twice after a load, so spam came twice as often. Now it runs once.
- **Characters say their reaction lines**: Poopman has lines for when you hurt him or throw something gross at him, but the game never found them. Now he says them.
- **Grandpa doesn't get stuck after the thief**: the game could send Grandpa to a routine he doesn't have, which kept erroring. Now he takes the closest one.
- **Cops only come for orders that went through**: blocked or unaffordable black and occult market orders could still bring a cop van.
- **Custom cassettes cost what the shop says**: the custom tape shop charged 10$ more than its label.
- **Every achievement has a spot on the medal shelf**: the last achievement's medals never showed up in the shed.
- **The money platinum medal checks the right thing**: it read the progress of a different medal.

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
| 555-0201 | Evergreen Gardens | Somebody bought you a cemetery plot. The deed is $40. |
| 555-0214 | No signal | A lost hiker with no pants. |
| 555-0221 | Kids Hotline | The kids who keep prank calling you. |
| 555-0231 | Still here | The number the creepy caller leaves you. Call it when the house is quiet. |
| 555-0241 | Trivia Line | One question, $25 for the right answer. |
| 555-0251 | Dot next door | She wants her cup of sugar back. |
| 555-0261 | Psychic Hotline | First call free, then $5. |
| 555-0262 | Dial-a-Joke | Bad jokes, mostly about a dad. |
| 555-0263 | Crisis Line | Gary is on his sandwich break. |
| 555-0264 | PC Help Desk | Opens the browser for you, once. |
| 555-0265 | Lonely Hearts | Your matches are terrible. |
| 555-0266 | Store Complaints | Complain about a late order and get a refund, once. |
| 555-0272 | Tallow Creek Fitness | Collections for a gym membership you forgot about. |

About 50 calls ring in by themselves, in place of the game's own prank calls: telemarketers and robocalls, creepy callers (one cuts the power, one sounds like you), wrong numbers, kids daring each other, the neighbours complaining about the smell, people from your old life, a shock-jock radio show, surveys, plus a mysterious **PRIVATE** caller (once per save), **MOM** when you're starving (sends food), **FEDUP** after you order something, and the **PIZZA** driver. Some only call after something else happened, and some pay you or send stuff to the door. They're as crude as the game's own calls.

- **New calls ring in**: how often a random call is a new one instead of one of the game's prank calls is set in Settings (**New call chance**, 50% by default).
- **Caller ID**: the phone's screen shows who's calling; the game's prank calls show UNKNOWN.

Modders can add calls as JSON or Ink files in any mod's `phone` folder.

## Email Expansion

New emails in the computer's inbox (Rail Mail), and you can answer them: replies are buttons under the email, and what you pick changes what happens next. About 70 of them, as crude as the game's own spam:

- **Scams**: a prince stuck in gravy, the Rail Lotery, a crypto coin for people in bed, the uncledad and his cow, a scammer who falls in love with you (and her mom).
- **Family**: mom forwarding shower articles and sending snack money, grandpa in all caps, a cousin with a boat, a reply-all ham disaster, your clone, and dad's out-of-office reply.
- **Store orders**: confirmations that overshare, a black market satisfaction survey, a package from the occult market that's alive, a courier who ate your bread.
- **Cults and Satan**: the Low Church of Brown, tithes, Satan's newsletter, Hell's HR department, a ghost's house rules.
- **Online life**: the 1337 gamer squad, forum flame wars, a dating site for shut-ins, a streamer with two viewers, someone who found your fanfic.
- **Bills and authorities**: C.U.M. taxes and late fees, the power company, a court summons, jury duty, the census, the HOA about the face in your lawn.
- **Creepy**: emails from the basement, the machine, your own account, dad's old account, the walls, and the future.
- **The neighbourhood**: Dot's list, smell complaints, Pumpkin Pizza, the Spookbusters invoice, Doug from the reunion. These follow up on what happened in your phone calls.

It also sends the three emails the game has but never sends: mom's "love u bb" some morning, your clone's hate mail after you trade soul for a clone, and the C.U.M. tax reminder with your first tax bill.

Emails can pay you, charge you, send stuff to the front door, cut the power, send someone over or make the phone ring. Some only come after another email, a reply, a phone call, a store order, or the morning after you slept.

- **New emails arrive**: new emails show up every few minutes while you play, after store orders, when you get home and when you wake up. How often is in Settings (**Fewest/Most minutes between emails**, 4 to 10 by default).

Modders can add emails as JSON or Ink files in any mod's `email` folder.

## Custom TV Stations

Your own videos on the living room TV. Put MP4 or WebM files into a mod's `tv` folder (there's an example station in `Mods/TvStations/tv`) and they show up as stations, next to the game's channels.

- **Keep the game's channels**: switch off to watch only your own stations. Single channels can be hidden with a `stations.json` in the `tv` folder.
- **Station volume** in Settings.

## Mixtapes

Your own music on cassettes for the radio. Put OGG, MP3 or WAV files into a mod's `mixtapes` folder (there's an example tape, Pumpkin Mix, in `Mods/Mixtapes/mixtapes`) and each one is a tape: sold in the computer's store, and if you want, left lying in a room of the house. Put it in the radio like the game's own tapes.

- **Mixtape radio**: with no tape in, the radio plays your songs one after another, shuffled, like a station. It still needs a fuse and the music button.
- **Tape price** in Settings. Names, prices, colours and the room a tape is left in go in a `tapes.json` in the `mixtapes` folder.

## Randomizer

Every game is a little different. Each save gets a seed that decides:

- **Shuffled visitors**: who knocks at night comes in a different order.
- **Random prices**: the store charges more or less for everything (how much in Settings). Money keeps its value.
- **Mystery deliveries**: sometimes an order brings something else from the same store, for what you paid (how often in Settings).
- **Shuffled house**: on a new game, loose things in the house swap places. Keys and notes stay, and nothing ends up somewhere you can't reach.
- **Random needs**: hunger, energy, bladder and hygiene drain faster or slower (how much in Settings).

Each part can be switched off. **Seed for new games** in Settings lets you play a seed someone shared; the **Randomizer** tab shows yours.

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
