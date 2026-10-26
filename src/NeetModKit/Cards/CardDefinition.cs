using UnityEngine;

namespace NeetModKit.Cards;

/// <summary>
/// An extra card for the computer's anime card game. Numbers left empty continue the game's own
/// progression (each card earns 4 more, costs 8 more and levels 4.8 slower than the one before).
/// </summary>
public sealed class CardDefinition
{
    public string Name;

    /// <summary>Card picture. Null keeps the picture of the card it was copied from.</summary>
    public Texture2D Art;

    /// <summary>
    /// Makes the picture when the card is created (inside the house), for art built from the
    /// game's own textures. Used when <see cref="Art"/> is null.
    /// </summary>
    public System.Func<Texture2D> ArtProvider;

    /// <summary>Crystals paid each time the exp bar fills, at level 0.</summary>
    public float? Income;

    /// <summary>Unlock price in crystals.</summary>
    public float? Price;

    /// <summary>Exp needed to fill the bar (exp grows by 1 per second, plus clicks).</summary>
    public float? ExpToLevel;
}
