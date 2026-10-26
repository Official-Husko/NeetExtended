using System.Collections.Generic;

namespace CardExpansion.Features;

/// <summary>What this mod keeps in the save file.</summary>
public sealed class CardState
{
    public bool AutoClickerOwned;
    public bool AutoClickerOn = true;
    public int Summons;
    public int Shinies;

    /// <summary>
    /// Base values per card (income, price, exp). The game doesn't save them, so promoted cards
    /// lost their doubled base after a reload. Index matches the card order.
    /// </summary>
    public List<float[]> CardBases = new List<float[]>();
}
