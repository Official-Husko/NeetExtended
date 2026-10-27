using PerformanceFixes.Rendering;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace PerformanceFixes.Painting;

/// <summary>Copies a paint surface's texture from the GPU into a pixel buffer, through a reused scratch texture.</summary>
internal static class PaintReadback
{
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
