using System.Collections.Generic;
using Randomizer.Seeds;
using UnityEngine;

namespace Randomizer.Parts;

/// <summary>
/// Now and then an order brings something else from the same store, of a similar price, for the
/// price of what was ordered. Orders from the custom shops (with a picture or song) are left alone.
/// </summary>
internal static class MysteryOrders
{
    private static readonly List<GameObject> Pool = new List<GameObject>();

    public static bool Enabled { get; set; }

    /// <summary>Before Computer.OrderItem: maybe swaps the item, keeping the charge the same.</summary>
    public static void BeforeOrder(ref GameObject item, ref float priceOffset, bool customized)
    {
        if (!Enabled || customized || item == null)
            return;
        var ordered = item.GetComponentInChildren<Usable>();
        if (ordered == null)
            return;

        var state = SeedState.Current;
        var roll = Rolls.For(state.Seed, "order:" + state.Orders++);
        if (roll.NextDouble() * 100.0 >= RandomizerMod.Config.Value.MysteryChance)
            return;

        var swap = Pick(item, ordered, roll);
        if (swap == null)
            return;
        priceOffset += ordered.price - swap.GetComponentInChildren<Usable>().price;
        item = swap;
    }

    // Something from the same store, from half to one and a half times the price.
    private static GameObject Pick(GameObject item, Usable ordered, System.Random roll)
    {
        Pool.Clear();
        foreach (var store in Object.FindObjectsOfType<StoreGenerator>(true))
        {
            if (store.storeType != ordered.purchaseableAt || store.pickupBank?.pickups == null)
                continue;
            foreach (var pickup in store.pickupBank.pickups)
            {
                var usable = pickup.obj != null ? pickup.obj.GetComponentInChildren<Usable>() : null;
                if (usable == null || pickup.obj == item || !Prices.Randomizable(pickup.obj.name, usable))
                    continue;
                if (usable.price >= ordered.price * 0.5f && usable.price <= ordered.price * 1.5f && !Pool.Contains(pickup.obj))
                    Pool.Add(pickup.obj);
            }
        }
        return Pool.Count > 0 ? Pool[roll.Next(Pool.Count)] : null;
    }
}
