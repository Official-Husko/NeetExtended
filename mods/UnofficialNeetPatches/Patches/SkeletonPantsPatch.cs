using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// Player_Abilities.Shit dirties whatever is in clothings[1] and copies its material into the body's
/// second material slot (Player_Abilities.cs:1564-1569). Skeletons have noClothUVs set: their body
/// has a single material, and their clothings hold the outfit prefabs themselves, not worn copies. So
/// MakeDirty runs on the Pants_Black prefab (every pair spawned after that is dirty, sticky and
/// paints), then materials[1] throws. Every other clothes path in the game checks noClothUVs. Hiding
/// the pants for the call sends such a body down the game's no-pants path: a turd on the floor.
/// </summary>
[HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.Shit))]
[HarmonyPatchCategory(SkeletonPantsFix.FeatureId)]
internal static class SkeletonPantsPatch
{
    private static void Prefix(Player_Abilities __instance, out GameObject __state)
    {
        __state = null;
        var clothes = __instance.clothings;
        if (!__instance.noClothUVs || clothes == null || clothes.Length < 2)
            return;
        __state = clothes[1];
        clothes[1] = null;
    }

    private static void Finalizer(Player_Abilities __instance, GameObject __state)
    {
        if (__state != null)
            __instance.clothings[1] = __state;
    }
}
