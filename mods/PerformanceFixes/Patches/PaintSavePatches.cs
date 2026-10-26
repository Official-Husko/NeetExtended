using HarmonyLib;
using PerformanceFixes.Features;
using PerformanceFixes.Painting;
using UnityEngine;

namespace PerformanceFixes.Patches;

/// <summary>Hands paint saving to <see cref="PaintSaver"/> and tells <see cref="PaintTracker"/> when paint changes.</summary>
[HarmonyPatch]
[HarmonyPatchCategory(FastPaintSavesFeature.FeatureId)]
internal static class PaintSavePatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SaveLoadHandler), "SavePaintSplats")]
    private static bool SavePaint(string path)
    {
        PaintSaver.Save(path);
        return false;
    }

    // The game reads the pictures back here, so anything still being written has to land first.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SaveLoadHandler), "LoadPaintTargets")]
    private static void BeforeLoadingPaint()
    {
        PaintSaver.Flush();
        PaintTracker.Forget();
        PaintTracker.Loading = true;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(SaveLoadHandler), "LoadPaintTargets")]
    private static void AfterLoadingPaint() => PaintTracker.Loading = false;

    // While loading, a texture handed in is the picture from disk, so the surface matches it.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PaintTarget), nameof(PaintTarget.SetupPaint))]
    private static void AfterSetup(PaintTarget __instance, Texture2D tex)
    {
        if (PaintTracker.Loading && tex != null)
            PaintTracker.MarkSaved(__instance);
        else
            PaintTracker.Changed(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PaintTarget), nameof(PaintTarget.PaintSplat))]
    private static void AfterSplat(PaintTarget __instance) => PaintTracker.Changed(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PaintTarget), nameof(PaintTarget.ClearPaint))]
    private static void AfterClear(PaintTarget __instance) => PaintTracker.Changed(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PaintTarget), nameof(PaintTarget.ClearWaterChannel))]
    private static void AfterDrying(PaintTarget __instance) => PaintTracker.Changed(__instance);
}
