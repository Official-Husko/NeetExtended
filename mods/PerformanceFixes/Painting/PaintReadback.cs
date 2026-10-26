using HarmonyLib;
using PerformanceFixes.Rendering;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace PerformanceFixes.Painting;

/// <summary>Copies a paint surface's texture from the GPU into a pixel buffer, through a reused scratch texture.</summary>
internal static class PaintReadback
{
    private static readonly AccessTools.FieldRef<PaintTarget, bool> EvenFrame =
        AccessTools.FieldRefAccess<PaintTarget, bool>("evenFrame");

    private static readonly AccessTools.FieldRef<PaintTarget, RenderTexture> SplatTexAlt =
        AccessTools.FieldRefAccess<PaintTarget, RenderTexture>("splatTexAlt");

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
        // Same sRGB-ness as the game's own copies (new Texture2D(..., ARGB32, false)), so the PNGs match.
        var scratch = ScratchTextures.Get(width, height, TextureFormat.RGBA32);

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
}
