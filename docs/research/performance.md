# Performance

Why NEET Demo feels laggy, measured in the running game, and what a Performance Fixes mod can do about it. Also: can it run as 64-bit?

## Summary

- **The steady frame rate is not the problem on a fast PC.** In the living room the game runs at 125 to 155 fps uncapped (6.4 to 8 ms per frame) on an i9-9900K with an RX 7900 XT under GE-Proton. The game caps itself at 120 fps.
- **The lag is hitches.** Every save freezes the game for 150 to 400 ms, and longer the longer you play. Every garbage collection freezes it for 9 to 17 ms, about every 8 seconds even when idle. Broken game states throw an exception every frame and log it with a stack trace (85,000 times in one session).
- **The frame's biggest costs are rendering.** Shadows from point lights account for about 1,600 of the 2,500 draw calls, and the front door's peephole camera renders the whole house again every frame. Script code is only about 1.2 ms per frame.
- **Some costs grow with play time.** Paint saves get slower as the house gets messier, and every load of the house (each death, each night) leaves the previous house's paint textures in video memory, about 1.5 GB per load in a messy house.
- **32-bit is not the bottleneck.** The game uses about half a gigabyte of the 4 GB it can address. A 64-bit build works, with all mods, and is about 5% faster and sees all the VRAM. It doesn't fix the hitches.

