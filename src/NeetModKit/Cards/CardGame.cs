using System;
using System.Collections.Generic;
using NeetLoader;
using NeetLoader.Runtime;
using NeetModKit.Game;

namespace NeetModKit.Cards;

/// <summary>
/// The anime card game on the in-game computer: read and change its state, and add cards.
/// Cards are collected once when the card site starts, so cards added later appear after the
/// next house load.
/// </summary>
public static class CardGame
{
    // The game's progression for card i: income 1 + 4i, price 10 + 8i, exp 5 + 4.8i.
    public static float DefaultIncome(int index) => 1f + 4f * index;
    public static float DefaultPrice(int index) => 10f + 8f * index;
    public static float DefaultExpToLevel(int index) => 5f + 4.8f * index;

    private static readonly List<CardDefinition> Extra = new List<CardDefinition>();

    public static AnimeCardGenerator Generator => GameRefs.CardGame;

    public static IReadOnlyList<CardDefinition> ExtraCards => Extra;

    /// <summary>Every card in display order (built-in first, then modded).</summary>
    public static AnimeCard[] Cards =>
        Generator != null ? Generator.GetComponentsInChildren<AnimeCard>(true) : Array.Empty<AnimeCard>();

    public static float Crystals
    {
        get => Generator != null ? Generator.Crystals : 0f;
        set => Require().Crystals = value;
    }

    /// <summary>Highest unlocked card index.</summary>
    public static int MaxUnlocked => Generator != null ? Generator.maxCardUnlocked : 0;

    /// <summary>Adds a card after the game's own. Takes effect the next time the house loads.</summary>
    public static void AddCard(ITracker owner, CardDefinition card)
    {
        if (string.IsNullOrEmpty(card.Name))
            throw new ArgumentException("A card needs a name.");
        Extra.Add(card);
        owner.Track(Disposable.Create(() => Extra.Remove(card)));
    }

    /// <summary>Highest level a card can reach at its current medal (the game's rule).</summary>
    public static int LevelCap(AnimeCard card) => Math.Max(1, card.medalLvl) * 50;

    /// <summary>
    /// Sets a card's level and updates its price and income exactly like buying levels does
    /// (price grows 7% per level, income is 1.67 x base x level). Clamped to the level cap.
    /// </summary>
    public static void SetLevel(AnimeCard card, int level)
    {
        level = Math.Max(0, Math.Min(level, LevelCap(card)));
        card.Level = level;
        card.purchasePrice = card.initPrice * (float)Math.Pow(1.07, level);
        card.Income = level == 0 ? card.initIncome : card.initIncome * 1.67f * level;
        if (card.buyButton != null)
            card.buyButton.text = "Up. " + Math.Round(card.purchasePrice) + "*";
    }

    /// <summary>Exp one manual click gives this card.</summary>
    public static float ClickExp(AnimeCard card) => 1f + card.initExpToLevel * 0.008f;

    internal static AnimeCardGenerator Require() =>
        Generator ?? throw new InvalidOperationException("The card game isn't loaded (not in the house?).");
}
