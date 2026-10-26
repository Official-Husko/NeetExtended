using NeetLoader;
using NeetModKit.Assets;
using NeetModKit.Cards;
using UnityEngine;

namespace CardExpansion.Cards;

/// <summary>
/// Makes Halloween versions of the game's card pictures: a duotone from deep purple through
/// pumpkin orange to candle-light, keyed on brightness.
/// </summary>
internal static class SpookyArt
{
    private static readonly Color Shadow = new Color(0.09f, 0.02f, 0.14f);
    private static readonly Color Pumpkin = new Color(1f, 0.45f, 0.08f);
    private static readonly Color Candle = new Color(1f, 0.95f, 0.78f);

    /// <summary>Recolours the picture of built-in card <paramref name="sourceIndex"/>.</summary>
    public static Texture2D FromCard(NeetMod owner, int sourceIndex)
    {
        var cards = CardGame.Cards;
        if (cards.Length == 0)
            return null;
        var picture = cards[sourceIndex % cards.Length].picture;
        if (picture == null || picture.mainTexture == null)
            return null;

        var tex = ModAssets.ReadableCopy(owner, picture.mainTexture);
        var pixels = tex.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];
            float luma = (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f;
            Color c = luma < 0.5f
                ? Color.Lerp(Shadow, Pumpkin, luma * 2f)
                : Color.Lerp(Pumpkin, Candle, (luma - 0.5f) * 2f);
            pixels[i] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), p.a);
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
