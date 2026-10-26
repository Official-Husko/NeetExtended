using HarmonyLib;
using PerformanceFixes.Features;
using PerformanceFixes.Leaks;
using PerformanceFixes.Rendering;
using UnityEngine;

namespace PerformanceFixes.Patches;

/// <summary>The places the game creates textures and loses them. See <see cref="TextureLeaksFeature"/>.</summary>
[HarmonyPatch]
[HarmonyPatchCategory(TextureLeaksFeature.FeatureId)]
internal static class TextureLeakPatches
{
    private static bool _loadingPaint;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SaveLoadHandler), "LoadPaintTargets")]
    private static void BeforeLoadingPaint() => _loadingPaint = true;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(SaveLoadHandler), "LoadPaintTargets")]
    private static void AfterLoadingPaint() => _loadingPaint = false;

    // LoadPaintTargets reads each picture into a new Texture2D, which SetupPaint copies into the
    // surface's render texture; the Texture2D is never used again.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PaintTarget), nameof(PaintTarget.SetupPaint))]
    private static void AfterSetup(PaintTarget __instance, Texture2D tex)
    {
        PaintTextures.Own(__instance);
        if (_loadingPaint && tex != null)
            Object.Destroy(tex);
    }

    // ClearPaint sets splatTexPick to null without destroying it.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PaintTarget), nameof(PaintTarget.ClearPaint))]
    private static void BeforeClear(PaintTarget __instance)
    {
        if (__instance.setupComplete && __instance.splatTexPick != null)
            Object.Destroy(__instance.splatTexPick);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PaintTarget), "UpdatePickColors")]
    private static void AfterPick(PaintTarget paintTarget) =>
        SceneOwned.Own(paintTarget.gameObject.scene, paintTarget.splatTexPick);

    // Used by SaveSplat and ClearWaterChannel (every wet surface, every two minutes); both use the
    // copy right away.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PaintTarget), nameof(PaintTarget.toTexture2D))]
    private static bool PaintCopy(RenderTexture rTex, ref Texture2D __result)
    {
        __result = ScratchTextures.CopyOf(rTex);
        return false;
    }

    // Every photo; encoded to PNG right away.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PhotoCamera), "toTexture2D")]
    private static bool PhotoCopy(RenderTexture rTex, ref Texture2D __result)
    {
        __result = ScratchTextures.CopyOf(rTex);
        return false;
    }

    /// <summary>
    /// Door.OnTriggerEnter gives the peephole camera on the player's side a new 256x256 render
    /// texture every time the player walks up to a door, and drops the old one. This is the same
    /// code, keeping one texture per camera.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Door), "OnTriggerEnter")]
    private static bool DoorApproached(Door __instance, Collider other)
    {
        if (other.gameObject != HouseManager._.player || __instance.keyHoleRTCams == null)
            return false;
        int side = Vector3.Dot(other.transform.position - __instance.transform.position, __instance.transform.up) > 0f ? 0 : 1;
        var camera = __instance.keyHoleRTCams[side];
        var view = camera.targetTexture;
        if (view == null || view.name != PeepholeView)
        {
            view = new RenderTexture(256, 256, 10) { name = PeepholeView };
            view.Create();
            SceneOwned.Own(__instance.gameObject.scene, view);
        }
        camera.targetTexture = view;
        __instance.keyHoleRTImages[side].texture = view;
        camera.gameObject.SetActive(true);
        return false;
    }

    private const string PeepholeView = "PeepholeView";
}
