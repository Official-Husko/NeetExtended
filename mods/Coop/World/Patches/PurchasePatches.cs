using System;
using Coop.World.Economy;
using HarmonyLib;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>Store orders and app purchases (requests on a client) and custom shop boxes; see <see cref="Purchases"/>.</summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class PurchasePatches
{
    // First, so mods that change orders (they patch the same method) see the host's run, not this one.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(Computer), nameof(Computer.OrderItem))]
    private static bool Order(Computer __instance, ref GameObject obj, Transform deliverySpot, float priceOffset, Action<GameObject> OnUnboxingApplyToContents) =>
        Purchases.BeforeOrder(__instance, ref obj, deliverySpot, priceOffset, OnUnboxingApplyToContents);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AppDownloader), nameof(AppDownloader.BuyApp), new[] { typeof(int), typeof(bool) })]
    private static bool BuyApp(int appId, bool enableOnly) => Purchases.BeforeApp(appId, enableOnly);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AppDownloader), nameof(AppDownloader.BuyApp), new[] { typeof(int), typeof(bool) })]
    private static void BoughtApp(AppDownloader __instance, bool enableOnly, bool __runOriginal)
    {
        if (__runOriginal)
            AppSync.AfterBuy(__instance, enableOnly);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Cardboard_Box), "Start")]
    private static void BoxStarted(Cardboard_Box __instance) => CustomBoxes.Started(__instance);
}
