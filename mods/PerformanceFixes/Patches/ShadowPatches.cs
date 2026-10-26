using HarmonyLib;
using NeetModKit.Events;
using PerformanceFixes.Features;
using PerformanceFixes.Lighting;
using UnityEngine;

namespace PerformanceFixes.Patches;

/// <summary>Lights that show up after the house is ready: each corpse's soul light and lamps in spawned items.</summary>
[HarmonyPatch]
[HarmonyPatchCategory(ShadowsFeature.FeatureId)]
internal static class ShadowPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(HouseManager), nameof(HouseManager.SoulifyCorpse))]
    private static void AfterSoulify(GameObject __result) => ShadowControl.Spawned(__result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Usable), nameof(Usable.Start))]
    private static void AfterItemStart(Usable __instance)
    {
        // Items loading with the house are covered when it's ready.
        if (GameEvents.IsHouseReady)
            ShadowControl.Spawned(__instance.gameObject);
    }
}
