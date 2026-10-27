# NeetExtended features

Everything NeetExtended adds to NEET Demo, for players. Each mod can be switched on and off on the **Mods** page of the mod menu, and most mods have smaller switches (features) and settings of their own. What changed in each version is on the **What's New** page in game.

## Getting started

- **Installing**: unpack the release, close the game, run `install.sh` (Linux) or `install.bat` (Windows). The installer switches the game to Unity's 64-bit version of its engine, downloaded from Unity the first time (about 180 MB): the game runs a little faster and can use more memory. It asks two questions:
  - **Incremental garbage collection?** The game cleans up memory in small steps instead of freezing for a moment every so often. Recommended.
  - **Files for Co-op over Steam?** Downloads the two files from Valve that Co-op over Steam needs and the pack can't include.
- **Back to 32-bit**: if the game won't start or keeps crashing on 64-bit, close it and run `use-32bit.sh` or `use-32bit.bat`. That puts the game's original 32-bit engine back, and installing again keeps it. `use-64bit.sh` or `use-64bit.bat` switches to 64-bit again.
- **Uninstalling**: run `uninstall.sh` or `uninstall.bat`. It puts the original game files back. Your saves and mod settings stay.
- **Version in the corner**: the bottom left corner shows the pack's name, version and build (for example `NeetExtended v0.1.0 (1a2b3c4)`), so you can see the mods are running and screenshots show which build you have. Hide it in Mod Settings.
- **Game version check**: every release is tested with a specific build of NEET Demo. If your game is a different build (after a Steam update, say), the corner adds "game build not tested". Most things usually still work; report anything that doesn't.

## Mod menu

Press **F1**, or pick **MODS** in the pause menu (where the feedback form used to be). The game pauses while it's open (you can turn that off in Mod Settings).

Tabs along the top:

| Tab | What's on it |
| --- | --- |
| **Mods** | Every installed mod: switch it on or off, load or unload it while playing, and switch its features. The Framework, No Feedback and Update Checker are always on. |
| **Mod Settings** | A list of mods like on the Mods tab. Pick one to see its page and its settings (a PAGE / SETTINGS switch at the top when it has both). |
| **What's New** | The changes in each version. Opens by itself after an update (you can turn that off). |
| **Co-op** | Host, join and who's playing (Co-op). |
| **Errors** | Anything that went wrong this session, newest first: red for errors, amber for problems a mod caught and handled. **COPY REPORT** copies it for a bug report; it's also saved as `NeetLoader/logs/errors.log`. |

The mods' own pages on **Mod Settings**:

| Mod | Its page |
| --- | --- |
| **Framework** | **PC Apps**: the addresses of the apps on the in-game computer; change them to whatever you like. Its settings are the menu's own (key, size, pause, What's New). |
| **Phone Expansion** | **Phone**: every new phone call, with a button to make it ring now. |
| **Email Expansion** | **Email**: every new email, with a button to send it now. |
| **Custom TV Stations** | **TV**: your TV stations. |
| **Mixtapes** | **Mixtapes**: your tapes, with a button to get one. |
| **Randomizer** | **Randomizer**: this save's seed, the visitors in the order they'll come and how fast your needs drain, with a button to roll again. |
| **Update Checker** | **Updates**: the version you have, the newest one found, the release page, and a button to check now. |

