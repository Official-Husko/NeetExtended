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

## How the fog looks work

`Fog/FogLook.cs` owns the scene's `RenderSettings.fogColor`, `fogDensity` and the sky material's `_Tint` while either fog look is on. Two features want those values, so one class applies them, once per frame from `WeatherExpansionMod.OnUpdate` (0.4 microseconds, no allocation). The game's own values are captured when the house is ready and put back when the last fog look goes off, when the mod unloads and when the house scene unloads (the sky material is a shared asset and would otherwise keep the flash tint into the main menu).

- **Stormy fog**: colour `(0.035, 0.05, 0.085)` times the brightness setting, density = the game's density (0.06, ExponentialSquared) times the thickness setting times `StormInputs.FogDensity` times a drift of about plus or minus 16% (two slow sine waves, a minute or so).
- **Lightning glow**: `Fog/FlashLevel.cs` turns the moon light's intensity into a 0 to 1 flash (`HouseManager.Thunder` pushes the moon from 0.2 to 14-22 and back; 14 counts as full). The fog colour is lerped to `(0.40, 0.47, 0.66)` and the sky `_Tint` from the game's 0.038 grey to `(0.50, 0.58, 0.78)` by that amount times the setting. Following the light itself needs no event, so it survives house loads, story strikes (`Thunder(instant: true)`) and hot reloads.
- The sky is `Skybox_CubeTest`, a `Skybox/Cubemap` of a moonlit golf course (`moonlit_golf_4k`) darkened to 0.038, which is why the sky is black. Brought up, it shows hills, trees and a few lights on the horizon, which is what lights up behind the yard in a flash.

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
- A custom shader (drops and trails drawn on the graphics card, height fog, wet surfaces) would need an asset bundle built in the Unity editor, which needs the editor installed and an account to activate it. None of that is in the repo.
