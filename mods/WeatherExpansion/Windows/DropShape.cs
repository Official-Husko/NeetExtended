using UnityEngine;

namespace WeatherExpansion.Windows;

/// <summary>
/// The shape of a running drop, taken from the game's own water: <c>WaterDropParticle</c>, the teardrop
/// picture the drip effect (<c>DripEffect</c>, material <c>Drop</c>) draws. Its alpha is read once (through a
/// render texture, since the game's copy isn't readable by scripts), blurred into a rounded height and
/// turned into a small table of normals, turned so the point trails upward behind a drop that slides down.
/// <see cref="DropField"/> stamps it for every drop. When the texture isn't in the game, there is no shape
/// and the field draws plain round drops.
/// </summary>
internal sealed class DropShape
{
    public const string TextureName = "WaterDropParticle";
    private const int Source = 128;
    private const int Size = 48;
    private const float Edge = 0.08f;

    private readonly uint[] _packed = new uint[Size * Size]; // the normal map pixel, 0 outside the drop

    /// <summary>How wide the picture is, in drop radii: the drop's body is a little over half of it.</summary>
    public const float Span = 3.6f;

    /// <summary>Reads the game's drop picture and builds the table. Null when the game has no such texture.</summary>
    public static DropShape Create(float tilt)
    {
        Texture2D source = null;
        foreach (var texture in Resources.FindObjectsOfTypeAll<Texture2D>())
        {
            if (texture != null && texture.name == TextureName)
            {
                source = texture;
                break;
            }
        }
        if (source == null)
            return null;
        var alpha = ReadAlpha(source);
        return alpha == null ? null : new DropShape(alpha, tilt);
    }

    private DropShape(float[] alpha, float tilt)
    {
        // A rounded height: the silhouette blurred a few times, so the drop is a dome with its tail thinning out.
        var mask = new float[Source * Source];
        for (int i = 0; i < mask.Length; i++)
            mask[i] = alpha[i] > Edge ? 1f : 0f;
        var height = Blur(Blur(Blur(mask, 6), 6), 6);

        // Sample into the table, turned: the picture's point (+x) becomes +y of the glass.
        var hx = new float[Size * Size];
        var hy = new float[Size * Size];
        float strongest = 1e-6f;
        for (int j = 0; j < Size; j++)
        {
            for (int i = 0; i < Size; i++)
            {
                float fx = (i + 0.5f) / Size - 0.5f, fy = (j + 0.5f) / Size - 0.5f;
                float u = 0.5f + fy, v = 0.5f - fx;
                int k = j * Size + i;
                _packed[k] = Sample(height, u, v) > 0.03f ? 1u : 0u; // a little beyond the silhouette, where the slope fades out
                float du = 1f / Source;
                float gu = (Sample(height, u + du, v) - Sample(height, u - du, v)) / (2f * du);
                float gv = (Sample(height, u, v + du) - Sample(height, u, v - du)) / (2f * du);
                // d/dfx = -d/dv, d/dfy = d/du
                hx[k] = -gv;
                hy[k] = gu;
                strongest = Mathf.Max(strongest, Mathf.Sqrt(hx[k] * hx[k] + hy[k] * hy[k]));
            }
        }
        for (int k = 0; k < _packed.Length; k++)
        {
            if (_packed[k] != 0u)
                _packed[k] = Pack(-hx[k] / strongest * tilt, -hy[k] / strongest * tilt);
        }
    }

    /// <summary>The normal map pixel at a point of the drop (fx, fy in drop radii from its centre); 0 outside the drop.</summary>
    public uint At(float fx, float fy)
    {
        float x = fx * (1f / Span) + 0.5f, y = fy * (1f / Span) + 0.5f;
        if (x < 0f || x >= 1f || y < 0f || y >= 1f)
            return 0u;
        return _packed[(int)(y * Size) * Size + (int)(x * Size)];
    }

    /// <summary>A normal as the game's shader reads it: Unity's DXT5nm layout (x in alpha, y in green), as the RGBA bytes of a little-endian uint.</summary>
    public static uint Pack(float nx, float ny)
    {
        float k = 127.5f / (float)System.Math.Sqrt(nx * nx + ny * ny + 1f);
        return 255u | ((uint)(127.5f - ny * k) << 8) | (255u << 16) | ((uint)(127.5f - nx * k) << 24);
    }

    private static float[] ReadAlpha(Texture2D source)
    {
        var target = RenderTexture.GetTemporary(Source, Source, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            var copy = new Texture2D(Source, Source, TextureFormat.RGBA32, false, true);
            copy.ReadPixels(new Rect(0, 0, Source, Source), 0, 0);
            var pixels = copy.GetPixels32();
            Object.Destroy(copy);
            var alpha = new float[Source * Source];
            for (int i = 0; i < alpha.Length; i++)
                alpha[i] = pixels[i].a / 255f;
            return alpha;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static float[] Blur(float[] input, int radius)
    {
        var temp = new float[input.Length];
        var output = new float[input.Length];
        for (int y = 0; y < Source; y++)
            for (int x = 0; x < Source; x++)
            {
                float sum = 0f;
                for (int d = -radius; d <= radius; d++)
                    sum += input[y * Source + Mathf.Clamp(x + d, 0, Source - 1)];
                temp[y * Source + x] = sum / (2 * radius + 1);
            }
        for (int y = 0; y < Source; y++)
            for (int x = 0; x < Source; x++)
            {
                float sum = 0f;
                for (int d = -radius; d <= radius; d++)
                    sum += temp[Mathf.Clamp(y + d, 0, Source - 1) * Source + x];
                output[y * Source + x] = sum / (2 * radius + 1);
            }
        return output;
    }

    private static float Sample(float[] data, float u, float v)
    {
        int x = Mathf.Clamp((int)(u * Source), 0, Source - 1);
        int y = Mathf.Clamp((int)(v * Source), 0, Source - 1);
        return data[y * Source + x];
    }
}
