using System.Collections.Generic;
using NeetModKit.Items;
using Randomizer.Seeds;
using UnityEngine;

namespace Randomizer.Parts;

/// <summary>
/// Every store item costs somewhere within the price range of what the game asks, the same for the
/// whole save. Store labels, the confirmation text, the charge and the sell value all follow.
/// </summary>
internal static class Prices
{
    // Every price changed, with what it was, so switching off puts them back exactly.
    private static readonly Dictionary<Usable, float> Original = new Dictionary<Usable, float>();
    private static readonly Dictionary<string, float> ByItem = new Dictionary<string, float>();

    public static bool Enabled { get; set; }

    public static void Apply()
    {
        Restore();
        if (Enabled)
        {
            float range = Mathf.Clamp01(RandomizerMod.Config.Value.PriceRange / 100f);
            foreach (var store in Object.FindObjectsOfType<StoreGenerator>(true))
            {
                if (store.pickupBank?.pickups == null)
                    continue;
                foreach (var pickup in store.pickupBank.pickups)
                    Change(pickup.obj, range);
            }
            // Items already in the house were made with the old price; selling them uses theirs.
            foreach (var usable in Object.FindObjectsOfType<Usable>())
            {
                if (usable.prefabName != null && ByItem.TryGetValue(usable.prefabName, out float price) && !Original.ContainsKey(usable))
                {
                    Original[usable] = usable.price;
                    usable.price = price;
                }
            }
        }
        StorePages.RefreshPriceLabels();
    }

    public static void Restore()
    {
        foreach (var pair in Original)
        {
            if (pair.Key != null)
                pair.Key.price = pair.Value;
        }
        Original.Clear();
        ByItem.Clear();
    }

    /// <summary>The price the item had before, or its price now if it wasn't changed.</summary>
    public static float Before(Usable usable) => usable != null && Original.TryGetValue(usable, out float price) ? price : usable?.price ?? 0f;

    private static void Change(GameObject item, float range)
    {
        var usable = item != null ? item.GetComponentInChildren<Usable>() : null;
        if (usable == null || Original.ContainsKey(usable) || !Randomizable(item.name, usable))
            return;
        var roll = Rolls.For(SeedState.Seed, "price:" + item.name);
        float price = Mathf.Max(1f, Mathf.Round(usable.price * (float)roll.Between(1f - range, 1f + range)));
        Original[usable] = usable.price;
        usable.price = price;
        ByItem[item.name] = price;
    }

    // Money keeps its value: coins and money stacks are worth what they say.
    internal static bool Randomizable(string name, Usable usable) =>
        usable.price > 0f && usable.purchaseableAt != Usable.StoreType.Nowhere &&
        !name.StartsWith("Money", System.StringComparison.Ordinal) && !name.StartsWith("Coin", System.StringComparison.Ordinal);
}
