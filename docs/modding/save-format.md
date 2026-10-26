# Save format (.nsav) and the OpenSaves mod

NEET Demo writes saves as indented JSON. The OpenSaves mod can write them in `.nsav`, a small container around the same JSON, compressed with Deflate. On a real save, the indented JSON measured 555 KB and the `.nsav` file 33 KB.

Both formats load while OpenSaves is installed. Plain JSON loads with or without the mod.

The codec is `mods/OpenSaves/Format/NsavCodec.cs`. The `neet` tool compiles the same file (it is linked into `tools/NeetTool`), so the tool and the mod always agree on the format.

## Container layout

All integers are little-endian.

| Offset | Size | Field | Meaning |
| ------ | ---- | ----- | ------- |
| 0 | 8 | magic | ASCII `NEETSAVE` |
| 8 | 1 | version | Format version. Currently `1`. |
| 9 | 1 | codec | `0` = plain UTF-8 JSON, `1` = raw Deflate (RFC 1951) of UTF-8 JSON. |
| 10 | 2 | reserved | `0`. |
| 12 | 4 | JSON length | Length of the uncompressed JSON in bytes. |
| 16 | 4 | CRC-32 | IEEE 802.3 CRC-32 (the zip and PNG polynomial) of the uncompressed JSON bytes. |
| 20 | n | payload | The JSON, or the Deflate stream of the JSON. |

The header is 20 bytes. The payload runs to the end of the file.

Codec `0` stores the JSON as it is, so the payload length is the file length minus 20. Codec `1` is a raw Deflate stream without a zlib or gzip wrapper. The encoder uses .NET's `DeflateStream` at `CompressionLevel.Optimal`. The writer always uses codec `1`.

### Detection and checks

A file is a container when it is at least 20 bytes long and starts with `NEETSAVE`. Any other file is treated as plain text.

When reading a container, the decoder:

1. Rejects a version greater than 1 with `Save format N is newer than this version understands (1).`
2. Rejects an unknown codec.
3. Decodes the payload and checks that the result has the stored length and CRC-32. A mismatch fails with `The save file is damaged (length or checksum mismatch).`

`neet save info` prints the CRC-32 as eight hex digits.

## JSON content

The payload is the save's JSON, the same document the game writes in plain form. The root object has a `Save` array. Each entry has a `type` field, which `neet save info` groups by. Mods add their own entries to this array. Other fields are described in the game's own code.

## Old JSON saves

A plain JSON save has no header. It is read as it is, so saves made before OpenSaves was installed still load. Any file that does not start with the magic is read as UTF-8 text.

## How the game picks a save

The game writes a new file for every save. The name is `neetSave_` followed by the local date and time without spaces, slashes or colons, and the extension `.json`. It never deletes old files.

When loading, `SaveLoadHandler.LoadAll` reads the newest file in the save folder whose name starts with `neet`, by last write time. The extension does not matter, so a `.nsav` file written last is loaded. This is also true for files written by `neet save decode` or `neet save encode`: a file written into the save folder becomes the newest save.

The save folder is inside the game's Proton prefix on Linux, at `AppData/LocalLow/NEET Games/NEET` under `drive_c/users/steamuser`. `neet status` prints its path.

Loading goes through `SaveFormats.ReadText` (see below). `neet` and the mods use the same rule to find the save: `SaveFiles.Newest` in `src/NeetModKit/Saves/SaveFiles.cs`.

## OpenSaves

OpenSaves is an optional mod with two features, both on by default. Each one can be switched at runtime with the bridge: `neet call features.set mod=OpenSaves id=CompressedSaves enabled=false`. The second feature's id is `SaveCleanup`. The choice is remembered across launches.

### Compressed saves (`CompressedSaves`)

While this feature is on, `SaveLoadHandler.SaveAll` writes `.nsav` files. Two calls in the game's method are swapped:

- `File.WriteAllText` becomes `SaveWriter.WriteSave`, which writes the `.nsav` container under the same name with the extension replaced.
- `JToken.ToString(Formatting, converters)` becomes `SaveWriter.Serialize`, which writes compact JSON instead of indented JSON.

Switching the feature off makes the game write indented JSON again. Reading `.nsav` stays on while the mod is loaded, so a save written with the feature on still loads after it is switched off.

### Save cleanup (`SaveCleanup`)

After each `SaveAll`, the mod deletes `neetSave_*` files in the save folder beyond the newest N, by last write time. The newest save is counted, so the save just written is kept.

N is `KeepSaves` in `Mods/OpenSaves/config.json`:

```json
{
  "KeepSaves": 25
}
```

The settings screen shows it as "Saves to keep". The range is 3 to 100, and the code never keeps fewer than 3 even if the file says less. Only `neetSave_*` files are removed.

## Removing OpenSaves

With the mod gone, the game can still read plain JSON saves. A `.nsav` save makes `ReadText` fail with:

```text
neetSave_<time>.nsav isn't a JSON save and no installed mod can read it. If it ends in .nsav, install OpenSaves.
```

The game loads only the newest file, so the load fails and does not use an older save. Two ways out:

- Reinstall OpenSaves.
- Convert the file to plain JSON with the tool, then put the JSON back in the save folder:

  ```sh
  neet save decode "neetSave_<time>.nsav" restored.json
  cp restored.json "<save folder>/neetSave_restored.json"
  ```

  Write the decoded file somewhere outside the save folder first, as above. The copy has a new modification time, so it becomes the newest save. Keep the name starting with `neet`.

`neet save decode` works without the mod, because the codec is compiled into the tool.

## Reading and writing with the tool

```sh
neet save list                       # save folder, newest first
neet save info "<file>"              # format, sizes, CRC-32, entry counts
neet save decode "<file>" out.json   # .nsav or plain JSON to indented JSON
neet save encode out.json            # JSON to .nsav (compact); checks the JSON first
```

`encode` parses the input before writing, so a syntax error in hand-edited JSON is reported by the tool rather than by the game. The default output of `decode` is the input name with a `.json` extension, in the same folder. That file is then the newest save. Use a different folder or name when that is not wanted.

## Format hooks for other mods

The game reads saves through `SaveFormats` (`src/NeetModKit/Saves/SaveFormats.cs`). A `LoadAll` transpiler (`SaveReadPatch`) replaces `File.ReadAllText` with `SaveFormats.ReadText`. Any mod can add a format that is read by that call:

```csharp
public interface ISaveFormat
{
    string Name { get; }
    bool CanRead(byte[] data);   // true if the bytes are this format
    string Read(byte[] data);    // returns the save's JSON text
}
```

Register a format from the mod's `OnLoad`:

```csharp
using NeetModKit.Saves;

SaveFormats.Register(this, new MySaveFormat());
```

`Register` takes the owning mod, so the format is removed when that mod unloads.

`ReadText` tries the formats in registration order and uses the first one whose `CanRead` returns true. If none does, it accepts the file as plain JSON when the first non-whitespace byte is `{` or `[`. Otherwise it throws the message shown in the previous section.

Only reading is pluggable. The writing side is the game's own `SaveAll`. To write another format, patch `SaveAll` the way `SaveWritePatch` does for `.nsav`.

`OpenSaves.Format.NsavFormat` is the reference implementation: its `CanRead` checks the magic, and its `Read` calls `NsavCodec.Decode`.
