using HarmonyLib;
using NeetModKit.Items;

namespace NeetModKit.Patches;

[HarmonyPatch]
internal static class ItemPatches
{
    // Belt and braces: make sure modded items exist before the save is read, whatever order
    // HouseManager.Start and SaveLoadHandler.Start run in.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.LoadAll))]
    private static void RegisterBeforeLoad()
    {
        if (PickupBank.pickupsDict.Count > 0)
            ItemRegistry.RegisterAll();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StoreGenerator), "Start")]
    private static void AfterStoreStart(StoreGenerator __instance) => StoreListings.OnStoreStarted(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StoreGenerator), "OnEnable")]
    private static void AfterStoreEnable(StoreGenerator __instance) => StoreListings.GrowPage(__instance);
}
