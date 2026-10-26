using System.Collections.Generic;
using HarmonyLib;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace PerformanceFixes.Painting;

/// <summary>Copies a paint surface's texture from the GPU into a pixel buffer, through one reused Texture2D per size.</summary>
internal static class PaintReadback
{
    private static readonly AccessTools.FieldRef<PaintTarget, bool> EvenFrame =
        AccessTools.FieldRefAccess<PaintTarget, bool>("evenFrame");

    private static readonly AccessTools.FieldRef<PaintTarget, RenderTexture> SplatTexAlt =
        AccessTools.FieldRefAccess<PaintTarget, RenderTexture>("splatTexAlt");

    private static readonly Dictionary<long, Texture2D> Scratch = new Dictionary<long, Texture2D>();

    /// <summary>
    /// The texture holding the surface's current paint. PaintTarget draws into its two textures in
    /// turn and flips evenFrame after each; the game's own SaveSplat always reads splatTex, which is
    /// one step behind half of the time.
    /// </summary>
    public static RenderTexture Latest(PaintTarget target) =>
        EvenFrame(target) ? SplatTexAlt(target) : target.splatTex;

    public static NativeArray<byte> Read(RenderTexture texture, out GraphicsFormat format)
    {
        int width = texture.width, height = texture.height;
        var scratch = ScratchFor(width, height);

        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        scratch.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
        RenderTexture.active = previous;

        var pixels = scratch.GetRawTextureData<byte>();
        var copy = PixelBuffers.Rent(pixels.Length);
        NativeArray<byte>.Copy(pixels, copy, pixels.Length);
        format = scratch.graphicsFormat;
        return copy;
    }

    public static void Release()
    {
        foreach (var texture in Scratch.Values)
        {
            if (texture != null)
                Object.Destroy(texture);
        }
        Scratch.Clear();
    }

    private static Texture2D ScratchFor(int width, int height)
    {
        long key = ((long)width << 32) | (uint)height;
        if (Scratch.TryGetValue(key, out var texture) && texture != null)
            return texture;
        // Same sRGB-ness as the game's own copies (new Texture2D(..., ARGB32, false)), so the PNGs match.
        texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear: false)
        {
            name = "PaintSaveScratch",
            hideFlags = HideFlags.HideAndDontSave,
        };
        Scratch[key] = texture;
        return texture;
    }
}
