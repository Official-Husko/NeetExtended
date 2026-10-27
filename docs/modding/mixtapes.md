# Mixtapes

The Mixtapes mod (`mods/Mixtapes`) puts your own music on cassettes for the game's radio. Each song file is a tape: it's sold in the computer's store, can be left lying in the house, and plays when you put it in the radio. With no tape in, the radio plays all of them like a station.

## Adding songs

Put song files in `Mods/Mixtapes/mixtapes/` (or the `mixtapes` folder of any mod, so a mod can be just a `mod.json` and a folder of songs). They show up within a second, no restart needed. The mod ships one example, Pumpkin Mix.

- **OGG (Vorbis)** works best: small, and kept compressed in memory while it plays.
- **MP3** and **WAV** work too. MP3s are decoded once when they load, so a long one takes more memory (about 10 MB a minute).
- Anything else (FLAC, M4A, ...): convert it first.

```sh
ffmpeg -i song.flac -c:a libvorbis -q:a 4 song.ogg
```

A file that can't be played shows up on the **Mixtapes** tab in the mod menu and in the log.

## Using them

- **Buy one** on the computer's store page (Pun Store), next to the game's items. The default price is 15$ (Settings: **Tape price**).
- **Find one**: a tape with a `house` room in its list entry is left in that room once per save.
- **Play it**: hold the radio, open it, and use the tape on it (or drop it in), like the game's own cassettes. Close it and press the music button. The radio needs a fuse for power.
- **Mixtape radio** (switch on the Mods page, on by default): with no tape in, the radio plays your songs one after another, shuffled, never the same twice in a row. Putting a tape in takes over.
- The **Mixtapes** tab lists every tape with a GET ONE button that drops one in front of you.

Saves keep tapes by name ("Mixtape: Pumpkin Mix"). If the song or the mod is gone, the tape is left out when the save loads.

## tapes.jsonc

Optional, in the same folder (`tapes.json` works too):

```json
{
  "tapes": [
    {
      "file": "pumpkin-mix.ogg",
      "name": "Pumpkin Mix",
      "price": 10,
      "color": "#ff7518",
      "house": "Bedroom_Neet",
      "description": "A tape somebody made for you."
    },
    { "file": "night-drive.mp3", "store": false, "house": "Livingroom" }
  ]
}
```

| Field | Meaning |
| --- | --- |
| `file` | The song, relative to the folder. |
| `name` | What's written on the tape; the item is "Mixtape: " and the name. Defaults to the file name. Names have to be unique. |
| `price` | Store price. Defaults to the **Tape price** setting. |
| `store` | `false` keeps it out of the store. |
| `color` | Tints the cassette: `"#rrggbb"` or `[r, g, b]` from 0 to 1. |
| `house` | A room one copy is left in, once per save: Bedroom_Neet, Livingroom, Kitchen, Basement, Lounge, ... (the cheat menu's room list). |
| `description` | The store text. |

Songs listed come first, in the list's order; other songs in the folder follow by file name. `//` comments are allowed.

## How it works

- Each tape is a modded item (`ItemRegistry`) based on the game's `Cassette`, tinted and priced from the list. Its template gets the song's `AudioClip` once it has loaded, so copies made from it have the song; copies already in the house when it finishes loading (from a save) get it then.
- Songs load with `UnityWebRequestMultimedia.GetAudioClip` in the background when the files load, so they're ready before a save needs them. OGG is kept compressed in memory (`DownloadHandlerAudioClip.compressed`); MP3 isn't, because playing a compressed MP3 on the radio crashed the game under Proton.
- The radio (`Radio`) plays `cassette.musicClip` on a looping `AudioSource`. The station is a postfix on `Radio.Update`: with no cassette, the radio switched on (`on`) and powered, and nothing playing, it plays the next song with looping off. A prefix on `InsertCassette` stops the station and turns looping back on, so the game's own code plays the tape as usual.
- The folder is read with the framework's `MediaFolder`, the same reader Custom TV Stations uses.
- The game has its own way to make a tape from a file: the cassette site's custom shop (`CustomItemShop` with `CustomMusicSaveLoader`), which asks for a file with a file browser. That still works and keeps its song through saves; Mixtapes is for songs that ship with mods.
