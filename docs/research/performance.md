# Performance

Why NEET Demo feels laggy, measured in the running game, and what a Performance Fixes mod can do about it. Also: can it run as 64-bit?

## Summary

- **The steady frame rate is not the problem on a fast PC.** In the living room the game runs at 125 to 155 fps uncapped (6.4 to 8 ms per frame) on an i9-9900K with an RX 7900 XT under GE-Proton. The game caps itself at 120 fps.
- **The lag is hitches.** Every save freezes the game for 150 to 400 ms, and longer the longer you play. Every garbage collection freezes it for 9 to 17 ms, about every 8 seconds even when idle. Broken game states throw an exception every frame and log it with a stack trace (85,000 times in one session).
- **The frame's biggest costs are rendering.** Shadows from point lights account for about 1,600 of the 2,500 draw calls, and the front door's peephole camera renders the whole house again every frame. Script code is only about 1.2 ms per frame.
- **Some costs grow with play time.** Status effects leak per-frame handlers, paint saves get slower, render textures leak.
- **32-bit is not the bottleneck.** The game uses about half a gigabyte of the 4 GB it can address. A 64-bit build works, with all mods, and is about 5% faster, sees all the VRAM and doesn't crash on exit. It doesn't fix the hitches.

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

The game was built without incremental GC (`GarbageCollector.isIncremental` is false, and it can't be switched on at runtime), so each collection stops the game in one go: 9 ms with a 44 MB Mono heap, 12 to 17 ms with 60 MB. At 120 fps that's one or two dropped frames, about every 8 seconds when idle and more often when much is going on. Less garbage per frame means fewer collections.

### Errors and log spam

Unity writes every log line, and for errors and exceptions a stack trace, to `Player.log`. Exceptions in Mono are expensive on their own.

- **The thief intro loop:** after Grandpa dies, `SeqSpawnThiefIntro` throws a `NullReferenceException` every frame, forever, repeating its effects. 85,499 of them in one session (`Player.log` of 42 MB). Fixed by Unofficial NEET Patches (`LostCharacters`, `StuckSequences`).
- **"Setting linear velocity of a kinematic body is not supported."** 64,811 times in one session, none in a fresh one. Unity warns each time code sets the velocity of a kinematic rigidbody. Candidates from the code: `Player_Movement.FixedUpdate` writes `rigid.velocity` every physics step while standing still (`Player_Movement.cs:407-421`), and the player's body is kinematic when dead or skeletonized (`Player_Abilities.cs:1882`, `:1891`) or hanging (`HangNoose.cs:69`); `NPCNavigationTest.FixedUpdate` (`:33-34`); hand grabs (`Player_Abilities.cs:800`, `:1074-1089`).
- **`print("restinput")`** in `Player_Movement.cs:286`: about 1,200 lines a session.
- **The house spider save crash** (`InvalidCastException` in `Brain.SaveBasicBrain`) aborted saves and death. Fixed by Unofficial NEET Patches (`SpiderSaves`).

## Things that grow over play time

From the code; the first is confirmed by reading it, the rest are worth confirming with the tools before fixing.

- **Status effect handlers leak** (confirmed in code). `StatusHandler.AddEffect` subscribes every amplifier effect's `OnTick` to the stat's `OnChanged` (`StatusHandler.cs:249-255`), and `RemoveEffect` never unsubscribes it (`:292-296`). `StatusHandler.Update` calls `SetVal` on sticky stats every frame, which fires `OnChanged`, so every amplifier effect ever applied keeps running every frame, and keeps amplifying after it ended. A gameplay bug as well as a cost.
- **Paint saves** get slower and leak a `Texture2D` per painted surface per save (above), and `PaintTarget.ClearWaterChannel` leaks one per wet surface every 120 seconds (`HouseManager.cs:407-411`).
- **Door peephole render textures**: `Door.OnTriggerEnter` creates a new 256x256 `RenderTexture` each time the player walks into the trigger and never releases it (`Door.cs:413-414`).
- **Room fires stay subscribed to saving** after they're put out (`FireSpreader.cs:104`, no unsubscribe in `Clear`).
- **`Spawner.concurrentSpawnIds`** only grows and is written into every save (`Spawner.cs:236-241`, `:112`).
- **`Locator.sitRestEnablers`** only grows (`Locator.cs:52-53`); memory only.

## Crash on quitting

Every session of the 32-bit game ends in a crash report: the game crashes inside the Mono runtime (`mono-2.0-bdwgc.dll`) while shutting down, right after the mods' quit handlers ran. Saves are written before that, so nothing is lost, but each exit leaves a crash dump in `AppData/Local/Temp/NEET Games/NEET/Crashes`. The 64-bit build quit without a crash.

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
| Crash on quitting | yes | no |

About 5% faster, the same hitches. The 32-bit game is far from its 4 GB limit (its exe is already Large Address Aware), so 64-bit only becomes important if a mod loads a lot more content.

### If we offer it

Unity's player binaries can't be shipped with a mod (Unity's licence covers shipping them with your own game), so the tool would download Unity's module from Unity at the player's request and build the swap in place, keeping the 32-bit files to switch back. Steam's "verify files" restores the 32-bit files. Worth it mainly for the clean exit and the extra VRAM; an opt-in `neet install --64bit` at most.

## What the Performance Fixes mod should do

Ranked by how much it helps, with what was measured.

1. **Peephole camera only while looking through it.** Turn the camera off unless the player is at the door's peephole, or render it a few times a second. About 1.1 ms a frame (17%) and 500 draw calls here, on every frame of the game.
2. **Fewer shadowed lights.** Drop shadows from small point lights (fridge lights, microwave, desk lamp, corpse lights), or offer presets (all shadows, main lights only, none). Shadows are 1.25 ms and 1,600 draw calls here; more in rooms with more of them.
3. **Save only the paint that changed.** Track which surfaces were painted since the last save, keep the other PNGs, reuse one `Texture2D`. Takes most saves from 150 to 400 ms down to the 20 ms of the game data.
4. **Stop the log spam.** Skip velocity writes to kinematic bodies (prefix on `Rigidbody.set_velocity` and `set_angularVelocity`), drop `print("restinput")`, and optionally turn off stack traces for warnings and logs (`Application.SetStackTraceLogType`).
5. **Fewer garbage collections.** Cut per-frame allocations (`CameraControl.Update`'s two `GetComponentsInChildren` calls, `PaintTarget.PaintSplats`' arrays, LINQ in NPC brains), and collect at natural pauses (sleeping, loading) so the collections fall there.
6. **Fix the leaks**: status effect handlers (in Unofficial NEET Patches, since it's also a gameplay bug), peephole render textures, paint textures, fire save subscriptions.
7. **Frame rate settings**: respect a frame cap and vsync of the player's choice instead of the 120 fps the game forces on every camera start.
8. **Cheaper idle work**: `PaintTarget.Update` on 1,236 surfaces that have nothing to do.
9. **Quit cleanly** on 32-bit: end the process after the game and mods are done saving, so exits stop crashing.
10. **64-bit mode** in the tool, as above, opt-in.

Measure each with `perf.frames`, `perf.save` and `perf.gc` before and after.
