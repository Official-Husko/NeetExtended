using HarmonyLib;
using Mixtapes.Features;
using Mixtapes.Radio;

namespace Mixtapes.Patches;

/// <summary>The station: started from the radio's own Update, stopped when a tape goes in.</summary>
[HarmonyPatch]
[HarmonyPatchCategory(RadioStationFeature.FeatureId)]
internal static class RadioPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(global::Radio), "Update")]
    private static void AfterUpdate(global::Radio __instance) => RadioStation.Tick(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(global::Radio), nameof(global::Radio.InsertCassette))]
    private static void BeforeInsert(global::Radio __instance) => RadioStation.TapeIn(__instance);
}
