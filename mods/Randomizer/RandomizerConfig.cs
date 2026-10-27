using System.ComponentModel;
using NeetModKit.Settings;

namespace Randomizer;

/// <summary>Stored in Mods/Randomizer/config.json and shown in the mod menu.</summary>
public sealed class RandomizerConfig
{
    [Label("Seed for new games")]
    [Description("Empty for a random seed. Any text works: two games with the same seed get the same visitors, prices and house.")]
    [Shared]
    public string Seed = "";

    [Label("Price range (%)")]
    [Description("How far store prices can move from the game's, up or down.")]
    [Range(0, 90)]
    [Shared]
    public int PriceRange = 50;

    [Label("Mystery delivery chance (%)")]
    [Description("How often an order brings something else from the same store, for the price of what you ordered.")]
    [Range(0, 100)]
    [Shared]
    public int MysteryChance = 15;

    [Label("Needs range (%)")]
    [Description("How far hunger, energy, bladder and hygiene can drain faster or slower than the game's.")]
    [Range(0, 90)]
    [Shared]
    public int NeedsRange = 30;
}
