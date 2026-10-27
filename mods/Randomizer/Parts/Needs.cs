using Randomizer.Seeds;
using UnityEngine;

namespace Randomizer.Parts;

/// <summary>Hunger, energy, bladder and hygiene each drain faster or slower than the game's, the same for the whole save.</summary>
internal static class Needs
{
    // The game's rates, read once, before anything changed them.
    private static readonly float Hunger = PlayerStats.hungerDecreaseRate;
    private static readonly float Energy = PlayerStats.energyDecreaseRate;
    private static readonly float Bladder = PlayerStats.bladderDecreaseRate;
    private static readonly float Hygiene = PlayerStats.hygieneDecreaseRate;

    public static bool Enabled { get; set; }

    public static void Apply()
    {
        Restore();
        if (!Enabled)
            return;
        float range = Mathf.Clamp01(RandomizerMod.Config.Value.NeedsRange / 100f);
        PlayerStats.hungerDecreaseRate = Hunger * Factor("hunger", range);
        PlayerStats.energyDecreaseRate = Energy * Factor("energy", range);
        PlayerStats.bladderDecreaseRate = Bladder * Factor("bladder", range);
        PlayerStats.hygieneDecreaseRate = Hygiene * Factor("hygiene", range);
    }

    public static void Restore()
    {
        PlayerStats.hungerDecreaseRate = Hunger;
        PlayerStats.energyDecreaseRate = Energy;
        PlayerStats.bladderDecreaseRate = Bladder;
        PlayerStats.hygieneDecreaseRate = Hygiene;
    }

    /// <summary>How fast a need drains compared to the game, for the menu: 1.25 is 25% faster.</summary>
    public static float Speed(string need) => Factor(need, Mathf.Clamp01(RandomizerMod.Config.Value.NeedsRange / 100f));

    private static float Factor(string need, float range) =>
        (float)Rolls.For(SeedState.Seed, "need:" + need).Between(1f - range, 1f + range);
}
