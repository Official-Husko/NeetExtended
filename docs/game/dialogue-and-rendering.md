# Dialogue (Ink), paint/decal system, text effects, and rendering helpers

Scope: `decompiled/Assembly-CSharp/` (Dialogue, ThoughtLoader, Text*, Vertex*, Paint*, and the rendering helpers listed below), `decompiled/Assembly-CSharp/AdvancedGizmo/`, `decompiled/Assembly-CSharp/WorldSpaceTransitions/`, and `decompiled/Assembly-CSharp-firstpass/` (DynamicBone, Kino Fog).

Paths are relative to the project root. Line numbers refer to the decompiled files as they currently exist.

## 1. Dialogue system (Ink)

### 1.1 Runtime library

The game uses the Ink runtime from `Ink-Libraries.dll` (namespace `Ink.Runtime`). The DLL also contains the Ink compiler (`Ink.Compiler`, `Ink.Parsed`, `Ink.InkParser`), so the game ships the full toolchain, not only the runtime. Compiled story TextAssets start with `{"inkVersion":20,...}`. Ink format version 20 was confirmed from the TextAsset bytes in `NEET_Data/sharedassets0.assets` and `sharedassets2.assets`.

Three Ink hosts exist, each with its own `Story` instance:

| Host | Field | Story source | Created at |
|---|---|---|---|
| `Dialogue` (main conversation system) | `public TextAsset masterScript` (`Dialogue.cs:54`), `public Story masterStory` (`Dialogue.cs:56`) | `new Story(masterScript.text)` in `Awake` (`Dialogue.cs:144`) | `Dialogue.cs:135` |
| `ThoughtLoader` (stat thoughts) | `public TextAsset masterThoughts` (`ThoughtLoader.cs:9`), `public Story masterStory` (`ThoughtLoader.cs:7`) | `new Story(masterThoughts.text)` in `Start` (`ThoughtLoader.cs:41`) | `ThoughtLoader.cs:41` |
| `EmailHandler` | `public TextAsset masterEmails` (`EmailHandler.cs:34`), `public Story masterStory` (`EmailHandler.cs:32`) | `new Story(masterEmails.text)` (`EmailHandler.cs:117`) | `EmailHandler.cs:117` |
| `EncounterSpawner` | `public TextAsset masterScript` (`EncounterSpawner.cs:94`), `public Story masterStory` (`EncounterSpawner.cs:96`) | `new Story(masterScript.text)` (`EncounterSpawner.cs:156`) | `EncounterSpawner.cs:156` |

The TextAsset names found in the data files (by scanning the serialized asset bytes, not by reading the scene or prefab references) are `Thoughts` (sharedassets0), `Master` (sharedassets0, probably the `Dialogue.masterScript`), and `Master_Email` (sharedassets2). The exact asset-to-field mapping (for `Dialogue`, `EncounterSpawner`) is not verified from code. The fields are serialized, so they are set in the Unity scene or prefab, which is not in the decompiled code.

Story state is only shared inside one host. `Dialogue.masterStory` is the only story that is saved through `SaveLoadHandler`. The others are not in the save file.

### 1.2 Loading and flow selection

