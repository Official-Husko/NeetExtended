using UnityEngine;

namespace WeatherExpansion.Fog;

/// <summary>A round, soft-edged white blob with an alpha falloff, the picture a puff of mist is drawn with.</summary>
internal static class SoftTexture
{
    public static Texture2D Create(int size = 64)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "WeatherExpansion soft blob",
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / (half + 0.5f), dy = (y - half) / (half + 0.5f);
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = a * a * (3f - 2f * a);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
