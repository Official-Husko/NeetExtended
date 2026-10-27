# TV stations

The Custom TV Stations mod (`mods/TvStations`) puts your own videos on the living room TV. Each video file is a station: next to the game's six channels, in place of one of them, or with the game's channels gone.

## Adding videos

Put video files in `Mods/TvStations/tv/` (or the `tv` folder of any mod, so a mod can be just a `mod.json` and a `tv` folder of videos). They show up within a second, no restart needed. The mod ships one example, Pumpkin TV.

- **WebM (VP8 video, Vorbis audio)** always works: Unity decodes it itself.
- **MP4 (H.264, AAC)** works under Proton too, through its media support. MOV, M4V, OGV, AVI, WMV and MPEG files are tried the same way.
- If a station shows **color bars with a box of noise** instead of your video, Proton couldn't decode the file and played its placeholder. Convert it to WebM:

```sh
ffmpeg -i input.mp4 -c:v libvpx -b:v 2M -c:a libvorbis output.webm
```

On plain Windows (or Wine without a placeholder), a file that can't be played is listed in the error report (the mod menu's Errors tab) with the same advice.

Videos are stretched to the screen; 16:9 fills it exactly. They loop, and their sound comes from the TV's speaker, so it's quieter across the house like the game's channels.

## Channels and the remote

The remote's six channel buttons pick channels 1 to 6. Hitting the remote goes to the next channel, so stations after the sixth are reached that way, or from the **TV** tab in the mod menu (F1), which lists every channel with a WATCH button. Out of the box the order is:

| Channel | |
| --- | --- |
| 1 to 6 | The game's Static, Bingo, Snail, Church, Sports, Rap |
| 7 and up | Your stations |

To put a station on one of the six buttons, show it **in place of** a game channel. To drop game channels, **hide** them. Both are done in a `stations.jsonc` (or `stations.json`) in the same folder:

```json
{
  "stations": [
    { "file": "pumpkin-tv.webm", "name": "Pumpkin TV", "volume": 0.8, "replace": "Sports" },
    { "file": "late-show.mp4", "name": "Late Show" }
  ],
  "hide": [ "Rap", "6" ]
}
```

| Field | Meaning |
| --- | --- |
| `stations[].file` | The video, relative to the folder. |
| `stations[].name` | Shown in the TV tab. Defaults to the file name. |
| `stations[].volume` | 0 to 1, on top of the TV's volume and the mod's setting. |
| `stations[].replace` | A game channel by name (Static, Bingo, Snail, Church, Sports, Rap) or number (1 to 6). The station takes its button. |
| `hide` | Game channels to take off the TV, by name or number. |

Stations listed come first, in the list's order; other videos in the folder follow by file name. `//` comments are allowed.

The **Keep the game's channels** switch on the Mods page takes all six game channels off at once (stations in their place stay). **Station volume** in Settings sets how loud stations play overall.

## How it works

- The TV fills its private channel list (`TV.channels`) from its children in `Awake`. A postfix right after builds the new list: game channels in order, stations in place of the ones they replace, hidden ones left out, then the other stations. This happens before a save is loaded, so the saved channel number points into the new list.
- A station is a copy of `Channel1_Static` (so it sits on the screen exactly like a game channel) whose `Image` is swapped for a `RawImage`, with a `VideoPlayer` rendering into a `RenderTexture` the size of the video, and an `AudioSource` set up like the TV's speaker. It keeps the copied `ScreenModule`, which is what the TV's list holds.
- A prefix on `TV.Control` handles switching to a station: it switches the other channels off and the TV's channel music off the way the game does, then shows and plays the station. Game channels go through the game's own code, which switches station objects off with the others; power and loading work unchanged.
- Changing files rebuilds the list (keeping the channel number on screen), and unloading the mod puts the game's own list back.
