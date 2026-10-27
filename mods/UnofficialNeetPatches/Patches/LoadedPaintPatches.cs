using System;
using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// SaveLoadHandler.Start loads the paint pictures (LoadPaintTargets, SaveLoadHandler.cs:443-466) into
/// surfaces whose own Start may not have run yet: Unity runs the Starts of one frame in no set order.
/// Such a surface has no renderer yet (PaintTarget.CheckValid sets it, PaintTarget.cs:545-556), so
/// SetupPaint never gives the material its paint and position textures (CreateTextures, :592-601) and
/// renders an empty position texture (RenderTextures, :606-616). The picture is there but never
/// shows, the next splat there draws nothing and copies the picture away, and the surface only
/// recovers once it moves. Finding the renderer first sets these up like any other.
///
/// A surface with SetupOnStart then starts (:558-565) and would set itself up again with empty
/// textures, losing the picture it just got; one already set up only finds its renderer.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(LoadedPaintFix.FeatureId)]
internal static class LoadedPaintPatches
{
    private static readonly AccessTools.FieldRef<PaintTarget, Renderer> PaintRenderer =
        AccessTools.FieldRefAccess<PaintTarget, Renderer>("paintRenderer");

    private static readonly Action<PaintTarget> CheckValid =
        AccessTools.MethodDelegate<Action<PaintTarget>>(AccessTools.Method(typeof(PaintTarget), "CheckValid"));

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PaintTarget), nameof(PaintTarget.SetupPaint))]
    private static void BeforeSetup(PaintTarget __instance)
    {
        if (PaintRenderer(__instance) == null)
            CheckValid(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PaintTarget), "Start")]
    private static bool BeforeStart(PaintTarget __instance)
    {
        if (!__instance.setupComplete)
            return true;
        CheckValid(__instance);
        return false;
    }
}
