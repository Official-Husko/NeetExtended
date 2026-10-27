# Weather Expansion

The Weather Expansion mod (`mods/WeatherExpansion`, id `WeatherExpansion`) changes how the storm outside looks. The house is one rainy night with thunder; this mod adds looks to it. Each look is a `ModFeature` marked experimental, so it can be switched off on its own on the Mods page, and each one undoes itself when it's switched off, when the mod unloads (hot reload) and when the house scene goes away.

It only uses what the game already ships: no custom shader and no extra download. What that rules out is under [Known limitations](#known-limitations).

## What's local in co-op

All of it. Every game draws its own looks from its own scene: nothing is sent over the network and no setting is `[Shared]`. The looks that follow lightning read the moon light's intensity (the game's own flash) in the local game, so they light up whenever that game's moon does; whether a client's moon flashes along with the host's is up to the co-op world sync, not this mod. The storm's state (drizzle, normal, heavy) is not part of this mod; when the framework's Sky API has one, it plugs into `Storm/StormInputs.cs` and every look follows it.

## Looks and settings

Switches are on the Mods page, values in Mod Settings under "Weather Expansion" (`Mods/WeatherExpansion/config.json`). All of them apply while the game runs.

| Look (feature id) | What it does | Settings (default, range) |
| --- | --- | --- |
| Stormy fog (`StormFog`) | The fog is a dark blue-grey instead of black, as thick as the setting says, and slowly thickens and thins. | Fog thickness (1.3, 0.5 to 3; 1 is the game's own), Fog brightness (1, 0 to 2) |
| Lightning glow (`LightningGlow`) | In a flash the fog turns bright blue-white and the sky's tint comes up, so the night landscape in the sky cubemap shows behind the yard. | Lightning glow (1, 0 to 2) |
| Mist in the yards (`MistBanks`) | Two banks of mist drift over the front and the back yard, tinted like the fog and brighter in a flash. | Mist (1, 0 to 2) |
| Rain on the windows (`RainOnGlass`) | Drops sit on every window and some run down it, bending what you see through the glass. A broken pane loses its drops. | Rain on the windows (0.5, 0 to 1), Window haze (0.15, 0 to 0.4) |

## How the fog looks work

`Fog/FogLook.cs` owns the scene's `RenderSettings.fogColor`, `fogDensity` and the sky material's `_Tint` while either fog look is on. Two features want those values, so one class applies them, once per frame from `WeatherExpansionMod.OnUpdate` (0.4 microseconds, no allocation). The game's own values are captured when the house is ready and put back when the last fog look goes off, when the mod unloads and when the house scene unloads (the sky material is a shared asset and would otherwise keep the flash tint into the main menu).

- **Stormy fog**: colour `(0.035, 0.05, 0.085)` times the brightness setting, density = the game's density (0.06, ExponentialSquared) times the thickness setting times `StormInputs.FogDensity` times a drift of about plus or minus 16% (two slow sine waves, a minute or so).
- **Lightning glow**: `Fog/FlashLevel.cs` turns the moon light's intensity into a 0 to 1 flash (`HouseManager.Thunder` pushes the moon from 0.2 to 14-22 and back; 14 counts as full). The fog colour is lerped to `(0.40, 0.47, 0.66)` and the sky `_Tint` from the game's 0.038 grey to `(0.50, 0.58, 0.78)` by that amount times the setting. Following the light itself needs no event, so it survives house loads, story strikes (`Thunder(instant: true)`) and hot reloads.
- The sky is `Skybox_CubeTest`, a `Skybox/Cubemap` of a moonlit golf course (`moonlit_golf_4k`) darkened to 0.038, which is why the sky is black. Brought up, it shows hills, trees and a few lights on the horizon, which is what lights up behind the yard in a flash.

## How the mist works

`Fog/MistBanksFeature.cs` makes one particle system per yard (`Fog/MistSystem.cs`), centred on the game's own `Particle_Rain_Frontyard` and `Particle_Rain_Backyard`, 2.5 m up so 4 to 7 m wide puffs stay off the ground. Each system holds at most 70 puffs (rate 3.5 a second times the setting, 18 s life), drifting with a fixed wind and fading in and out. They share one material: a copy of the game's `Legacy Shaders/Particles/Alpha Blended` shader and a soft round texture the mod draws once. The tint is set once per frame (night colour to flash colour by the same flash level); the game's fog fades far puffs out.

## How rain on the windows works

A window is two invisible paintable panes (`Wall_Window_*_Pane_1` and `_Pane_2`, both with `BreakOnImpact`) in a wall, with a blue `WindowHighlight` quad between them; the panes are what the game's paint and breaking use. The mod puts one quad 1 cm inside the inner pane of every window (`Windows/WindowFinder.cs`, `WindowPane.cs`; which side is inside comes from the room trigger boxes in `InteriorRooms.cs`, because the pane names don't say for every window). The quad is drawn with `Windows/GlassMaterial.cs`: a copy of the game's own refracting glass, `Glass/DiffuseBumped` (the ferris wheel's windows; it reads a normal map and bends what is behind it by `_IOR`). The normal map is what makes the drops.