Every other mod with settings is in the list too.

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
- **Picking up the thief's beanie works**: it used to throw an error and leave your hand stuck holding it.
- **Characters leave hidden items alone**: characters no longer pick up things that belong to something switched off, like the thief's beanie before he shows up.
- **Autosave works after loading**: once a save was loaded, the game never autosaved again. Now it saves every five minutes while you're in your bedroom.
- **Spawners keep working after loading**: after loading a save, loose change, dust, sink water, toilet flushes and blood and paint sprays stopped for the rest of the session. They start again two seconds after the load.
- **Worn clothes stay put**: clothes you wore when you saved fell through the world after loading, and so did the clothes of characters who died or left.
- **Every mask comes back after loading**: wearing a hat and a cigarette (or glasses) and loading the game put only the first one back on and left the rest hanging in the air. All of them go back on now.
- **Dying with a mask on works**: anyone who died wearing a mask got stuck half dead and couldn't die again.
- **Skeletons don't ruin the black pants**: a skeleton that had to crap threw an error and left every pair of black pants you bought or found afterwards dirty and sticky, until you restarted the game.
- **Your stuff stays where you died**: the clothes you wore when you died were gone for good (the game hid them under the house, in every save). Now everything you had, clothes, masks and whatever was in your hands, falls where you died, so you can go back for it.
- **Camera photos are kept**: every photo after the first 20 seconds of play used to overwrite the one before (changing the older pictures too), and photos came back blank after loading. Each photo now gets its own file and loads again.
- **Loaded paint shows**: blood, scorch marks and other paint you saved could be missing on some surfaces after loading, and the next splat there wiped it. In co-op, the mess already in the house when you joined didn't show at all. Now it all shows.
- **The TV shows after loading**: a TV that was on when you saved came back on with its sound playing but a dark screen. It shows its channel again.
- **Custom shirts show when you wear them**: a shirt from the custom shop showed your picture in your hand, but was plain once you put it on, until you saved and loaded. It shows the picture right away.
- **Pans cool down after food burns in them**: a frying pan that had food burn in it stayed hot for good, even long after the stove was off. It cools down like any other pan.
- **Thunder keeps going**: once the story had set off its own lightning there was no thunder again until you restarted the game. Thunder now carries on at random.
- **The moon comes back after lightning**: the first flash switched the moonlight off for good. It goes back to its usual light when the flash is over.
- **Lightning looks the same at any frame rate**: the flash and the lamps flickering with it were over in half the time at 144 fps. They now last as long as they do at 60.

## Performance Fixes (experimental)

Makes the game smoother. Each fix is its own switch.

- **Faster saves**: saving no longer freezes the game. Only the paint (blood, dirt, water...) that changed since the last save is saved, in the background. In a messy house a save went from 2.5 seconds to well under a tenth of a second.
- **Free unused textures**: the game no longer keeps using more video memory every time the house loads (after dying or sleeping); that was about 1.5 GB per load in a messy house.
- **Peephole camera only near the door**: the front door's peephole only films the porch while you're close enough to look through it, which saves about a sixth of each frame everywhere else.
- **Fewer shadows**: choose which lights cast shadows in Mod Settings, under **Shadows**: room lights (default: lights you switch on and off and the flashlight), none (fastest), or all lights like the original game.
- **Fewer moon shadows**: the moon's shadows cost a lot of each frame and hardly show. Pick **Moon shadows** in Mod Settings: only outdoors (default: shadows in the front and back yard, none inside, where the moon doesn't reach anyway), off, or on like the original game. Lightning flashes keep their shadows with every choice, so storms look the same. It's your own setting, so in co-op everyone picks theirs.
- **Less log spam**: no more tens of thousands of warnings in the log while you're dead or hanging.
- **Idle paint surfaces rest**: surfaces only do work when something lands on them.
- **Characters plan their walk once**: trick-or-treaters, the cat, rats and others don't recalculate their path every frame.

## Open Saves

- **Compressed saves**: saves are about 16 times smaller (`.nsav` files, an open format). Old saves still load. Turn it off to save plain JSON like the original game.
- **Save cleanup**: keeps only the newest saves (25 by default, see Mod Settings) instead of a new file piling up every time the game saves.

## Open House

Opens what the demo closes off. Each area is its own switch.

- **Open the pantry** (on): the pantry next to the kitchen, with everything inside usable.
- **Open the basement backroom** (on): the black door behind the boxes in the basement, without hunting for its key.
- **Open dad's yard** (on): the yard door in the living room. Switch it off to open it the game's way, with the key lying nearby.
- **Open the attic** and **Open the roof** (off): areas the demo walls off; parts may be unfinished. The Queen of Dust lives in the attic and pays for dust balls.
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

- **New calls ring in**: how often a random call is a new one instead of one of the game's prank calls is set in Mod Settings (**New call chance**, 50% by default).
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

- **New emails arrive**: new emails show up every few minutes while you play, after store orders, when you get home and when you wake up. How often is in Mod Settings (**Fewest/Most minutes between emails**, 4 to 10 by default).

Modders can add emails as JSON or Ink files in any mod's `email` folder.

## Custom TV Stations

