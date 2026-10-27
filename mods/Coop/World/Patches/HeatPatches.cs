using Coop.World.State;
using HarmonyLib;
using UnityEngine;

namespace Coop.World.Patches;

/// <summary>
/// The heat of pans and pots (HeatHolder) is worked out by the game that simulates the pan, and the
/// others take it from the state that arrives (HeatState). A pan that is only a copy here doesn't
/// warm up or cool down on its own, so its flame level, which cooks the food in it and burns whoever
/// touches it, is the owner's. The food is cooked where it is simulated (ItemStatePatches), from the
/// level the pan has in that game, so nothing cooks twice.
/// </summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class HeatPatches
{
    private static readonly AccessTools.FieldRef<HeatHolder, Flame> FlameOf =
        AccessTools.FieldRefAccess<HeatHolder, Flame>("flame");

    // Flames in contact and the slow cooling are the owner's. The copy only keeps its flame at the
    // level its heat says (water or the extinguisher put it out in this game; the owner's Update
    // would have lit it again the next frame).
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HeatHolder), "Update")]
    private static bool Heating(HeatHolder __instance)
    {
        if (!WorldSide.Running || OwnerGatePatches.Simulates(__instance.gameObject))
            return true;
        var flame = FlameOf(__instance);
        if (flame != null && flame._heatLevel != LevelOf(__instance._heatIndex, flame.maxHeatLevel))
            __instance.HeatIndex = __instance._heatIndex;
        return false;
    }

    // Water on a hot pan makes steam and a hiss in every game, but only the owner cools it.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HeatHolder), nameof(HeatHolder.WaterContact))]
    private static void Splashing(HeatHolder __instance, out float __state) => __state = __instance._heatIndex;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(HeatHolder), nameof(HeatHolder.WaterContact))]
    private static void Splashed(HeatHolder __instance, float __state)
    {
        if (WorldSide.Running && __instance._heatIndex != __state && !OwnerGatePatches.Simulates(__instance.gameObject))
            __instance.HeatIndex = __state;
    }

    // The level the game gives a heat (HeatHolder.HeatIndex), kept to what the flame can reach.
    private static HeatLevel LevelOf(float heat, HeatLevel max)
    {
        var level = heat < 20f ? HeatLevel.Cold : heat < 40f ? HeatLevel.Low : heat < 75f ? HeatLevel.Medium : HeatLevel.High;
        return level > max ? max : level;
    }
}
