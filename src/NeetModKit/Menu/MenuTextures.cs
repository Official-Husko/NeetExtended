using UnityEngine;

namespace NeetModKit.Menu;

/// <summary>Small generated textures for the menu skin, so it needs no image files.</summary>
internal static class MenuTextures
{
    public static Texture2D Solid(Color color) => Box(1, color, color, 0);

    /// <summary>A square with a 1px border, used with GUIStyle.border = 1 so it stretches cleanly.</summary>
    public static Texture2D Bordered(Color fill, Color border, int size = 8) => Box(size, fill, border, 1);

    /// <summary>Filled box with a tick drawn in <paramref name="tick"/>.</summary>
    public static Texture2D Check(Color fill, Color tick, int size = 18)
    {
        var tex = Box(size, fill, fill, 0);
        // Two strokes: short one down-right, long one up-right. Texture y goes up.
        DrawLine(tex, 4, 9, 7, 5, tick);
        DrawLine(tex, 7, 5, 13, 13, tick);
        tex.Apply();
        return tex;
    }

    private static Texture2D Box(int size, Color fill, Color border, int borderWidth)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool edge = x < borderWidth || y < borderWidth || x >= size - borderWidth || y >= size - borderWidth;
                pixels[y * size + x] = edge ? border : fill;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color color)
    {
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 2;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            // Two pixels thick so it reads at small sizes.
            tex.SetPixel(x, y, color);
            tex.SetPixel(x, y + 1, color);
        }
    }
}