- `Dialogue` is a singleton: `public static Dialogue _` (`Dialogue.cs:48`), set in `Awake` (`Dialogue.cs:135-145`).
- `Start` (`Dialogue.cs:147`) initializes UI state (`storyText = GetComponentInChildren<TextMeshProUGUI>()`, `storyText.enabled = false`, `aliveFlowNames`, `pausedFlowOnChoiceAnswer`) and then binds the external functions (see 1.5). Bindings are made in `Start`, not in `Awake`, so any code that binds external functions to `Dialogue._.masterStory` must run after `Dialogue.Start` (see the `SequenceManager` and `QueenOfDust` bindings, section 1.5).
- Ink flows: `SwitchFlow(string flow)` (`Dialogue.cs:383`) fires `OnSwitchFlow` (cleanup of the text box) and calls `masterStory.SwitchFlow(flow)`. `CloseDialogue` (`Dialogue.cs:556-595`) calls `masterStory.SwitchToDefaultFlow()` and remembers paused flows in `pausedFlowOnChoiceAnswer`.
- Entry points into a conversation:
  - `InitCharacterDialogue(CharacterStories speaker, string flow, string charKnot, bool enableCanvasOnly = false, bool forceChoosePath = false)` (`Dialogue.cs:397`). It switches to the flow, then calls `ChoosePathString(charKnot, true)` (lines 410 and 433). If the flow has already been started (`aliveFlowNames`), it only switches flow and skips the first `ChoosePathString`.
  - Callers: `CharacterStories.cs:138` and `:142` (`characterName` as flow, knot `characterName + ("Night" + EncounterSpawner.nightId)? + "." + overrideKnot`), `CharacterStories.cs:212` and `:217` (flows `<name>_Harm` and `<name>_Disgust`, knots `<name>Harm` and `<name>Disgust`, with `enableCanvasOnly` and `forceChoosePath`), `Player_Abilities.cs:847`, and `Phone_Interface.cs:163` and `:212` (flow `"Phone"`, knots `Phone.p<number>` and `Phone.pPrank<id>`).
  - `Usable.cs:780-781` runs `masterStory.SwitchFlow("Usable")` and `ChoosePathString(fullKnotPath, true)` directly.
  - Offering results: `ProcessOffering` jumps to `currKnot + ".got_something"` (`Dialogue.cs:665`), `DeniedOffering` to `currKnot + ".got_nothing"` (`Dialogue.cs:676`).
- The conversation pump is `Update` (`Dialogue.cs:512`): while the speaker is in range and the Use intent starts, it calls `AdvanceStory(isChoice:false)` if `masterStory.canContinue`, otherwise `UpdateChoices()`. Pressing Use while no conversation is open calls `CloseDialogue()`.
- Choices: `UpdateChoices` (`Dialogue.cs:487`) enables and labels the `optionButtons` for `masterStory.currentChoices`. `MakeChoice(int)` (`Dialogue.cs:534`) calls `masterStory.ChooseChoiceIndex`, hides the buttons, and calls `AdvanceStory(isChoice:true)`.

### 1.3 Displaying lines

`AdvanceStory(bool isChoice, bool doNotContinue = false)` (`Dialogue.cs:685-800`):
1. If `currSpeaker.playerRef` is set, calls `CheckPlayerHeldItems()` and `CheckPlayerBody()` (to push inventory state into Ink variables, section 1.4).
2. Calls `storyText.SetText(masterStory.Continue())` (or `currentText` when `doNotContinue`), which is the displayed line.
3. Processes `masterStory.currentTags` (section 1.6).
4. Picks an `AudioSource` for voice (`currentAudio`), starts `TextAnimations.AnimateVertexColors(currentTextAnim)` on the text object, and starts `PlayVoiceAudio()` (`Dialogue.cs:801`).

`lastLineChoice` records whether the last thing shown was a choice answer; `CloseDialogue` uses it for `pausedFlowOnChoiceAnswer`.

### 1.4 Ink variables observed

Read and written by C# (grep across `decompiled/Assembly-CSharp`):

| Variable | Where | Notes |
|---|---|---|
| `playerHeld` (InkList) | `Dialogue.cs:609`, `:617` (set in `CheckPlayerHeldItems`, `:601`) | Built by `Offering.OfferingStatsToInkList`. |
| `playerBody` (InkList) | `Dialogue.cs:647` (`CheckPlayerBody`, `:622`) | Built from `new InkList("PlayerStates", masterStory)`. |
| `tempCurrOffering` (InkList) | `Dialogue.cs:655` (`ProcessOffering`) | |
| `offeringValue` (number) | `Dialogue.cs:658` | Set from `Usable.price`. |
| `deathRespawns` (int) | `HouseManager.cs:466` (read and written) | Incremented on death. |
| `OfferingStates` (InkList, default name) | `Offering.cs:48-50` | Used as the default InkList variable when converting an offering. |

Other Ink list and variable names in the story itself are not visible from the decompiled code.

### 1.5 External functions bound into Ink

Bound with `masterStory.BindExternalFunction(name, delegate)`:

**`Dialogue.Start` (`Dialogue.cs:155-236`), 21 functions:**

