# Randomizer

The Randomizer mod (`mods/Randomizer`) gives every save a seed that shuffles parts of the game: who knocks at night and in what order, what the store charges, what deliveries bring, where things lie in the house on a new game, and how fast needs drain. The same seed always gives the same game, and the seed is kept in the save, so loading never changes anything.

## The parts

Each part is a switch on the Mods page (all on by default):

| Switch | What it does |
| --- | --- |
| **Shuffled visitors** | Night 1's six visitors (GhostBoy, Emo, DeathBlue, Biker, MushroomBoy, Poopman) come in a shuffled order. GhostBoy's second visit, the one that ends the demo's visitors, stays last. Visitors that already came, or the one at the door, keep their place. |
| **Random prices** | Every store item costs up to the price range (Settings, 50% by default) more or less than the game's. Coins and money stacks keep their value. Store labels, the order confirmation, the charge and the sell value all follow. |
| **Mystery deliveries** | With the chance in Settings (15%), an order brings something else from the same store, between half and one and a half times its price, for the price of what you ordered. Orders from the custom shops (your picture, your song) are left alone. |
| **Shuffled house** | On a new game, loose items in the rooms you can reach from the start swap places. Keys, notes, things fixed to walls or plugged in, and anything heavier than 30 kg stay put, and nothing moves into or out of the basement backroom, the attic, the pantry, the shed or the yard, so the game can still be finished. Only for games started with it on. |
| **Random needs** | Hunger, energy, bladder and hygiene each drain up to the needs range (Settings, 30%) faster or slower than the game's. |

The **Randomizer** page (mod menu, Mod Settings, Randomizer) shows the save's seed, the visitors in the order they'll come and how fast each need drains, with ROLL AGAIN for a new seed (prices, needs and the visitors still to come change; the house stays).

**Seed for new games** in Settings: leave it empty for a random seed, or put in any text or number to play a seed someone shared. It's read when a save gets its seed: a new game, or a save from before the mod was installed (those keep their house as it is).

## What isn't randomized, and why

- **Dreams** are already random every time (`DreamRandomizer`: layout, materials, lights, time speed, loot and characters).
- **Nights 2 to 5**: the game never moves past night 1, and their visitor lists are empty in the demo.
- **Phone numbers and keys**: the house's notes and hints point at them, and moving keys could lock a save.

## How it works

- **Seed** (`Seeds/SeedState`): kept with `ModSaveData`. A save without one gets one when it loads; if there was no save file at all (`SaveFiles.Newest()` before the load), it's a new game and the house gets shuffled. Every decision uses its own `System.Random` made from the seed and a name ("visitors", "price:Mug", "order:12"), with an FNV hash so it's the same on every run. The game's `UnityEngine.Random` isn't touched.
- **Visitors**: `EncounterSpawner.Nights[0]` is rebuilt from `Night1Encounters` on every house load. The mod replaces it with the save's order (a list of indexes, saved), applied when the house is ready. `encounterId` points into that list, so the order never changes for slots that already happened.
- **Prices**: `Usable.price` on every store's items (the prefabs in each `StoreGenerator.pickupBank`) and on items already in the house, from a snapshot so switching off restores the game's prices. `StorePages.RefreshPriceLabels` (framework) rewrites the store buttons' labels.
- **Mystery deliveries**: a prefix on `Computer.OrderItem` swaps the item and adds the price difference to `priceOffset`, so the money check and the charge stay those of the ordered item.
- **House**: on the first house load of a new game, the positions and rotations of the candidate items are shuffled among themselves.
- **Needs**: `PlayerStats`' static drain rates, from the game's values read once.
