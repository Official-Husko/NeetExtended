using HarmonyLib;
using NeetModKit.Events;
using UnityEngine;

namespace NeetModKit.Patches;

/// <summary>Turns a successful Computer.OrderItem (it returns the ordered object, or null when refused) into <see cref="GameEvents.ItemOrdered"/>.</summary>
[HarmonyPatch(typeof(Computer), nameof(Computer.OrderItem))]
internal static class OrderPatch
{
    private static void Postfix(GameObject obj, GameObject __result)
    {
        if (__result == null || obj == null)
            return;
        var usable = obj.GetComponentInChildren<Usable>();
        string item = obj.name.Replace("(Clone)", "").Trim();
        GameEvents.RaiseOrder(new StoreOrder(item, usable != null ? usable.purchaseableAt.ToString() : null));
    }
}
