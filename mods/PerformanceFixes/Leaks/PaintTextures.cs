using HarmonyLib;
using UnityEngine;

namespace PerformanceFixes.Leaks;

/// <summary>
/// Registers what a paint surface creates for itself (two colour and two position render textures,
/// two materials, the instanced materials of its renderer and the colour-picking texture) with
/// <see cref="SceneOwned"/>, so they go when the surface's scene does.
/// </summary>
internal static class PaintTextures
{
    private static readonly AccessTools.FieldRef<PaintTarget, RenderTexture> SplatTexAlt =
        AccessTools.FieldRefAccess<PaintTarget, RenderTexture>("splatTexAlt");
    private static readonly AccessTools.FieldRef<PaintTarget, RenderTexture> WorldPosTex =
        AccessTools.FieldRefAccess<PaintTarget, RenderTexture>("worldPosTex");
    private static readonly AccessTools.FieldRef<PaintTarget, RenderTexture> WorldPosTexTemp =
        AccessTools.FieldRefAccess<PaintTarget, RenderTexture>("worldPosTexTemp");
    private static readonly AccessTools.FieldRef<PaintTarget, Material> PaintBlitMaterial =
        AccessTools.FieldRefAccess<PaintTarget, Material>("paintBlitMaterial");
    private static readonly AccessTools.FieldRef<PaintTarget, Material> WorldPosMaterial =
        AccessTools.FieldRefAccess<PaintTarget, Material>("worldPosMaterial");
    private static readonly AccessTools.FieldRef<PaintTarget, Renderer> PaintRenderer =
        AccessTools.FieldRefAccess<PaintTarget, Renderer>("paintRenderer");

    /// <summary>Call after SetupPaint, which creates the textures and materials.</summary>
    public static void Own(PaintTarget target)
    {
        var scene = target.gameObject.scene;
        SceneOwned.Own(scene, target.splatTex);
        SceneOwned.Own(scene, SplatTexAlt(target));
        SceneOwned.Own(scene, WorldPosTex(target));
        SceneOwned.Own(scene, WorldPosTexTemp(target));
        SceneOwned.Own(scene, PaintBlitMaterial(target));
        SceneOwned.Own(scene, WorldPosMaterial(target));
        SceneOwned.Own(scene, target.splatTexPick);

        // SetupPaint reads renderer.materials, which gives the renderer its own copies ("... (Instance)").
        var renderer = PaintRenderer(target);
        if (renderer == null)
            return;
        foreach (var material in renderer.sharedMaterials)
        {
            if (material != null && material.name.EndsWith("(Instance)"))
                SceneOwned.Own(scene, material);
        }
    }

    /// <summary>For surfaces set up before this was switched on.</summary>
    public static void OwnExisting()
    {
        foreach (var target in Object.FindObjectsOfType<PaintTarget>())
        {
            if (target.setupComplete)
                Own(target);
        }
    }
}