`Windows/DropField.cs` simulates the water on the CPU into one 256 by 144 normal map that every window shares:

- about 100 small drops sit still, grow while it's wet and dry up slowly; one that gets big enough starts to run
- up to 12 running drops slide down in stop and go, wobbling a little, leaving a thin wet trail, getting smaller; one that runs out of water stays behind as a bead
- each tick (15 a second) puts the previous tick's pixels back to flat, redraws the trails from their slope and stamps each drop's rounded dome. Only touched pixels are written, into a managed array uploaded with `SetPixelData`, so nothing allocates and a tick costs about 0.5 ms
- the field only advances while one of the quads is on screen (`Renderer.isVisible`), so it costs nothing in the rest of the house

Wetness (the setting times `StormInputs.Wetness`) sets how fast new drops land and grow. Window haze is the glass shader's `_Opacity` with a blue-grey tint: a thin film that makes the drops show in the dark. Once a second the quads check their panes and hide when either is broken or gone; the window parent owns them, so a window that is destroyed takes its quad along.

## The storm seam

`Storm/StormInputs.cs` holds two multipliers that are 1 until something sets them: `FogDensity` (the fog look) and `Wetness` (window rain and mist). The framework's Sky API plugs in here: set them from the storm state (drizzle low, heavy high) and every look follows. Nothing in the mod reads a storm state any other way.

## Files

| File | What it does |
| --- | --- |
| `WeatherExpansionMod.cs` | Registers the features and the settings, runs `FogLook.Tick` every frame. |
| `WeatherExpansionConfig.cs` | The settings. |
| `Storm/StormInputs.cs` | The multipliers the Sky API will drive. |
| `Fog/FogLook.cs`, `FlashLevel.cs` | Applying fog and sky tint, and following the lightning. |
| `Fog/StormFogFeature.cs`, `LightningGlowFeature.cs` | The two fog looks. |
| `Fog/MistBanksFeature.cs`, `MistSystem.cs`, `SoftTexture.cs` | The mist banks, their particle systems and the puff picture. |
| `Windows/RainOnGlassFeature.cs` | Builds the quads when the house is ready, drives the field, tidies up. |
| `Windows/WindowFinder.cs`, `WindowPane.cs`, `InteriorRooms.cs` | Finding the panes, their size and which way is into the house. |
| `Windows/GlassMaterial.cs`, `QuadMesh.cs`, `DropField.cs` | The material, the quad and the water simulation. |

## Testing

The settings and switches work live, so most checks are `neet call settings.set` and `neet call features.set`. The storm seam can be set from the bridge:

```sh
neet call type.set type=WeatherExpansion.Storm.StormInputs member=FogDensity value=0.5   # drizzle fog
neet call type.set type=WeatherExpansion.Storm.StormInputs member=Wetness value=1.5      # soaked
neet call obj.set target='Directional light Moon' component=Light member=intensity value=14   # hold a flash
neet call obj.set target='Directional light Moon' component=Light member=intensity value=0.2  # and end it
```

A real strike is `neet call obj.call target='House_Manager---------------' component=SequenceManager method=TriggerSequence 'args=["Execute","Thunder"]'`; the first strike from a sequence ends the game's own random thunder for the session, see Weather & sky in `docs/game/world-environment.md`. Dark scenes need a brightened copy of a screenshot to judge; the game really is that dark.

## Known limitations

- Nothing here is saved with the game, and none of it follows the storm: fog, mist and glass look the same all night. Drizzle, heavy rain and a clear spell come with the Sky API.
- There is no height fog and no fog that sits in the sky: Unity's fog is one global colour and density, and a fog volume needs a shader the game doesn't ship. Mist banks stand in for it, over the two yards only.
- Mist puffs are flat billboards without soft edges, so one that drifts into a wall or the ground shows a hard line there.
- Window rain is drawn on the inside of the glass and only seen from inside; from the yard the windows look as they did. Some windows face a wall close outside, and that wall is what shows through their drops.
- The drops are a 256 by 144 picture, so close up they are chunky, and all windows share one picture, so two windows in view show the same pattern.
- The glass needs the game's `Glass/DiffuseBumped` material; if a game update renames it, the look logs a warning and stays off.
- Each pane in view adds a draw and a screen grab (the shader bends the picture behind it); in the house that is one or two panes at a time.
- A custom shader (drops and trails drawn on the graphics card, height fog, wet surfaces) would need an asset bundle built in the Unity editor, which needs the editor installed and an account to activate it. None of that is in the repo.
