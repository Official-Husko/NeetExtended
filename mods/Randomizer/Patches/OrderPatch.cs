using System;
using HarmonyLib;
using Randomizer.Features;
using Randomizer.Parts;
using UnityEngine;

namespace Randomizer.Patches;

/// <summary>Mystery deliveries: the item is swapped before the order goes in.</summary>
[HarmonyPatch(typeof(Computer), nameof(Computer.OrderItem))]
[HarmonyPatchCategory(MysteryDeliveriesFeature.FeatureId)]
internal static class OrderPatch
{
    // Runs before the framework's postfix, so mods hear about what's actually on its way.
    private static void Prefix(ref GameObject obj, ref float priceOffset, Action<GameObject> OnUnboxingApplyToContents) =>
        MysteryOrders.BeforeOrder(ref obj, ref priceOffset, OnUnboxingApplyToContents != null);
}