What has been fixed since, and how much it helped, is in [What's fixed](#whats-fixed).

## How it was measured

The dev bridge has profiling commands (`perf.frames`, `perf.scene`, `perf.scripts`, `perf.gc`, `perf.save`, `perf.savebreakdown`; see [dev-bridge.md](../modding/dev-bridge.md#performance)). They work in a release build: frame times come from `Time.unscaledDeltaTime`, render counts from Unity's `ProfilerRecorder` counters (which a release player still offers), and script and save timing from Harmony patches that wrap methods with a stopwatch for a few seconds and then remove themselves.

Unless noted, numbers are from the living room, standing at (4.09, 0.885, -11.6) facing the TV, uncapped, on: Intel i9-9900K, AMD RX 7900 XT, GE-Proton10-34 with DXVK (Direct3D 11), 1600x900 windowed.

## What a frame costs

`perf.scene` in the living room: 3 cameras rendering, 31 enabled lights (10 cast shadows: 9 point lights and the moon), 2,207 mesh renderers (1,016 visible), 774 rigidbodies (87 awake), 2,956 colliders, 3,751 enabled scripts (1,236 of them `PaintTarget`, one per paintable surface).

Turning costs off one at a time (`perf.frames` uncapped, same spot):

| Change | Frame time | Draw calls | Shadow casters |
| --- | --- | --- | --- |
| As the game ships | 6.43 ms (156 fps) | 2,512 | 2,680 |
| Peephole camera off | 5.33 ms (188 fps) | 2,009 | 2,117 |
| and MSAA 8x off | 5.25 ms | 2,009 | 2,117 |
| and per-pixel lights 15 to 4 | 5.40 ms | 2,006 | 2,117 |
| and all shadows off | 4.08 ms (245 fps) | 386 | 0 |

- **Point light shadows** cost the most. A shadowed point light renders its shadow casters six times (a cube map). The shadowed ones include both fridge lights (range 12), the microwave light and a desk lamp. Every corpse also gets a shadowed point light (`HouseManager.cs:283-292`, `CorpseSoulLight`).
- **The peephole camera** (`HOUSE/Door_Front/Door/PeepholeCamera`) renders everything (culling mask -1) into a 256x256 texture every frame, shadows included, whether anyone looks through the peephole or not.
- MSAA and per-pixel light count made no measurable difference on this GPU. They will on weaker ones.
- The game sets `vSyncCount = 0` and `targetFrameRate = 120` every time a `CameraControl` starts (`CameraControl.cs:102-103`), overriding the player's choice.

Scripts (`perf.scripts`): 1.16 to 1.25 ms per frame for all game scripts together. The top ones are `PaintTarget.Update` (0.31 ms, 1,238 calls a frame), `Flame.Update` (0.2 ms), `BrainTrickOrTreater.Update` (0.17 to 0.19 ms for one object) and `Player_Movement.Update` (0.12 ms, three raycasts per rig). Not where the frame goes, but `PaintTarget.Update` being called 1,238 times a frame to do nothing is easy to cut.

NPCs: every NPC is a copy of the player rig with its own camera, which looked like a big cost, but spawning two NPCs added no rendering cameras and only about 0.1 ms each. Their cameras are off in the prefab.

## Hitches

### Saving: 150 to 400 ms

`perf.savebreakdown` on a save that took 400 ms in a session that had run for about an hour:

| Step | Time |
| --- | --- |
| `SaveLoadHandler.SavePaintSplats` | 370 ms |
| of which `PaintTarget.SaveSplat`, 87 calls | 363 ms (4.2 ms each) |
| `File.WriteAllBytes`, 88 PNGs | 41 ms |
| `AllSaveDataToJSon` (all game data) | 20 ms |

Every save deletes every paint PNG and writes all of them again (`SaveLoadHandler.cs:125-141`), each with a GPU readback (`ReadPixels`), a PNG encode and a new `Texture2D` that's never destroyed (`PaintTarget.cs:89-103`). Surfaces join the list once they've been painted on, so saves slow down as the house gets dirtier: 156 to 185 ms right after loading, 400 ms an hour later. The bedroom autosaves every 300 seconds (`SaveLoadHandler.cs:77-85`), and sleeping and dying save too. Open Saves' compression makes no measurable difference.

### Garbage collection: 9 to 17 ms

The game was built without incremental GC (`GarbageCollector.isIncremental` is false, and it can't be switched on from code), so each collection stops the game in one go: 9 ms with a 44 MB Mono heap, 12 to 17 ms with 60 MB. At 120 fps that's one or two dropped frames, about every 8 seconds when idle and more often when much is going on. Less garbage per frame means fewer collections.

### Errors and log spam

Unity writes every log line, and for errors and exceptions a stack trace, to `Player.log`. Exceptions in Mono are expensive on their own.

- **The thief intro loop:** after Grandpa dies, `SeqSpawnThiefIntro` throws a `NullReferenceException` every frame, forever, repeating its effects. 85,499 of them in one session (`Player.log` of 42 MB). Fixed by Unofficial NEET Patches (`LostCharacters`, `StuckSequences`).
- **"Setting linear velocity of a kinematic body is not supported."** 64,811 times in one session, none in a fresh one. Unity warns each time code sets the velocity of a kinematic rigidbody. Candidates from the code: `Player_Movement.FixedUpdate` writes `rigid.velocity` every physics step while standing still (`Player_Movement.cs:407-421`), and the player's body is kinematic when dead or skeletonized (`Player_Abilities.cs:1882`, `:1891`) or hanging (`HangNoose.cs:69`); `NPCNavigationTest.FixedUpdate` (`:33-34`); hand grabs (`Player_Abilities.cs:800`, `:1074-1089`).
- **`print("restinput")`** in `Player_Movement.cs:286`: about 1,200 lines a session.
- **The house spider save crash** (`InvalidCastException` in `Brain.SaveBasicBrain`) aborted saves and death. Fixed by Unofficial NEET Patches (`SpiderSaves`).

## Things that grow over play time

- **Paint textures from every load of the house stay in memory.** A painted surface (`PaintTarget`) creates four render textures (two colour textures, two float position textures: about 2.5 MB of video memory), two materials and copies of its renderer's materials, and nothing ever destroys them. Measured with `textures.count` after `Resources.UnloadUnusedAssets`: about 2,480 more render textures after every load of the house; after six loads in one session, 12,400 render textures, about 7 GB. `leaks.find` found no managed reference to the old surfaces, so Unity's asset cleanup should have freed them but doesn't.
- **Paint saves** get slower as more surfaces are painted (above), and every save made a `Texture2D` per surface that's never destroyed (`PaintTarget.cs:89-103`). `PaintTarget.ClearWaterChannel` makes one per wet surface every 120 seconds (`HouseManager.cs:407-411`), and loading makes one per saved picture (`SaveLoadHandler.cs:443-466`). These are freed on the next load.
- **Door peephole render textures**: `Door.OnTriggerEnter` creates a new 256x256 `RenderTexture` every time the player walks up to a door (`Door.cs:413-414`).
- **Room fires stay subscribed to saving** after they're put out (`FireSpreader.cs:104`, no unsubscribe in `Clear`). That turned out to be a gameplay bug too: the empty fire is saved and starts burning again on load. Fixed in Unofficial NEET Patches (`GhostSaves`, see [known-bugs.md](../game/known-bugs.md)).
- Not confirmed yet: `Spawner.concurrentSpawnIds` only grows and is written into every save (`Spawner.cs:236-241`, `:112`); `Locator.sitRestEnablers` only grows (`Locator.cs:52-53`).

Earlier versions of this page said status effects leak handlers. They don't (no status effect in the game uses the amplifier type that would), but reading that code found three gameplay bugs instead: effects freeze while a newer one runs, slowdowns get stuck after a load, and lasting effects depend on the frame rate. All fixed in Unofficial NEET Patches.

## Crash on quitting

Every exit of the 32-bit game crashed inside the Mono runtime (`mono-2.0-bdwgc.dll`) right after the mods' quit handlers ran, leaving a crash report in `AppData/Local/Temp/NEET Games/NEET/Crashes`. Narrowed down on a copy of the game: the unmodded game and the loader without mods exit cleanly; a single Harmony patch, on a method that never runs, is enough to crash, and removing every patch before quitting doesn't help. So it's the 32-bit Mono runtime's shutdown after Harmony was used, not the game. The 64-bit player isn't affected. The loader now ends the 32-bit process from `AppDomain.ProcessExit`, which Mono raises just before the crash (see [loader.md](../modding/loader.md#quitting)).

## 64-bit

The game's C# is architecture-independent IL, and its data files are the same for 32-bit and 64-bit Windows apart from a platform number in the header (5, StandaloneWindows), which the player doesn't seem to check. So the 32-bit player binaries can be swapped for the 64-bit ones of the exact same Unity version.

### How it was done

1. Unity's "Windows Build Support (Mono)" module for the macOS editor is the only standalone download of the Windows players for 2022.3.17f1 (changeset 4fc78088f837): `https://download.unity3d.com/download_unity/4fc78088f837/MacEditorTargetInstaller/UnitySetup-Windows-Mono-Support-for-Editor-2022.3.17f1.pkg` (181 MB). There is no Windows-target installer for the Windows or Linux editors; the Windows editor ships these files itself.
2. Unpack it: `7z x UnitySetup-Windows-Mono-Support-for-Editor-2022.3.17f1.pkg TargetSupport.pkg.tmp/Payload`, then `bsdtar -xf TargetSupport.pkg.tmp/Payload`. The 64-bit release player is in `Variations/win64_player_nondevelopment_mono/`.
3. In a copy of the game folder, replace `NEET.exe` with that folder's `WindowsPlayer.exe` (renamed to `NEET.exe`; the name has to match `NEET_Data`), `UnityPlayer.dll`, `UnityCrashHandler32.exe` with `UnityCrashHandler64.exe`, and the whole `MonoBleedingEdge` folder. `NEET_Data` stays as it is, including the loader's injection.
4. Run it through Proton as usual.

It started, loaded the save and all 11 mods (Harmony patches included), and ran normally.

### Results

| | 32-bit | 64-bit |
| --- | --- | --- |
| Frame rate (uncapped, same spot, fresh load) | 123 to 127 fps (7.9 to 8.1 ms) | 133 fps (7.5 ms) |
| Slowest 1% of frames | 10 to 12 ms | 9.8 ms |
| GC pause | 9 ms (44 MB heap) | 12 to 17 ms (60 MB heap) |
| Save | 156 to 185 ms | 165 to 217 ms |
| Scripts per frame | 1.16 ms | 1.14 ms |
| Memory | about 0.5 GB reserved | about 0.5 GB |
| VRAM the game sees | 3 GB | 20 GB |
| Crash on quitting | yes (fixed in the loader since) | no |

About 5% faster, the same hitches. The 32-bit game is far from its 4 GB limit (its exe is already Large Address Aware), so 64-bit only becomes important if a mod loads a lot more content.

### Incremental garbage collection

Whether the player collects garbage incrementally is a build setting, written into `NEET_Data/boot.config` as `gc-max-time-slice=3`. NEET's `boot.config` doesn't have the line; adding it makes both the 32-bit and the 64-bit player report `GarbageCollector.isIncremental = true`. Measured in the house (32-bit, 7 windows of 12 s, same save): without it, one or two full collections per window and 5 frames over 33 ms, the slowest 29 to 35 ms; with it, 4 to 8 slices per window and no frame over 33 ms outside the loading window, the slowest 22 to 28 ms.

### In the installer

Unity's player binaries can't be shipped with a mod (Unity's licence covers shipping them with your own game), so `neet install` downloads Unity's module from Unity on the player's machine and does the swap in place, keeping the 32-bit files to switch back (`--32bit`, the package's `use-32bit` scripts, or Steam's "verify files"). The 64-bit player started as an opt-in (`--64bit`) and is now what every install sets up. `--incremental-gc` adds the `boot.config` line; the release package's install scripts ask about it. Details in [tools.md](../modding/tools.md#install).

### Moon shadows

The Moon ("Directional light Moon", intensity 0.2, soft shadows) is the house's only directional light and the only light outside besides lamps. Its shadow map is rendered for the whole scene, and it reaches nothing indoors, so for most of the game it costs frame time and shows nothing. The `MoonShadows` feature has a setting (Mod Settings, Performance Fixes, **Moon shadows**): On (the game's own), Only outdoors (default) or Off.

- **Only outdoors**: while the local player is in the front or back yard (`Location.FrontYard`/`Backyard`, the same list as `Commons.ExteriorLocations`; the shed counts as indoors) the moon keeps its shadows. Anywhere else the light is switched off. A shadowless moon can't just stay on: with shadows off it shines through walls (the living room's side wall and floor lit up blue-grey in a screenshot). Indoors the light contributes nothing with shadows on, so switching it off looks the same.
- **Off**: no moon shadows in the yards either, the moon stays on there; indoors it is off like above.
- **Lightning**: the game pushes the moon's intensity to 14-22 for a flash. During that (intensity above 1) the moon is on with its shadows with every setting, because a flash without shadows floods the whole house white (screenshot, living room). The feature reads one float per frame to see it; nothing else polls. The game's thunder leaves the moon at intensity 0 afterwards, which costs nothing either way.
- The setting is local: Performance Fixes is one of the mods that may differ between co-op players, and it only changes the player's own view.

Measured on rig 2 (64-bit player, GE-Proton, 1600x900 windowed, `perf.frames` uncapped, 4 s windows, moon reset to 0.2 before each, a window with a lightning strike discarded; the `Shadows` preset on room lights; the house had five shadow-casting lights and few lamps on). `On` and `Off` alternated 10 times per spot, the median is shown; Only outdoors is by construction the same as Off indoors and the same as On in the yards (checked in a 5-round run: 770 vs 784 draw calls indoors, 1,386 vs 1,386 in the yard).

| Spot | Moon shadows | Median frame | Draw calls | Shadow casters |
| --- | --- | --- | --- | --- |
| Living room (4.09, 0.885, -11.6, facing the TV) | On | 10.4 ms | 879 | 448 |
| | Off / only outdoors | 9.9 ms (-5%) | 755 (-14%) | 305 (-32%) |
| Backyard (17.44, 0.85, -6.5, facing -Z) | On / only outdoors | 12.2 ms | 1,402 | 912 |
| | Off | 11.4 ms (-7%) | 830 (-41%) | 165 (-82%) |

Three other game instances (other test rigs) ran on the same PC during all of this (load average 20 to 27), so the frame times are noisy and the gain is probably bigger on a quiet PC; draw calls and casters don't depend on load and are the numbers to trust. They match the earlier measurement in the weather notes (12 to 15% more draw calls with the moon's shadows in a busier house).

What players lose, from screenshots with the game paused (`game.timescale 0`) so On and Off show the same frame:

- Indoors, **Only outdoors** is the same picture as On except for moonlight coming through an open door or window (the entrance floor with the front door open, a few levels of brightness at most).
- In the yards, **Only outdoors** is pixel-identical to On (mean difference 0.1 of 255, the noise floor).
- **Off** in the yards lights places the house would shadow: the back yard behind the house, dark in the original, shows its gravel and rocks. The mean difference to On is only 1 to 2 of 255 (the moon is dim), but boosted it is plainly a different mood, so Off is the opt-in.

## What's fixed

What the Performance Fixes mod (each fix a feature that can be switched off), Unofficial NEET Patches, the loader and the installer do now, with what was measured. Numbers from the living room unless noted; the house in the later measurements was much busier (more lights on, a dozen characters) than in the first ones.

| Fix | Where | Measured |
| --- | --- | --- |
| Save only the paint that changed, encode and write it on a background thread | Performance Fixes: `FastPaintSaves` | 619 painted surfaces: 2,500 ms per save before; 276 ms for the first save with the mod (everything once, 3 s in the background), 52 to 65 ms after that. Pictures pixel-identical to the game's own. At most 64 MB of pixels wait for the writer: a first save that queued all 748 surfaces of a heavily painted house at once ran the 32-bit game out of memory and crashed it; capped, it takes about 2.8 s and about 50 MB. |
| Free the paint textures of old houses, reuse the door and copy textures | Performance Fixes: `TextureLeaks` | Render textures stay flat over house loads instead of +2,480 per load. |
| Peephole camera only near the front door | Performance Fixes: `Peephole` | About 500 draw calls and 0.8 to 1.1 ms a frame. |
| Shadow presets | Performance Fixes: `Shadows` | Five ceiling lights on: 31.0 ms and 15,300 draw calls as shipped, 25.6 ms and 9,400 with room lights (default), 21.0 ms and 5,200 with none. |
| Moon shadows only outdoors (or off) | Performance Fixes: `MoonShadows` | Living room: 879 draw calls and 448 shadow casters with the moon's shadows, 755 and 305 without (median frame 10.4 vs 9.9 ms). Backyard: 1,402 and 912 with, 830 and 165 without (12.2 vs 11.4 ms). Details in [Moon shadows](#moon-shadows). |
| No velocity writes to kinematic bodies, no "restinput", no stack traces for logs and warnings | Performance Fixes: `LogSpam` | 233 warnings in 4 s while kinematic before, none after. |
| Idle paint surfaces skip their update | Performance Fixes: `IdlePaint` | `PaintTarget.Update` (0.31 ms, 1,238 calls a frame) gone. |
| Characters plan their path only when the target moves | Performance Fixes: `PathRequests` | `BrainTrickOrTreater.Update` 172 to 2.4 us a call (0.69 ms a frame with four at the door). |
| Incremental garbage collection | Installer: `--incremental-gc` | Collection spikes gone (see above). |
| The player's frame rate and vsync settings | Unofficial NEET Patches: `FrameRateSettings` | The game forced 120 fps without vsync over the settings menu's choice whenever a camera started. |
| Clean exit on 32-bit | Loader | No crash report on exit. |
| 64-bit player | Installer (default; `--32bit` goes back) | About 5% faster. |

Saving also no longer fails for good when one object can't be saved (`SaveCrashes`) or a destroyed one is still subscribed (`GhostSaves`), both in Unofficial NEET Patches.

### Still open

- **Allocations**: about 11 KB a frame in a busy house (`perf.frames`), mostly from the character rigs' `Player_Movement.Update` (3.1 KB), `Player_Abilities.Update` (2.2 KB) and `BrainNeetAI.Update` (1.5 KB) by `perf.scripts allocations`, with no single allocation in those methods themselves; it comes from what they call. `CameraControl.Update`'s `GetComponentsInChildren` calls are only 0.14 KB, not worth a patch. With incremental GC on, this matters much less.
- **Collecting at natural pauses** (sleeping, loading, the pause menu) would only move collections around; not done.
- **"Coroutine continue failure"** shows up once per session in the house; the cause isn't known yet.

Measure with `perf.frames`, `perf.save`, `perf.gc`, `perf.scripts` and `textures.count` before and after.
