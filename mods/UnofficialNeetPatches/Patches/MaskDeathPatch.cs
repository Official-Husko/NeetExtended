using HarmonyLib;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// Player_Abilities.DieAccessor sets dead = true and then takes the masks off with
/// foreach (mask in masks) mask.RemoveMask(...) (Player_Abilities.cs:1720-1724), but RemoveMask
/// removes the mask from that same list (MaskFace.cs, masks.Remove). With a mask on, the loop throws,
/// the rest of the death never runs (no animation, no death coroutine, no respawn), and because dead is
/// already true the body can't die again. Taking the masks off from a copy of the list first leaves
/// the game's loop nothing to trip over. Last, so a death another mod turns away (god mode, co-op's
/// going down) keeps its masks.
/// </summary>
[HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.DieAccessor), typeof(bool))]
[HarmonyPatchCategory(MaskDeathFix.FeatureId)]
internal static class MaskDeathPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(Player_Abilities __instance, bool __runOriginal)
    {
        var masks = __instance.masks;
        if (!__runOriginal || __instance.dead || masks == null || masks.Count == 0)
            return;
        foreach (var mask in masks.ToArray())
        {
            if (mask != null && mask.TryGetComponent(out MaskFace face))
                face.RemoveMask(__instance.gameObject);
        }
    }
}
