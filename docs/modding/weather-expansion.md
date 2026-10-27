# Weather Expansion

The Weather Expansion mod (`mods/WeatherExpansion`, id `WeatherExpansion`) changes how the storm outside looks. The house is one rainy night with thunder; this mod adds looks to it. Each look is a `ModFeature` marked experimental, so it can be switched off on its own on the Mods page, and each one undoes itself when it's switched off, when the mod unloads (hot reload) and when the house scene goes away.

It only uses what the game already ships: no custom shader and no extra download. What that rules out is under [Known limitations](#known-limitations).

## What's local in co-op

All of it. Every game draws its own looks from its own scene: nothing is sent over the network and no setting is `[Shared]`. The looks that follow lightning read the moon light's intensity (the game's own flash) in the local game, so they light up whenever that game's moon does; whether a client's moon flashes along with the host's is up to the co-op world sync, not this mod. The storm's state (drizzle, normal, heavy) is not part of this mod; when the framework's Sky API has one, it plugs into `Storm/StormInputs.cs` and every look follows it.

## Looks and settings

The looks arrive as features, each with its own switch on the Mods page and its own settings.

## Known limitations

- The mod has no looks yet.
