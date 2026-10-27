# Weather Expansion

The Weather Expansion mod (`mods/WeatherExpansion`, id `WeatherExpansion`) changes how the storm outside looks. The house is one rainy night with thunder; this mod adds looks to it. Each look is a `ModFeature` marked experimental, so it can be switched off on its own on the Mods page, and each one undoes itself when it's switched off, when the mod unloads (hot reload) and when the house scene goes away.

It only uses what the game already ships: no custom shader and no extra download. What that rules out is under [Known limitations](#known-limitations).

## What's local in co-op

All of it. Every game draws its own looks from its own scene: nothing is sent over the network and no setting is `[Shared]`. The looks that follow lightning read the moon light's intensity (the game's own flash) in the local game, so they light up whenever that game's moon does; whether a client's moon flashes along with the host's is up to the co-op world sync, not this mod. The weather state (`Sky.Weather`: drizzle, rain, storm) is the framework's; this mod only reads it, so the looks follow whatever mod drives it (see [Following the weather](#following-the-weather)).

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

- **Stormy fog**: colour `(0.035, 0.05, 0.085)` times the brightness setting, density: the feature gives the framework one factor, `Sky.FogAmount.Set(feature, thickness setting x weather x drift)`, and the kit applies it to the scene's density (0.06, ExponentialSquared) and puts it back when the feature goes off or the mod unloads. The drift is about plus or minus 16% (two slow sine waves, a minute or so). `FogLook` itself only owns the colour and the sky tint, so nothing fights over the density.
- **Lightning glow**: `Fog/FlashLevel.cs` turns the moon light's intensity into a 0 to 1 flash (`HouseManager.Thunder` pushes the moon from 0.2 to 14-22 and back; 14 counts as full). The fog colour is lerped to `(0.40, 0.47, 0.66)` and the sky `_Tint` from the game's 0.038 grey to `(0.50, 0.58, 0.78)` by that amount times the setting. Following the light itself needs no event, so it survives house loads, story strikes (`Thunder(instant: true)`) and hot reloads.
- The sky is `Skybox_CubeTest`, a `Skybox/Cubemap` of a moonlit golf course (`moonlit_golf_4k`) darkened to 0.038, which is why the sky is black. Brought up, it shows hills, trees and a few lights on the horizon, which is what lights up behind the yard in a flash.

## How the mist works

`Fog/MistBanksFeature.cs` makes one particle system per yard (`Fog/MistSystem.cs`), centred on the game's own `Particle_Rain_Frontyard` and `Particle_Rain_Backyard`, 2.5 m up so 4 to 7 m wide puffs stay off the ground. Each system holds at most 70 puffs (rate 3.5 a second times the setting, 18 s life), drifting with a fixed wind and fading in and out. They share one material: a copy of the game's `Legacy Shaders/Particles/Alpha Blended` shader and a soft round texture the mod draws once. The tint is set once per frame (night colour to flash colour by the same flash level); the game's fog fades far puffs out.

## How rain on the windows works

A window is two invisible paintable panes (`Wall_Window_*_Pane_1` and `_Pane_2`, both with `BreakOnImpact`) in a wall, with a blue `WindowHighlight` quad between them; the panes are what the game's paint and breaking use. The mod puts one quad 1 cm inside the inner pane of every window (`Windows/WindowFinder.cs`, `WindowPane.cs`; which side is inside comes from the room trigger boxes in `InteriorRooms.cs`, because the pane names don't say for every window). The quad is drawn with `Windows/GlassMaterial.cs`: a copy of the game's own refracting glass, `Glass/DiffuseBumped` (the ferris wheel's windows; it reads a normal map and bends what is behind it by `_IOR`). The normal map is what makes the drops.

`Windows/DropSimulation.cs` simulates the water on the CPU and `Windows/DropField.cs` draws it into one 256 by 144 normal map that every window shares:

- up to 420 small drops sit still (most only a pixel or two across, a few bigger), grow while it's wet and dry up slowly; one that gets big enough starts to run
- up to 12 running drops slide down in stop and go, wobbling a little, leaving a thin wet trail, getting smaller; one that runs out of water stays behind as a bead
- each tick (15 a second) puts the previous tick's pixels back to flat, redraws the trails from their slope and stamps every drop. Only touched pixels are written, into a managed array uploaded with `SetPixelData`, so nothing allocates and a tick costs about 1 ms
- every drop is stamped in the game's own drop shape (`Windows/DropShape.cs`): the game keeps a teardrop picture, `WaterDropParticle`, for its drip effect (`DripEffect`, material `Drop`). The mod reads its alpha once through a render texture (the game's copy isn't script-readable), blurs the silhouette into a rounded height, turns that into a table of normals and rotates it so the point trails upward. Without that texture the field draws plain soft round drops
- the field only advances while one of the quads is on screen (`Renderer.isVisible`), so it costs nothing in the rest of the house