Your own videos on the living room TV. Put MP4 or WebM files into a mod's `tv` folder (there's an example station in `Mods/TvStations/tv`) and they show up as stations, next to the game's channels.

- **Keep the game's channels**: switch off to watch only your own stations. Single channels can be hidden with a `stations.jsonc` in the `tv` folder.
- **Station volume** in Mod Settings.
- A station whose video isn't playing yet shows an old "Please stand by" card instead of a black screen.
- In co-op everyone's stations are on everyone's TV, and the videos play in step.

## Mixtapes

Your own music on cassettes for the radio. Put OGG, MP3 or WAV files into a mod's `mixtapes` folder (there's an example tape, Pumpkin Mix, in `Mods/Mixtapes/mixtapes`) and each one is a tape: sold in the computer's store, and if you want, left lying in a room of the house. Put it in the radio like the game's own tapes.

- **Mixtape radio**: with no tape in, the radio plays your songs one after another, shuffled, like a station. It still needs a fuse and the music button.
- **Tape price** in Mod Settings. Names, prices, colours and the room a tape is left in go in a `tapes.jsonc` in the `mixtapes` folder.
- In co-op everyone's tapes are in everyone's game, and a radio plays the same song at the same moment for everyone.

## Update Checker

Tells you when a newer version is out. Always on; switch off **Check for updates** in Mod Settings to keep it from going online.

- **The check**: when the game starts and then every hour, the mod asks GitHub (`github.com/Official-Husko/NeetExtended`) for the newest release. It sends nothing about you or your game. If you're offline or GitHub doesn't answer, nothing happens and nothing is shown.
- **In the corner**: while a newer version is out, the version label in the bottom left says `update available (v0.3.0)`.
- **In the game**: while a newer version is out, the game sends you one email and rings your phone up to twice per save, about once an hour of play. They're rude about it, like everything else in the game. In co-op only the host gets them.
- **Updates page** in the mod menu (Mod Settings): the version you have, the newest one found, the release page, CHECK NOW and COPY LINK. The mod never updates anything by itself; you get the new release the same way you got this one.
- **Check for updates** in Mod Settings (on by default). Off: the mod never goes online.
- **Tell me in the game** in Mod Settings (on by default). Off: only the note in the corner.

## Weather Expansion (experimental)

Changes how the storm outside looks. Each look is its own switch on the Mods page, and the amounts are in Mod Settings under "Weather Expansion". It's all drawn by your own game, so in co-op everybody sees their own.

- **Stormy fog**: the night's fog is a dark blue-grey instead of pure black, so the house and the trees show as shapes in the haze, and it slowly thickens and thins. **Fog thickness** and **Fog brightness** in Mod Settings.
- **Lightning glow**: a lightning flash lights up the fog and the sky as well as the things near you, so the sky shows the landscape behind the yard for a moment. **Lightning glow** in Mod Settings.
- **Mist in the yards**: banks of mist drift over the front and back yard and light up with the lightning. **Mist** in Mod Settings.
- **Rain on the windows**: drops sit on the windows and run down them, bending the view outside, and you can see them in the dark through a thin haze on the glass. Broken windows lose their drops. **Rain on the windows** (how many) and **Window haze** in Mod Settings.

## Randomizer

Off until you switch it on: on the Mods tab, pick Randomizer and tick **Load when the game starts** (or press LOAD NOW to try it right away).

Every game is a little different. Each save gets a seed that decides:

- **Shuffled visitors**: who knocks at night comes in a different order.
- **Random prices**: the store charges more or less for everything (how much in Mod Settings). Money keeps its value.
- **Mystery deliveries**: sometimes an order brings something else from the same store, for what you paid (how often in Mod Settings).
- **Shuffled house**: on a new game, loose things in the house swap places. Keys and notes stay, and nothing ends up somewhere you can't reach.
- **Random needs**: hunger, energy, bladder and hygiene drain faster or slower (how much in Mod Settings).

Each part can be switched off. **Seed for new games** in Mod Settings lets you play a seed someone shared; the **Randomizer** page on Mod Settings shows yours.

## Co-op

Play the house together, over the internet or your own network. The host's house is the shared world: its items, money, doors, lights and the people in it. Everyone keeps their own body: health, needs, status effects and clothes.

