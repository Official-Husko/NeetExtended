using System;
using HarmonyLib;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// A HeatHolder (a frying pan) remembers the flames touching it in <c>flamesInContact</c>: added in
/// OnTriggerEnter, removed in OnTriggerExit (HeatHolder.cs:131-152). A flame that is destroyed while it
/// touches the pan never leaves: Unity sends no exit for a destroyed component, and the exit of the
/// thing it sat on finds no Flame to remove. Food that catches fire in the pan gets a Flame of its own
/// (Flame.AddBurnCollider) which the game destroys again, so the pan keeps a dead entry. Update calls
/// DistributeHeat for every entry, and DistributeHeat resets the 2 second cooling delay even for a dead
/// one (HeatHolder.cs:190), so the pan never cools. Dead entries are dropped before Update runs.
/// </summary>
[HarmonyPatch(typeof(HeatHolder), "Update")]
[HarmonyPatchCategory(PanCoolingFix.FeatureId)]
internal static class PanCoolingPatch
{
    private static readonly Predicate<Flame> Destroyed = flame => flame == null;

    private static void Prefix(HeatHolder __instance)
    {
        var touching = __instance.flamesInContact;
        if (touching != null && touching.Count != 0)
            touching.RemoveWhere(Destroyed);
    }
}
