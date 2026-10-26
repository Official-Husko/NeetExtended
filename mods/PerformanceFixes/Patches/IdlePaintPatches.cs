using HarmonyLib;
using PerformanceFixes.Features;
using PerformanceFixes.Painting;

namespace PerformanceFixes.Patches;

/// <summary>
/// PaintTarget.Update only draws queued splats, and all 1,200-odd surfaces run it every frame with
/// nothing queued (0.3 ms a frame). A surface switches itself off once its queue is empty and on
/// again when something paints on it. Splats only reach the queue through PaintSplat, and the rest
/// of the game calls PaintTarget's methods directly, which works on a disabled component.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(IdlePaintFeature.FeatureId)]
internal static class IdlePaintPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PaintTarget), "Update")]
    private static void AfterUpdate(PaintTarget __instance)
    {
        if (PaintTracker.Pending(__instance) == 0)
            __instance.enabled = false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PaintTarget), nameof(PaintTarget.PaintSplat))]
    private static void AfterSplat(PaintTarget __instance)
    {
        if (!__instance.enabled)
            __instance.enabled = true;
    }
}
