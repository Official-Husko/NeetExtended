# Update Checker

The Update Checker mod (`mods/UpdateChecker`) finds out whether a newer release of the pack is out. While there is one, the version label in the corner says so, and (if the player allows it) the game sends an email and rings the phone about it. It never downloads or installs anything. It's always on (`"essential": true`), so every player hears about updates; the **Check for updates** setting still keeps it offline.

## What it does

- **Checks** when the mod loads (game start) and then every hour of real time. One request to `https://api.github.com/repos/Official-Husko/NeetExtended/releases/latest`, with the headers `User-Agent: NeetExtended/<version>` and `Accept: application/vnd.github+json` and a 10 second timeout. It reads `tag_name` (`v0.3.0`; the `v` and anything after a `-` or `+` is dropped) and `html_url`, and compares the tag with `PackInfo.Version` as a `System.Version`.
- **Fails quietly**: offline, a 404, GitHub's rate limit or Proton network trouble each write one Info line to the log (`Update check failed: ...`) and show nothing in game. The Updates tab shows the same reason as plain text. A failed check keeps what an earlier check found.
- **Corner note**: while an update exists, `VersionTag.AddNote` adds `update available (v0.3.0)` after the pack's version. The note goes when the update is gone (the check was switched off) or the mod unloads.
- **Tells the player in the game** (details below).
- **Updates tab** in the mod menu: the version running, the newest one found, the release page as plain text, the status of the last check, CHECK NOW (one check even with the setting off) and COPY LINK.

## Settings

Mod menu, Settings, "Update Checker" (`Mods/UpdateChecker/config.json`):

| Setting | Default | |
| --- | --- | --- |
| Check for updates | on | Ask GitHub at start and every hour. Off: the mod never goes online. Switching it on checks right away; switching it off forgets what was found. |
| Tell me in the game | on | Send the email and ring the call while an update is out. Off: only the corner note. |

## Telling the player

Only in the game that runs the house (`WorldRole.RunsWorld`, so the host in co-op), only while an update is out, and only when "Tell me in the game" is on. It happens

- the first time the house is ready in a launch (if the check answers later, then when it answers), and
- then once per hour of play (`ModFlags.PlaySeconds`).

Each time: if the update email wasn't sent in this save yet, it sends it; otherwise, if the update call rang fewer than twice in this save, it rings it. So a save gets at most one email and two calls. The counts are `ModFlags` (saved with the game): `updatechecker_email` (0 or 1), `updatechecker_calls` (0 to 2) and `updatechecker_hour` (the hour of play it last told). Nothing here needs the phone or email mod: it runs the shared `email` and `ring` rule actions (`RuleActions.Run`). Without Email Expansion the first notice is a call instead; without Phone Expansion calls are skipped.

## The content

Both files live in the mod's own folders and are read by the phone and email mods like any other mod's:

- `email/update.jsonc`: "ur mods r OLD AS FUCK". Trigger `manual`, so it only arrives when the mod sends it. Opens with one of six voices (three nag, three sales pitches) and ends with replies, one of which, "How do I update?", says to go to where you got the mods and look in the mod menu.
- `phone/update.jsonc`: "The Update Guy" (caller ID UPDATE), no number, `incoming` trigger `manual`. Six voices again, then the same kind of choices.

If the player has updated by the time they open the email or pick up the call, both notice ("u updated???") and drop it.

Mod code adds to the shared rules:

| | |
| --- | --- |
| Text value `{{updateVersion}}` | The newest version found, without the `v`: `v{{updateVersion}}`. "new" before any check worked. |
| Condition `"updateAvailable": true` | True while a newer version than the one running is out (`false` for the opposite). |

## Code

Namespace `UpdateChecker`. Everything is internal; the mod has no API for others. Other mods that want a note in the corner use the framework's `VersionTag.AddNote` (see [framework.md](framework.md#pack-name-and-version)).

| File | |
| --- | --- |
| `Check/UpdateState.cs` | `Available`, `Latest`, `Url`, `Status` and a `Changed` event. Parses and compares versions. |
| `Check/ReleaseCheck.cs` | The request (a `UnityWebRequest` in a coroutine) and the answer. |
| `Check/CheckSchedule.cs` | At start, then every hour. |
| `Check/CornerNote.cs` | The version label note. |
| `Notice/UpdateNotice.cs` | The email and call, the counts. |
| `Rules/UpdateRules.cs` | `updateVersion` and `updateAvailable`. |
| `Menu/UpdatePage.cs` | The Updates tab. |
| `Testing.cs` | Hooks for the dev bridge. |

## Testing

The real latest release can be the one that's running, so `UpdateChecker.Testing` fakes one. Through the dev bridge:

```sh
neet call type.call type=UpdateChecker.Testing method=FakeLatest 'args=["0.9.0"]'   # pretend v0.9.0 is out
neet call type.call type=UpdateChecker.Testing method=Report                         # state, flags, whether telling is allowed
neet call type.call type=UpdateChecker.Testing method=TellNow                        # what the hourly notice does ("email", "call" or "nothing")
neet call type.call type=UpdateChecker.Testing method=Clear                          # forget the fake
```

Faking an update in a launch that hasn't told yet also runs the first-time notice. `neet call console.exec '{"command":"setFlag updatechecker_email 0"}'` (and `_calls`, `_hour`) puts the counts back. Reloading the mod checks GitHub again.

## Known limitations

- It only looks at GitHub's latest release. Pre-releases and drafts don't count, and a tag that isn't a version (`v0.3.0-beta` is read as 0.3.0) is ignored with a log line if it can't be read at all.
- GitHub allows 60 unauthenticated requests an hour per address. Normal play makes one; a lot of mod reloads in a row can use them up, and the check then fails quietly until the limit resets.
- The check runs from the main menu too, but the email and call only happen in the house.
- If the email or call file is missing (the mod folder was edited), the action logs a warning, and the count still goes up.
- The email and call say "where you got these mods" because the mod can't know how the player installed the pack; the Updates tab has the release page.
- A client in co-op sees the corner note but never gets the email or call; the host's inbox and phone get them.
