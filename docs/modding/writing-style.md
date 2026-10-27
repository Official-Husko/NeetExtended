# Writing calls, emails and other game text

Anything a player reads inside the game's story (phone calls, emails, notes, thoughts) should
sound like NEET wrote it. Mod menus and settings are the exception: those stay plain and clear.

The best reference is the game itself. Call every number, read the spam in the inbox, listen to
the prank calls. `neet call assets.text` can dump the game's Ink stories (`Master`, `Master_Email`,
`Thoughts`) if you want to read them all at once.

## The voice

**Crude, loud and stupid on purpose.** NEET swears, does toilet jokes, dirty jokes and dark jokes,
and doesn't censor any of it. A survey caller asks "On a scale of 1 to 6 how would you rate the
burning of the hole?". A tax email comes from the Collections and Usury Minister at CUM.com. Write
"fuck", not "f***".

**Dick jokes and dirty insults.** The game loves them: "Mandatory Penis inspection day is
forthcoming. Make preperations.", a spam email that just says "pennis", a vet who calls the player
"you son of a bitch", a goth who's the "Defacto boss bitch", a player reply that's just "Slut!".
Use them freely: penis, pennis, dick, dickhead, wiener, balls, bitch, son of a bitch, slut, whore,
asshole, dipshit.

**Absurd escalation, not wit.** A normal setup that goes off the rails: a pie store that delivers
in pi weeks, a gamer tip line whose host starts telling you his wife left him. Dry, clever sitcom
one-liners ("Your complaint has been noted by nobody.") don't fit.

**Bad grammar where the character would have it.** Scammers write broken English full of typos
("hello waht iz pasword?", "Yes sir extremely happi for you the dollars!", "THANK U"). Kids and
stoners talk lazy ("yo dawg watchu want?"). Normal adults talk normally, just blunt. Misspell what
that person would misspell, not every word everywhere.

**Short lines.** One thought per line, lots of one-word lines ("Speed?" "Weed?" "Seed and feed?").
Cut people off mid-word ("Does your refridgerator r-"). Use "...", "*click*".

**Rich text for volume and menace**, in what callers say and in email bodies:

| Tag | For |
| --- | --- |
| `<size=150%>SIR ARE YOU ASSHOLE?` | yelling (with caps) |
| `<size=60%>clarice it's another one<size=100%>` | muttering, someone in the background |
| `<color=red>yo Satan here who dis?` | Satan, threats, warnings |
| `<i>Don't use the machine.</i>` | something that should land quietly |

Use them where they make the joke, not on every line. Write colours without quotes
(`<color=red>`), so nothing needs escaping in JSON. Keep tags out of a choice's text: choice
buttons use Unity's older text component, which doesn't understand sizes in percent.

**The player is a shut-in.** No job, lives in mom's house, plays video games, orders junk off the
computer, doesn't shower. Callers and senders can roast that, and mom enables it. It's Halloween.

**Choices are what the player says**: short and blunt, often rude or dumb ("Fuck off man",
"Sure it's 12234"). Three or fewer per menu.

## Limits

These are about real harm, not taste:

- No racial, ethnic, homophobic or disability slurs (the game has none). The game's own insults
  above are all fine, and so are made-up cults and churches, Satan and ghosts.
- Kids in calls can swear, call people dickheads and do gross-out stuff, like the game's own kids.
  No sexual jokes with or about a kid.
- No real people, companies, websites or phone numbers.
- No jokes about the game itself, its developer or its development.
- Don't tell the player to kill or hurt themselves.

## Before and after

Too tidy:

```json
"Please hold. Your call is important to us, in the sense that we're keeping it."
```

Sounds like the game:

```json
"PLASE HOLD",
"<size=60%>...is he still there? Kevin is he still there",
"<size=100%>Sir your call very important to us. We keep it forever. Thank u"
```