Wetness (the setting times the weather's wetness, see below) sets how fast new drops land and grow; the weather's speed sets how fast running drops slide. Window haze is the glass shader's `_Opacity` with a blue-grey tint: a thin film that makes the drops show in the dark. Once a second the quads check their panes and hide when either is broken or gone; the window parent owns them, so a window that is destroyed takes its quad along.

## Following the weather

`Storm/WeatherFollow.cs` reads `Sky.Weather` once per frame (`Kind` and `Intensity`) and turns it into three numbers that the looks multiply in, each moving toward its new value over a few seconds (time constant 3 s) when the weather changes, so a storm rolls in instead of switching on:

| Weather | Fog thickness and mist | Window wetness | Running-drop speed |
| --- | --- | --- | --- |
| Drizzle | 0.7 | 0.45 | 0.7 |
| Rain (the default) | 1 | 1 | 1 |
| Storm | 1.35 | 1.6 | 1.5 |

Intensity 1 is the kind's usual strength; wetness is also multiplied by it, and fog and speed by `0.8 + 0.2 x intensity`. Nothing else in the mod reads the weather. The mod doesn't set the weather, rain amount, storm volume or thunder timer; whatever drives `Sky.Weather` does.

## Files

| File | What it does |
| --- | --- |
| `WeatherExpansionMod.cs` | Registers the features and the settings, runs `FogLook.Tick` every frame. |
| `WeatherExpansionConfig.cs` | The settings. |
| `Storm/WeatherFollow.cs` | Turns `Sky.Weather` into smoothed multipliers for fog, mist, wetness and drop speed. |
| `Fog/FogLook.cs`, `FlashLevel.cs` | Applying fog and sky tint, and following the lightning. |
| `Fog/StormFogFeature.cs`, `LightningGlowFeature.cs` | The two fog looks. |
| `Fog/MistBanksFeature.cs`, `MistSystem.cs`, `SoftTexture.cs` | The mist banks, their particle systems and the puff picture. |
| `Windows/RainOnGlassFeature.cs` | Builds the quads when the house is ready, drives the field, tidies up. |
| `Windows/WindowFinder.cs`, `WindowPane.cs`, `InteriorRooms.cs` | Finding the panes, their size and which way is into the house. |
| `Windows/GlassMaterial.cs`, `QuadMesh.cs` | The material and the quad. |
| `Windows/DropSimulation.cs`, `DropField.cs` | The water as numbers (drops, trails), and drawing it as the normal map. |
| `Windows/DropShape.cs` | The game's own drop picture turned into a table of normals. |

## Testing

The settings and switches work live, so most checks are `neet call settings.set` and `neet call features.set`. The weather comes from the bridge's `sky.set`:

```sh
neet call sky.set weather=storm          # also drizzle, rain; intensity=1.5 for heavier
neet call sky.get                        # fog density, the factor and the weather
neet call sky.set moon=14                # hold a flash
neet call sky.set moon=0.2               # and end it
neet call sky.set reset=true             # back to the default rain
```

A real strike is `neet call sky.strike`. Dark scenes need a brightened copy of a screenshot to judge; the game really is that dark.

## Known limitations

- Nothing here is saved with the game. The looks follow `Sky.Weather` but only the three kinds (drizzle, rain, storm); there is no clear spell, and nothing in this mod changes the weather.
- There is no height fog and no fog that sits in the sky: Unity's fog is one global colour and density, and a fog volume needs a shader the game doesn't ship. Mist banks stand in for it, over the two yards only.
- Mist puffs are flat billboards without soft edges, so one that drifts into a wall or the ground shows a hard line there.
- Window rain is drawn on the inside of the glass and only seen from inside; from the yard the windows look as they did. Some windows face a wall close outside, and that wall is what shows through their drops.
- The drops are a 256 by 144 picture, so very close up their edges are chunky, and all windows share one picture, so two windows in view show the same pattern. Every drop has the game's teardrop shape pointing up, also the ones sitting still, and at rest in a dark room they only show as faint glints on the glass; the refraction needs something lit behind them (a flash, a lamp outside).
- The glass needs the game's `Glass/DiffuseBumped` material; if a game update renames it, the look logs a warning and stays off.
- Each pane in view adds a draw and a screen grab (the shader bends the picture behind it); in the house that is one or two panes at a time.
- A custom shader (drops and trails drawn on the graphics card, height fog, wet surfaces) would need an asset bundle built in the Unity editor, which needs the editor installed and an account to activate it. None of that is in the repo.
