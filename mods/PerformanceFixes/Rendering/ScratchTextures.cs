using System.Collections.Generic;
using UnityEngine;

namespace PerformanceFixes.Rendering;

/// <summary>
/// One reusable Texture2D per size and format, for code that copies a render texture to the CPU,
/// uses the copy right away and drops it. The game makes a new one each time and never destroys it.
/// Main thread only.
/// </summary>
internal static class ScratchTextures
{
    private static readonly Dictionary<(int, int, TextureFormat, bool), Texture2D> Textures =
        new Dictionary<(int, int, TextureFormat, bool), Texture2D>();

    public static Texture2D Get(int width, int height, TextureFormat format, bool linear = false)
    {
        var key = (width, height, format, linear);
        if (Textures.TryGetValue(key, out var texture) && texture != null)
            return texture;
        texture = new Texture2D(width, height, format, mipChain: false, linear)
        {
            name = "Scratch",
            hideFlags = HideFlags.HideAndDontSave,
        };
        Textures[key] = texture;
        return texture;
    }

    /// <summary>Copies a render texture into a scratch texture, like the game's toTexture2D methods but without the new texture.</summary>
    public static Texture2D CopyOf(RenderTexture source)
    {
        var texture = Get(source.width, source.height, TextureFormat.ARGB32);
        RenderTexture.active = source;
        texture.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
        texture.Apply();
        return texture;
    }

    public static void Release()
    {
        foreach (var texture in Textures.Values)
        {
            if (texture != null)
                Object.Destroy(texture);
        }
        Textures.Clear();
    }
}
