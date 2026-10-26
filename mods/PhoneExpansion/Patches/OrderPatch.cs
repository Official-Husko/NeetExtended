using HarmonyLib;
using PhoneExpansion.Calls;
using UnityEngine;

namespace PhoneExpansion.Patches;

/// <summary>Lets calls ring after the player orders something from a computer store.</summary>
[HarmonyPatch(typeof(Computer), nameof(Computer.OrderItem))]
internal static class OrderPatch
{
    private static void Postfix(GameObject obj, GameObject __result)
    {
        if (__result == null || obj == null)
            return;
        var usable = obj.GetComponentInChildren<Usable>();
        IncomingCalls.OnOrder(usable != null ? usable.purchaseableAt.ToString() : null);
    }
}
