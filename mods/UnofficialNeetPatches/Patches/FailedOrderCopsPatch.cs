using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// Computer.OrderItem rolls the 20% cop van for black and occult market orders (Computer.cs:245)
/// before it checks the demo block, the money and the order cooldown (:249). So orders that never
/// went through could still bring the cops. The van is held back during the order and only called
/// once the order went through.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(FailedOrderCopsFix.FeatureId)]
internal static class FailedOrderCopsPatch
{
    private static bool _ordering;
    private static bool _vanRolled;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Computer), nameof(Computer.OrderItem))]
    private static void BeforeOrder()
    {
        _ordering = true;
        _vanRolled = false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Computer), nameof(Computer.OrderItem))]
    private static void AfterOrder(GameObject __result)
    {
        _ordering = false;
        if (_vanRolled && __result != null)
            HouseManager._.SpawnCopVan();
        _vanRolled = false;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Computer), nameof(Computer.OrderItem))]
    private static void Finally() => _ordering = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HouseManager), nameof(HouseManager.SpawnCopVan))]
    private static bool BeforeCopVan()
    {
        if (!_ordering)
            return true;
        _vanRolled = true; // Comes later, if the order goes through.
        return false;
    }
}
