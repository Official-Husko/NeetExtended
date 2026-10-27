using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// A character looking for something to pick up draws from its room's memory
/// (Brain.AnalyzeRoomInventory, Brain.cs:224), which is the live Locator.usables list: whatever
/// entered the room's trigger and hasn't left (Locator.cs:45-62). Nothing in
/// BrainNeetAI.FindObjInroom (:350-371) or PickupTargetObj (:403-419) checks that the thing is
/// switched on, and Player_Abilities.Pickup with a given object doesn't check reach either (:1120),
/// so a character can take the beanie of the thief while he's still switched off.
/// PickupTargetObj gives up on such a target and drops it from the rooms' lists.
/// </summary>
[HarmonyPatch(typeof(BrainNeetAI), "PickupTargetObj")]
[HarmonyPatchCategory(HiddenPickupsFix.FeatureId)]
internal static class HiddenPickupsPatch
{
    private static bool Prefix(BrainNeetAI __instance, ref NodeState __result)
    {
        var target = __instance.pickupTgt?.Obj;
        if (target == null || target.activeInHierarchy)
            return true;

        Forget(target);
        __instance.pickupTgt = null;
        __result = NodeState.Failure;
        return false;
    }

    // Rare path, so walking all the rooms is fine.
    private static void Forget(GameObject item)
    {
        foreach (var room in Locator.rooms.Values)
        {
            if (room != null)
                room.GetComponent<Locator>().usables.Remove(item);
        }
    }
}