- **Host**: load a game, open the mod menu (F1), **Co-op** tab, **OPEN MY HOUSE**. Friends connect to your address on UDP port 8889 (setting **Port**). The port has to be open in your router and firewall; a VPN like Tailscale or ZeroTier works too.
- **Join**: **Co-op** tab, type the host's address, **JOIN**. You don't need a save, and your own saves aren't touched while you're in someone else's house.
- **Over Steam** (the Co-op over Steam mod): host with **OPEN OVER STEAM** and friends join with the Steam ID the Co-op tab shows you. No port forwarding. Needs Steam running and a one-time download of Valve's `steam_api.dll`: the installer offers it, or run `neet steam`. No invites or lobbies yet.
- **Shared**: things you pick up, throw, eat, break or order (windows, glass cases and breakable walls too: hit one with a throw and it breaks for everyone, and stays broken for whoever joins later); money; doors and locks, lights, fuses, the TV, taps, the stove, flashlights and lighters; how cooked, spoiled, soaked, dirty or burnt things are, what a glass holds and how much is left, melting ice, dust ball sizes and merges, the water level in sinks and the bath, butter on food, egg cartons, toilet paper, vacuums, guns' magazines, the piggy bank and the cat's bowl; fire (flames appear and go out for everyone as they do on the host, and candles, lighters and burning things are lit or out for everyone); the stove's gas, which lights and explodes once, the same for everyone; how hot a pan is (what cooks in it and what burns you is the same for everyone), laser pointers, the balls of fire a spray makes when something lights it (a lighter at the nozzle), the fire puff of a burnt tax bill, fuse and lighter explosions; visitors and the other characters, who live on the host's game.
- **Your own body comes back**: the host's save keeps your needs, clothes and where you stood, so next time you join you pick up where you left off.
- **Your clothes show on your body for everyone**: the other players see exactly what you wear, including a custom shirt from the custom shop with your own picture on it, the pants you shat in, and your hats, masks, glasses and cigarettes, which ride on your head. When you leave, what you wore on your head is put away instead of hanging in the air, and it's back on your head when you come back.
- **Down, not dead**: whatever would kill you (your health running out, a story death, the skeletonizer, an explosion gibbing you) puts you down instead. You get 15 seconds; then the death that put you down happens as it would in single player, skeleton, gibs and respawn included. Anyone, in any game, can bring a downed player back: use food on them (look at them, use it; 30 health) or a healing pill (60 health), give them a sip from a cup of water or blood (20 or 35 health), or, with an empty hand, hold that hand's button on them for 3 seconds (15 health, a bar shows how far you are). A quick tap of the same button slings them over your shoulder instead, and either hand button puts them down; the hand that picked them up stays busy while you carry them, but you can still feed or give a drink to the one you carry.
- **Everyone sees and hears the same people**: visitors crouch, kick, strip, wear and lose masks, and get skeletonized or blown up (with the blood and the skeleton's red flush) the same way in every game, they groan, grunt, punch and walk with sound for everyone, and stripping a corpse in your game strips it for everyone.
- **Enemies pick the nearest player**: the cops, the hostile visitor, house spiders and the skeleton parasite go after whoever is closest and still standing, and leave downed players alone.
- **Paint is shared**: blood, poop, piss, water, scorch marks, footprints and cleaning show up for everyone, whoever made the mess.
- **Your files are shared**: custom photos and tapes from the custom shops, photos you take with the camera, the wallpaper you pick on the computer and your face go to the other players, and theirs come to you. Everyone's TV stations and mixtapes are in the game for everyone. When you join you get the files already in the game first, then the house loads with all of it there; files added later come in the background. A note in the bottom right corner shows what's being sent and received.
- **The TV plays in step**: a video waits on its "Please stand by" card until everyone has it, then starts for everyone at once and stays in step with the host's TV.
- **The radio plays in step**: a tape in a radio, and the mixtape radio station, play the same song at the same moment for everyone.
- **Bodies move like their players**: other players' arms reach where their hands are (holding an item, reaching out, punching), look up and down with the camera and kick when the player kicks.
- **Who's coming and going**: a note in the corner says when someone is joining, joins or leaves.
- **The host's Players tab** (mod menu): pick a player, then help them (heal them and get them back up, bring them to you, go to them, give them any item, kick a player who's stuck or bugged) or make trouble (send the hitman, slap, yeet, spin, freeze, starve, piss alarm, make them shit, knock them down, set their room on fire, or drop a spider, a rat or Poopman next to them). They get a note saying who did it.
- **Disconnect**: in someone else's house the pause menu's **Get out** button says **Disconnect** and takes you out of their game.
- **Cheats are the host's**: the cheat menu doesn't open in someone else's house, and the host's game speed stays normal while playing together.
- **Talking and the phones**: everyone can talk to the characters and use both phones. The host's game runs the conversation, so whatever it gives, costs or sets off happens once, and what it checks or does to you (your needs, what you're holding, knocking you out) is about whoever is talking. A character talks to one player at a time: anyone else gets a busy line. Everyone close to the character (or the phone) watches the conversation along: within about 8 metres, and in the same room or with a clear line to them, so the room next door doesn't hear it. They see the same lines at the same moment, the choices when the talker brings them up and the one they pick, and hear the same voice blips from where they come from. You keep playing while you watch, you can't click their choices, and starting a conversation of your own takes the box over.
- **The hitman line**: in co-op, call 448-626. Pay $69, pick a player (or yourself), and about a minute later the van comes: its agents go straight for that player and leave once they're down. One job at a time, then the line waits a while before the next (setting **Hitman line cooldown**, 10 minutes by default).
- **In co-op** nobody dreams (resting with your eyes shut still restores energy), the story's scene changes wait, and the mod and cheat menus don't pause the game. Going back to the main menu leaves; when the host leaves, the game ends for everyone.
- Everyone needs the host's mods installed, in the same versions. Switched on or not doesn't matter: a joining game loads what the host runs (and sets aside what the host doesn't) until it leaves. Mods that only change your own game (Cheat Menu, Performance Fixes, Open Saves and the like) can differ.
- **The host decides the mods**: the host's mods, their switches and the settings that change the house (prices, chances, timings) count for everyone. Your mod menu shows them locked while you're in someone's game, and you get your own setup back when you leave.
- Settings: **Your name** (shown over your head), **Port**, **Max players** (2 to 4), **Hitman line cooldown** (the host's counts), **Physics hands on other players** (experimental: other players get the floppy first-person arms you see on yourself, reaching for what they hold, instead of their body's own arms), **Biggest file to share** (512 MB; bigger files aren't sent or taken), **File upload speed** (4096 KB/s), **Get files before joining** (on; off loads the house straight away and the files follow), **Show file transfers**, **Show players joining and leaving** and **File cache size** (2048 MB; other players' files are kept in `Mods/Coop/cache`, so each one is only sent once).
- Not everything is shared yet: see [Known limitations](docs/modding/coop.md#known-limitations).

## Card Expansion

More for the computer's anime card game.

- **Spooky card set**: twelve Halloween cards after the game's 66.
- **Summons**: a Summon button that spends crystals (500 by default) on a random card payout, or a shiny card with free levels.
- **Auto-clicker**: buy it once with crystals (5,000 by default) and it clicks every unlocked card for you.
- **Better crystal exchange**: converting crystals pays $5 per 10K instead of $3 (set it in Mod Settings).

Costs, chances and the click rate are in Mod Settings.

## Pumpkin Smash

A whack-a-pumpkin game on the in-game computer (address `pumpkin`). Smash pumpkins for 30 seconds, beat your best score and earn a little cash for each one. Round length and cash per pumpkin are in Mod Settings.

## Cheat Menu

Press **Insert** (changeable in Mod Settings). Four tabs:

- **Player**: god mode, needs that don't drain, fill all needs, movement speed, teleport.
- **Spawn**: items, people and creatures.
- **World**: start or put out a fire, power on or off, unlock all doors, game speed.
- **Computer**: money, taxes, crystals and the card game.

The game keeps running while the menu is open unless you turn on pausing in Mod Settings.

In co-op only the host can open it, and the game speed stays normal while the house is shared.

## No Feedback

Always on. The pause menu's feedback form would send reports about a modded game to the original developer, describing code they never wrote; this stops it and hides the form. The MODS section takes its place.
