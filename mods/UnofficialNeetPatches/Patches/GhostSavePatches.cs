using System;
using System.Collections.Generic;
using HarmonyLib;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// Saveable things subscribe to SaveLoadHandler.OnSave and are meant to unsubscribe when they go
/// away. Two kinds don't:
/// <list type="bullet">
/// <item>FireSpreader subscribes in Start and never unsubscribes. Clear destroys it, but the
/// destroyed spreader still saves its room, and loading that entry calls Locator.StartRoomFire,
/// which lights the room again.</item>
/// <item>Usable unsubscribes in OnDestroy, which Unity only calls for objects that were active at
/// some point. LoadUsables calls Start (and so subscribes) on items it loads inactive, like a fuse
/// inside a device. When such an item is destroyed, its Save keeps running and throws, and the
/// exception ends the whole save.</item>
/// </list>
/// The prefix drops subscribers whose object is destroyed before the game calls them; the postfix
/// unsubscribes a cleared fire right away so a save in the same frame skips it too.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(GhostSavesFix.FeatureId)]
internal static class GhostSavePatches
{
    private static readonly AccessTools.FieldRef<SaveLoadHandler, Func<SaveData>> OnSave =
        AccessTools.FieldRefAccess<SaveLoadHandler, Func<SaveData>>("OnSave");

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.AllSaveDataToJSon))]
    private static void DropDestroyed(SaveLoadHandler __instance)
    {
        var subscribers = OnSave(__instance);
        if (subscribers == null)
            return;

        List<string> dropped = null;
        foreach (var subscriber in subscribers.GetInvocationList())
        {
            if (!(subscriber.Target is UnityEngine.Object owner) || owner != null)
                continue;
            __instance.UnsubSave((Func<SaveData>)subscriber);
            (dropped ??= new List<string>()).Add(PatchReports.Describe(subscriber.Target));
        }

        if (dropped != null)
            PatchReports.GhostsDropped(dropped);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FireSpreader), nameof(FireSpreader.Clear))]
    private static void AfterFireCleared(FireSpreader __instance)
    {
        if (SaveLoadHandler._ != null)
            SaveLoadHandler._.UnsubSave(__instance.Save);
    }
}
