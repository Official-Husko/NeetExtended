using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NeetModKit.Game;

/// <summary>
/// The game's paint surfaces (blood, dirt, water...). PaintTarget draws into two textures in turn,
/// so "the current paint" is not always the texture the game's own saving reads.
/// </summary>
public static class PaintSurfaces
{
    private static readonly AccessTools.FieldRef<PaintTarget, bool> EvenFrame =
        AccessTools.FieldRefAccess<PaintTarget, bool>("evenFrame");

    private static readonly AccessTools.FieldRef<PaintTarget, RenderTexture> SplatTexAlt =
        AccessTools.FieldRefAccess<PaintTarget, RenderTexture>("splatTexAlt");

    /// <summary>
    /// The texture holding the surface's current paint. It draws into splatTex and splatTexAlt
    /// in turn and flips evenFrame after each; the game's own SaveSplat always reads splatTex,
    /// which is one step behind half of the time.
    /// </summary>
    public static RenderTexture Latest(PaintTarget target) =>
        EvenFrame(target) ? SplatTexAlt(target) : target.splatTex;

    /// <summary>
    /// PNG bytes of every painted surface in the house, by PaintTarget.saveId, the same pictures the
    /// game writes on a save. Main thread; reads each texture back from the GPU.
    /// </summary>
    public static Dictionary<int, byte[]> CapturePng()
    {
        var result = new Dictionary<int, byte[]>();
        var house = HouseManager._;
        if (house == null)
            return result;

        var scratch = new Dictionary<int, Texture2D>();
        var previous = RenderTexture.active;
        try
        {
            foreach (var target in house.paintTargets)
            {
                if (target == null || !target.setupComplete)
                    continue;
                var texture = Latest(target);
                // The game destroys these when it clears paint for a load.
                if (texture == null)
                    continue;
                result[target.saveId] = ReadPng(texture, scratch);
            }
        }
        finally
        {
            RenderTexture.active = previous;
            foreach (var copy in scratch.Values)
                Object.Destroy(copy);
        }
        return result;
    }

    private static byte[] ReadPng(RenderTexture source, Dictionary<int, Texture2D> scratch)
    {
        int key = source.width << 16 | source.height;
        if (!scratch.TryGetValue(key, out var copy))
            scratch[key] = copy = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false);

        RenderTexture.active = source;
        copy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
        return copy.EncodeToPNG();
    }
}