| Ink name | C# delegate | Calls |
|---|---|---|
| `GiveItem` | `(string itmName)` | `GiveItem(string)` (`Dialogue.cs:276`) |
| `AddMoney` | `(float amt)` | `HouseManager._.mainComputer.Money += amt` |
| `SetMoney` | `(float amt)` | body at `Dialogue.cs:160` |
| `TradeSoulForClone` | `()` | `TradeSoulForClone()` (`:283`) |
| `KillPlayer` | `()` | `KillPlayer()` (`:301`) |
| `KillSpeaker` | `()` | `KillSpeaker()` (`:316`) |
| `SkeletonizePlayer` | `()` | `SkeletonizePlayer()` (`:306`) |
| `KnockoutPlayer` | `(float l)` | `KnockoutPlayer(float)` (`:311`) |
| `SkeletonizeSpeaker` | `(bool noSkeleton)` | `SkeletonizeSpeaker(bool)` (`:321`) |
| `DestroySpeaker` | `(float delay)` | `DestroySpeaker(float)` (`:326`) |
| `DisableLootSpeaker` | `()` | `DisableLootSpeaker()` (`:331`) |
| `SceneSwitchKnockout` | `(string sceneName)` | `SceneSwitchKnockout(string)` (`:248`) |
| `SceneSwitchInstant` | `(string sceneName)` | `SceneSwitchInstant(string)` (`:243`) |
| `GiveChange` | `(float offeringVal, float priceToDeduct)` | `GiveChange(float, float)` (`:253`) |
| `DestroyReceivedOffering` | `()` | `DestroyReceivedOffering()` (`:258`) |
| `RestoreReceivedOffering` | `()` | `RestoreReceivedOffering()` (`:263`) |
| `ExpelAllFuses` | `()` | `ExpelAllFuses()` (`:296`) |
| `SwitchSegmentLine` | `(string characterName, string segmentLineName)` | `SwitchSegmentLine(string, string)` (`:346`) |
| `SetOverrideKnot` | `(string characterName, string newKnotName)` | `SetOverrideKnot(string, string)` (`:366`) |
| `AddPlayerSceneSwitchOnDeath` | `(string scene)` | `AddPlayerSceneSwitchOnDeath(string)` (`:371`) |
| `EncounterIncrement` | `()` | `EncounterIncrement()` (`:361`) |

**Bound from other classes:**

- `SequenceManager.cs:110-115`: `TriggerSequence`, `Action<string, string>`, calls `TriggerSequence(func, arg)`. Bound in `SequenceManager.Start`, which relies on `Dialogue._` already existing and having run `Start`.
- `QueenOfDust.cs:31`: `AbsorbDusts`, `(Action)AbsorbDusts`, lookahead-safe `false`. Bound in the `QueenOfDust` start code.

Total: 23 external functions. Ink calls them with the usual Ink external-function syntax, and the C# delegates receive the typed arguments.

Note: the `Dialogue` bindings are one-time. Re-creating `masterStory` (for example in a mod) means re-binding all of them.

### 1.6 Tags

Tags are read from `masterStory.currentTags` in `AdvanceStory` (`Dialogue.cs:701-795`). Matched by prefix (`Substring` checks):

