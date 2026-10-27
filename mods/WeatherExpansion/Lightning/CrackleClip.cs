using UnityEngine;

namespace WeatherExpansion.Lightning;

/// <summary>
/// A short burst of static with pops in it, made once at run time. The TV, the radio and the phones all
/// play it when lightning hits. Made when first asked for and again if it was destroyed (a hot reload
/// or a house load can take it), and destroyed when the mod unloads.
/// </summary>
internal static class CrackleClip
{
    private const int Rate = 22050;
    private const float Seconds = 1.4f;

    private static AudioClip _clip;

    public static float Length => Seconds;

    public static AudioClip Get()
    {
        if (_clip != null)
            return _clip;
        int count = (int)(Rate * Seconds);
        var data = new float[count];
        var rng = new System.Random(1337);
        float low = 0f;
        float pop = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            // A little low-pass so it hisses instead of buzzing.
            low += (white - low) * 0.55f;
            // Pops: rare and sharp at first, thinning out with the tail.
            if (rng.NextDouble() < 0.0016 * (1f - t))
                pop = (float)(0.6 + rng.NextDouble() * 0.4) * (rng.Next(2) == 0 ? -1f : 1f);
            pop *= 0.985f;
            float envelope = t < 0.02f ? t / 0.02f : Mathf.Pow(1f - (t - 0.02f) / 0.98f, 1.6f);
            data[i] = Mathf.Clamp((low * 0.55f + pop) * envelope, -1f, 1f);
        }
        _clip = AudioClip.Create("WeatherExpansion crackle", count, 1, Rate, false);
        _clip.hideFlags = HideFlags.HideAndDontSave;
        _clip.SetData(data, 0);
        return _clip;
    }

    public static void Release()
    {
        if (_clip != null)
            Object.Destroy(_clip);
        _clip = null;
    }
}
