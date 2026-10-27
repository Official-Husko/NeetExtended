using System.Collections;
using Coop.World.Ambience;
using HarmonyLib;

namespace Coop.World.Patches;

/// <summary>The sky and the toilets: see <see cref="SkySync"/> and <see cref="ToiletSync"/>.</summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class AmbiencePatches
{
    // Every thunder coroutine goes through here, the game's own and the host's replayed strikes.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(HouseManager), nameof(HouseManager.Thunder))]
    private static void Thunder(ref IEnumerator __result) => __result = SkySync.Wrap(__result, SkySync.TakeReplay());

    // Another game is pulling this handle: it decides the flush, this one only shows the handle.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Toilet_Flush), "FixedUpdate")]
    private static bool Flush(Toilet_Flush __instance) => !WorldSide.Running || !ToiletSync.IsRemote(__instance);
}