| Tag | Effect |
|---|---|
| `TextAnim:<value>` | `currentTextAnim = (TextAnimationType)Enum.Parse(...)`. The only enum value is `Simple` (`Dialogue.cs:14-17`). See the open question about the offset. |
| `SpeakerAudio:<Player or other>` | Chooses the voice AudioSource (`GetComponents<AudioSource>()[0]` for `Player`, otherwise the speaker's AudioSource). |
| `OfferingTime` | Activates the speaker's bucket (`Candy_Bucket`, `bucket.ActivateBucket`) if it is not already active. |
| `DeniedOffering` | Calls `DeniedOffering()` (`Dialogue.cs:671`). |
| `Leaving` | Calls `currSpeaker.Leaving()` and `CloseDialogue()`. |
| `ClearCharacter` | Calls `CloseDialogue()`. |
| `EndPhoneCall` | `Phone_Interface.ClearScreen(noDistanceCheck: false, endPhoneCall: true)`. |
| `NextBrainSegment[n]` | `BrainNeetAI.IncrementSegments(n)` (default n=1), and then one extra `masterStory.Continue()` if possible. |

### 1.7 Saving and loading

- `Dialogue.Save()` (`Dialogue.cs:86-88`) returns `SaveDataDialogue`, with `currSpeakerId` (`"b<brain saveId>"`, `"u<usable saveId>"` or `"x"`), `currKnot`, `aliveFlowNames`, `firstKnotLoad`, `storyTextEnabled`, `currentText`, and `bucketActive`.
- `SaveLoadHandler.cs:101`: `jObject["Save"][0]["mainStory"] = JObject.Parse(Dialogue._.masterStory.state.ToJson())`. The Ink state is the full JSON of `masterStory.state`, which contains the current flow, callstack, and variables.
- `SaveLoadHandler.cs:187`: `Dialogue._.Load(...)` restores the UI state. `Dialogue.Load` (`Dialogue.cs:91`) calls `CloseDialogue()` first.
- `SaveLoadHandler.cs:188`: `Dialogue._.masterStory.state.LoadJson(jObject["Save"][0]["mainStory"].ToString())` restores the Ink state.
- The save file is written to `Application.persistentDataPath` as `neetSave_<DateTime>.json` (`SaveLoadHandler.cs:110-116`).

Only `Dialogue.masterStory` state is saved this way. `ThoughtLoader`, `EmailHandler`, and `EncounterSpawner` never touch their `masterStory.state` in save or load code (their only `masterStory` uses are the construction lines in section 1.1, plus `EncounterSpawner.cs:219` for `VisitCountAtPathString`).

### 1.8 Thoughts (`ThoughtLoader`)

- `ThoughtLoader.cs:5-44`: a separate Ink story (`masterThoughts`, TextAsset `Thoughts`). `LoadThought(int needId)` (`ThoughtLoader.cs:13`) chooses a knot by need and level:
  - `0` bladder: `Toilet_Low`, `Toilet_Neutral`, `Toilet_High`
  - `1` hunger: `Hunger_*`
  - `2` energy: `Sleep_*`
  - `3` hygiene: `Hygiene_*`
  - Thresholds are 35 and 65 on the `PlayerStats` values (`bladder`, `hunger`, `energy`, `hygiene`).
- It calls `ChoosePathString(path, true)`, then `Continue()` once if possible, and returns `currentText` with `^N`/`^n` replaced by newlines.
- Callers: `VertexShakeA.cs:168` (when the text has faded out and the brain HUD is not active, with a 90% chance) and `VertexShakeA.cs:189` (`UpdateThought`). The `textId` field on `VertexShakeA` picks the need.
- The thought text is shown by `VertexShakeA` on the stat objects (`Bladder.cs`, `Energy.cs`, `Soul.cs` construct it with `VertexShakeA`).

### 1.9 How a mod could inject dialogue

Possible approaches, from what the code allows. None of them has been tested.

1. **Add external functions:** Harmony postfix on `Dialogue.Start` to call `Dialogue._.masterStory.BindExternalFunction(...)` for new names. The bindings are not re-run, so this is the safe hook point. Ink calls them the same way (signatures in section 1.5).
2. **Hook tag handling:** Harmony prefix or postfix on `Dialogue.AdvanceStory` that reads `masterStory.currentTags`. Custom tags can be recognized there and acted on. Existing tags (section 1.6) show the pattern.
3. **Hook flow/knot entry:** Harmony postfix on `Dialogue.InitCharacterDialogue` (or the `ChoosePathString` calls). New conversations can be started by calling `InitCharacterDialogue` with a flow and knot that exist in the compiled story.
4. **Use a different compiled story:** `Ink.Runtime.Story` takes JSON text. A mod could load its own compiled story JSON (from a file or an AssetBundle) and create a separate `Story` for mod dialogue. This keeps the base game story unchanged, but then the UI, tags, and save/load logic in `Dialogue` (which all operate on `masterStory`) would need to be duplicated. It is not wired in the existing code.
5. **Compile Ink source at runtime:** `Ink.Compiler` is in the shipped `Ink-Libraries.dll` (`new Compiler(string inkSource, Options options)` and `Story Compile()`, confirmed with `monodis`). A mod could compile `.ink` source to a `Story` at runtime. Same caveat as option 4: the result is a separate story.
6. **Swap the `masterScript` TextAsset:** `Dialogue.Awake` builds the story from `masterScript.text` once, so changing the field afterward has no effect unless `masterStory` is re-created and all bindings in section 1.5 are re-applied. This is more fragile than option 1.

Save compatibility: `mainStory` in the save is the full Ink state JSON. Adding or changing knots or variables in a mod's story changes what `LoadJson` expects. Behavior when a loaded state references missing content has not been checked.

## 2. Paint / decal system

### 2.1 Overview

`PaintTarget` (`PaintTarget.cs:7`) is a MonoBehaviour on each surface that can be painted. It owns:
- `splatTex` and `splatTexAlt` (`RenderTexture`, `ARGB32`, size from the `TextureSize` enum, default `Texture256x256`, `PaintTarget.cs:9`, `:584-586`). The two textures are used alternately with `evenFrame` (`PaintTarget.cs:39`, `:707-731`).
- `worldPosTex` and `worldPosTexTemp` (`ARGBFloat`, `PaintTarget.cs:588-590`), written by the `Hidden/PaintPos` shader (`PaintTarget.cs:579`).
- `paintBlitMaterial` from `Shader.Find("Hidden/PaintBlit")` (`PaintTarget.cs:578`). Shader pass indices used in the code: 0 (splat), 2 (world position), 3 (score tally).
- Each target's renderer gets `_SplatTex` and `_WorldPosTex` set (`PaintTarget.cs:597-598`) and the main texture is set to `splatTex` (`:630`).
- `renderCamera` (`CreateCamera`, `PaintTarget.cs:518`), used for command-buffer rendering (`ClearPaint`, `RenderTextures`).

`PaintTarget.cs:654`: `PaintSplat(Paint)` queues a splat in `m_Splats` and silently drops anything beyond 100 entries (`if (m_Splats.Count < 100)`). `Update` (`PaintTarget.cs:737`) flushes the queue; with `PaintAllSplats` it flushes all of it in the frame.

Static entry points:
- `PaintTarget.PaintObject(PaintTarget, Vector3 point, Vector3 normal, Brush)` (`PaintTarget.cs:325`). Builds a `Paint` (matrix, channel mask, scale/bias, brush) for one splat. Callers: `CollisionPainter.cs:122`, and `ParticlePainter.OnParticleCollision` (`ParticlePainter.cs`).
- `PaintRaycast(Ray, Brush, bool multi = true, float maxDistanceScaling = 0f, bool pierceFurniture = false, bool pierceRoomBoundaries = ...)` (`PaintTarget.cs:246`). Callers: `LiquidBomb.cs:85`, `BrainAgentCop.cs:279`.
- `PaintLine`, `PaintRay`, `PaintCursor` (`PaintTarget.cs:224-239`). `PaintRay` is used by `FootprintPainter.cs:79`.
- `RayChannel(Ray)` and `RayColor(Ray)` (`PaintTarget.cs:105`, `:162`) sample the splat under a ray. `FootprintPainter.CheckStainedFeet` uses `RayChannel` (`FootprintPainter.cs:115-127`).
- Brush/colors: `Brush` (`Brush.cs:5`) holds `splatTexture`, `splatsX/Y` (atlas grid), `splatChannel` (int), scale/rotation randomization, and `getMask()`/`getTile()`. `Paint` (`Paint.cs`) is the data object. `PaintTarget.paintColors` is a static array of 5 colors set in `Awake` (`PaintTarget.cs:769`).

Painters that drive it:
- `CollisionPainter.cs` (`brush`, `RandomChannel`, `maxCollisions`, `decreaseSizePerCollision`, events `OnCollided`/`OnCollidedLong`). It calls `PaintTarget.PaintObject` from collision callbacks.
- `ParticlePainter.cs`: paints from particle collision events.
- `FootprintPainter.cs`: paints footprints (`PaintFootprint`, `PaintFootprint(bool bareFeet, Vector3, Vector3, bool)`), checks for stained feet (`CheckStainedFeet`), and counts steps with `printStepsShoeCount`/`printStepsBareFeetCount`.
- `PaintCamera.cs` and `PaintExample.cs`: debug/demo painters that paint from the mouse or the camera. `PaintExample` has an `OnGUI` menu (`PaintExample.cs:101`), which is the only `OnGUI` in the game code.
- `PaintLightSurface.cs`: subscribes to `OnPainted`, calls `TallyScoreThisTarget` and lerps a `Light`'s color by the per-channel scores (`/14f`).

Scores: `PaintTarget.TallyScoreGlobal()` (`PaintTarget.cs:402`) and `TallyScoreThisTarget()` (`PaintTarget.cs:451`) blit the splat texture to 256 and then 4x4 with shader pass 3, and read the result into `PaintTarget.scores` (`Vector4`, static). `Scores.cs:50-71` turns these into percentages for four players.

### 2.2 Saving and loading splats (PNG per saveId)

Save path, `SaveLoadHandler.cs`:
- `SaveLoadHandler.cs:118` (inside the save routine, after writing the JSON save file): `SavePaintSplats(Application.persistentDataPath)`.
- `SavePaintSplats(string path)` (`SaveLoadHandler.cs:125-141`): ensures `<persistentDataPath>/Textures/PaintTargetSplats` exists, deletes every file currently in that folder, then for each `HouseManager._.paintTargets` entry with `setupComplete`, calls `SaveSplat(folder)`.
- `PaintTarget.SaveSplat(string path)` (`PaintTarget.cs:89-94`): `File.WriteAllBytes(path + "\\" + saveId + ".png", toTexture2D(splatTex).EncodeToPNG())`. `toTexture2D` (`PaintTarget.cs:96`) reads the active `RenderTexture` with `ReadPixels`.
- The file name is the `PaintTarget.saveId` (int field, `PaintTarget.cs:63`).

Load path:
- `SaveLoadHandler.LoadPaintTargets()` (`SaveLoadHandler.cs:443-463`) calls `PaintTarget.ClearAllPaint(clearMemory: true)`, then builds a `saveId -> PaintTarget` map from `HouseManager._.paintTargets`. For each id:
  - If `Application.persistentDataPath + "\\Textures\\PaintTargetSplats\\<id>.png"` exists, it reads the bytes, `new Texture2D(2, 2).LoadImage(data)`, and calls `SetupPaint(tex)` (`PaintTarget.cs:567`).
  - Otherwise it calls `ClearPaint(new Color(0,0,0,0))`.
- `LoadPaintTargets` is called from the load routine (`SaveLoadHandler.cs:182`).

Caveats seen in the code:
- The load path uses hard-coded `\\` separators (`SaveLoadHandler.cs:451`, `:457`). The save path uses `"/"` for the folder and `"\\"` for the file name (`:127` and `PaintTarget.cs:91`). This works on Windows only, which matches the target build (Windows x86).
- Only `splatTex` is saved (`PaintTarget.cs:92`). Rendering alternates between `splatTex` and `splatTexAlt` (see 2.1). Whether the saved image always includes the latest splats has not been verified at runtime (see open questions).

### 2.3 Paint-related helper scripts

- `PaintTarget` is also the place where paint is cleared: `ClearPaint(Color)` (`PaintTarget.cs:638`) renders a clear into both textures using a `CommandBuffer` and the render camera. `ClearAllPaint` (`PaintTarget.cs:377`) does this for all targets (static).
- `TextureSize` enum (`TextureSize.cs`): 32 to 4096 square (`Texture32x32 = 0x20` ... `Texture4096x4096 = 0x1000`).
- `TextureSpace` enum (`TextureSpace.cs`): `texture2D`, `texture3D_Atlas`. Usage not traced.
- `PaintDebug` enum (`PaintDebug.cs`): `none`, `splatTex`, `worldPosTex`, used for the `debugTexture` field on `PaintTarget`.

## 3. Text effects

Three components modify TextMeshPro text vertex data. All are MonoBehaviours that poll `TMP_TextInfo`:

- `TextAnimations.cs` (attached to the dialogue text object; `Dialogue.cs:796` calls `GetComponent<TextAnimations>().AnimateVertexColors(currentTextAnim)`). `AnimateVertexColors(Dialogue.TextAnimationType)` (`TextAnimations.cs:20`): for `Simple` it reveals characters one at a time by setting the alpha of each character's four vertex colors to 255, then `UpdateVertexData(Colors32)` once per visible character, one per frame. Other types exit immediately.
- `TextAnimation_Wave.cs`: sin-wave offset on each character's vertices (`Mathf.Sin(Time.time + i*2)` on Y and Z). Its `Update()` calls `StartCoroutine(AnimateVertexColors())` every frame (`TextAnimation_Wave.cs`, `Update`). That starts a new coroutine each frame, which looks unintended. Whether this component is placed in any scene is not known from code.
- `VertexShakeA.cs` (the stat/thought text, `:5`): applies random per-character jitter (rotation `Random.Range(-0.25, 0.25)` degrees, scale `Random.Range(0.9, 1.1) * dynamicMult`) and fades characters in with `fadeInMultiplier`. It uses the `textId` to fetch a thought from `ThoughtLoader` when the text has faded to alpha 0 (section 1.8). Its `thoughtsScrambled` flag stops repeated refreshes.

`Dialogue.TextAnimationType` only contains `Simple` (`Dialogue.cs:14-17`), so effects are selected by tag (section 1.6) and only `Simple` does anything in `TextAnimations`.

## 4. Other rendering helpers and third-party code

### 4.1 Small helpers (`decompiled/Assembly-CSharp/`)

- `RandomVertColors.cs`: assigns random vertex colors to the mesh at `Start`.
- `LineRendererObjBasedPos.cs`: sets a `LineRenderer`'s positions every frame from a list of GameObjects (world or local space via `relativePos`).
- `RendMatRef.cs`: serializable struct `RendMatRef(Renderer r, int[] matindexes)`. `SwitchMat(Texture)` sets `_MainTex` on the listed material slots; `SwitchMat(Material)` replaces the materials.
- `SkinnedAsTarget.cs`: at `Start`, copies the `bones` array from `targetSkin` (a `SkinnedMeshRenderer`) to this one. Marked `[ExecuteInEditMode]`.
- `GizmoAnim.cs`: on enable, after `delay`, moves `gizmo` along its forward axis with a `Lerp` over `time`, then back.
- `RadialAnim.cs`: on enable, finds `GizmoFollow` and disables it, enables the `FADE_SPHERE` and `CLIP_SPHERE` shader keywords and disables the plane variants, and reads `_Radius`, `_SectionPoint`, `_SectionPlane`, `_SectionPlane2` globals. Animates them over time.
- `NormalDistribution.cs`: static `RandomRangeNormalDistribution(min, max, ConfidenceLevel_e)` using a z-score table and rejection sampling.
- `ThreadMarker.cs`: implements `Usable.IUsable`. Draws a `LineRenderer` between two connected objects (`connectedObj1`, `connectedObj2`), with a snap sound.
- `MaterialTracker.cs`: static singleton `_`; `TrackMaterialsInScene()` counts materials per scene using `FindObjectsOfType<Renderer>()`.
- `RenderingModeSwitch.cs`: a UI `Dropdown` that switches `RenderingPath` values from a `RenderingPath[]` (reloads on scene load).
- `TextureScroll.cs`: scrolls `_MainTex` offsets, optionally with fade and sine-wave modulation.

### 4.2 `AdvancedGizmo/` (`Gizmo.cs`, `Hover.cs`)

- `Gizmo.cs` (`:7`): a transform gizmo with axis colliders (`xAxis`/`yAxis`/`zAxis`, planes, rotate rings and a sphere), with axis-constrained dragging. It finds the `FadingTransition` (`Object.FindObjectOfType<FadingTransition>()`, `Gizmo.cs:105`).
- `Hover.cs` (`:6`): changes a renderer's color to `hovercolor` on mouse enter, and handles mouse down/select with the `EventSystem`.

### 4.3 `WorldSpaceTransitions/` (`FadingTransition`)

- `FadingTransition.cs` (namespace `WorldSpaceTransitions`, `:6`): `Awake` sets `HouseManager.fadingTransitionInstance = this` if it is null (`FadingTransition.cs:60`). `HouseManager.cs:92` declares `public static FadingTransition fadingTransitionInstance`. `HouseManager.cs:10` imports `WorldSpaceTransitions`.
- `Start` sets global shader values used by the transition shader: `_spread` (`transitionSpread`), `_ScreenNoise`, `_NoiseAtlas` and `_atlasSize` (cube root of the atlas width), `_Noise2D`, `_TransitionGradient` (when not using the dynamic texture), and the `WORLDMAP2D` keyword.
- Fields: `surfaceType` (plane/sphere), `fadingCentre` (gizmo/camera), `radius`, `distance`, `downOffset`, `noiseScaleWorld`, `noiseScaleScreen`, `useDynamicTexture`, `useWorldSpace2DTexture`.
- Other files in the folder: `GizmoFollow.cs`, `GradientSelector.cs`, `TextureSelector.cs`, `TransitionGradient.cs`, `FadingTransitionDemo.cs`. Not read in detail.

### 4.4 `Assembly-CSharp-firstpass/` (DynamicBone and Kino)

- DynamicBone (`DynamicBone.cs:5`, plus `DynamicBoneColliderBase.cs`, `DynamicBoneCollider.cs`, `DynamicBonePlaneCollider.cs`, and `DynamicBoneDemo1.cs`). A third-party secondary-motion (jiggle bone) component. Version not determined from the code.
  - Game usage: `DynamicBoneUsable.cs` implements `Player_Abilities.IDropTriggerable` and `IPickupTriggerable`. On drop it sets `DynamicBone.m_Force = Vector3.zero`. `Player_Abilities.cs:1067-1069` sets `m_Force = vector3 * 6E-06f` on the item in the hand.
- Kino `Fog` (`Kino/Fog.cs`, namespace `Kino`, `:9`): an image-effect fog. `OnRenderImage` at `Fog.cs:79`, `_shader` at `:23`, `_material` created from it at `:83`. Attached to a camera as a post effect. Not referenced by other game code (grep). Whether it is enabled in any scene is unknown.
- `UnitySourceGeneratedAssemblyMonoScriptTypes_v1.cs` is generated by Unity and is not game logic.

## Modding notes

- **Hook points for Dialogue:** `Dialogue.Start` (binding point for new external functions), `Dialogue.AdvanceStory` (tags), `Dialogue.InitCharacterDialogue` (conversation entry), `Dialogue.CloseDialogue`, `Dialogue.MakeChoice`. Harmony postfix on `Start` is the safest first step.
- **Ink tooling is present:** `Ink-Libraries.dll` includes `Ink.Compiler`, so a mod can compile `.ink` source at runtime. Keep in mind that a separate `Story` does not share state or UI with `Dialogue`.
- **Save data:** only `Dialogue.masterStory` state is in `mainStory` (`SaveLoadHandler.cs:101`, `:188`). Mod-owned Ink state needs its own save entry, which the save loader does not know about.
- **Tag parsing is fragile:** the `Substring` offsets in `AdvanceStory` are hard-coded. Adding a tag with a prefix that collides with an existing one will match the existing branch first (for example `TextAnim:` and `TextAnimation...`). Test new tags in the same form as existing ones.
- **Paint injection:** call `PaintTarget.PaintRaycast(ray, brush)` or `PaintTarget.PaintObject(target, point, normal, brush)` (both static) to paint from a mod. `PaintTarget.saveId` must be set for splats to be saved and loaded (`SaveLoadHandler.cs:370` loads by `saveId`-keyed maps).
- **Text effects:** `VertexShakeA` and `TextAnimations` are safe to reuse on new `TextMeshProUGUI` objects. Adding a new `TextAnimationType` value requires changing the enum (`Dialogue.cs:14`) and the tag parser.
- **Paths:** `SaveLoadHandler` uses `\\` separators in the splat load path. Do not change these without also changing the save path, or splats will not load on Windows.

## Open questions

1. Which TextAsset is `Dialogue.masterScript`? The scan found `Master` (`sharedassets0.assets`), `Thoughts`, and `Master_Email` (`sharedassets2.assets`), but the field-to-asset mapping is in scene or prefab data that is not in the decompiled code. `EncounterSpawner.masterScript` is also unmapped.
2. `TextAnim:` tag parsing: `AdvanceStory` checks a 9-character prefix (`"TextAnim:"`) but parses `item.Substring(10)` (`Dialogue.cs:706-708`). The value starts at index 9, so the first character is skipped. This only works if the tag is written with a space (`# TextAnim: Simple`). Check how the Ink source writes the tag, and what `Enum.Parse` does when the value is wrong.
3. `SaveSplat` saves only `splatTex`, but splats are written alternately to `splatTex` and `splatTexAlt`. Confirm at runtime whether the PNG is missing the latest splats.
4. Is `TextAnimation_Wave` attached to any scene object? Its per-frame `StartCoroutine` in `Update` is likely unintended.
5. Is `Fog` (Kino) or `DynamicBone` placed in any scene? Only the scripts are present in the code.
6. `FadingTransition.Awake` sets `HouseManager.fadingTransitionInstance` only if it is null. Confirm which object is the first instance in a scene and whether its `Start` depends on `HouseManager._` being ready.
7. The order of `Dialogue.Start` relative to `SequenceManager.Start` and `QueenOfDust` start code is not guaranteed by Unity. Their bindings depend on `Dialogue._.masterStory` existing.
8. What the 23 Ink external functions do in the story itself, and which knots and flows exist in the compiled `Master` story. This needs the compiled JSON, not the C# code.
