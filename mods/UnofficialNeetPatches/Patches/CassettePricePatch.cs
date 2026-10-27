using System;
using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// CustomItemShop.CacheItemToOrder shows the item's price plus 10$ for custom pictures (type 0) and
/// without it for custom tapes (type 1), but both buy buttons order with priceOffset 10
/// (CustomItemShop.cs:77-92). For tapes the buy button is set up again without the surcharge.
/// </summary>
[HarmonyPatch(typeof(CustomItemShop), nameof(CustomItemShop.CacheItemToOrder))]
[HarmonyPatchCategory(CassettePriceFix.FeatureId)]
internal static class CassettePricePatch
{
    private const int MusicType = 1;

    private static readonly Action<CustomItemShop, GameObject> ApplySong =
        AccessTools.MethodDelegate<Action<CustomItemShop, GameObject>>(AccessTools.Method(typeof(CustomItemShop), "CustomizeMediaURLsOnItem"));

    private static void Postfix(CustomItemShop __instance, int i, int type)
    {
        if (type != MusicType || __instance.customPickups?.pickups == null || i < 0 || i >= __instance.customPickups.pickups.Count)
            return;
        var shop = __instance;
        var item = shop.customPickups.pickups[i].obj;
        shop.confirmBuyButton.onClick.RemoveAllListeners();
        shop.confirmBuyButton.onClick.AddListener(() =>
            shop.GetComponentInParent<Computer>().OrderItem(item, null, 0f, ordered => ApplySong(shop, ordered)));
    }
}
