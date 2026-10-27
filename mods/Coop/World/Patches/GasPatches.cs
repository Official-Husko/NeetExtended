using Coop.World.Flames;
using HarmonyLib;

namespace Coop.World.Patches;

/// <summary>The stove's gas lighting and going out; see <see cref="GasSync"/>.</summary>
[HarmonyPatchCategory(WorldPatches.World)]
[HarmonyPatch]
internal static class GasPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GasButton), nameof(GasButton.GasIgnite))]
    private static void Igniting(GasButton __instance) => GasSync.Lighting(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GasButton), nameof(GasButton.GasExtinguish))]
    private static void Extinguished(GasButton __instance) => GasSync.PutOut(__instance);
}
