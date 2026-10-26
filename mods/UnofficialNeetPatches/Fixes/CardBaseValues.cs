using System.Collections.Generic;
using NeetModKit.Cards;

namespace UnofficialNeetPatches.Fixes;

/// <summary>
/// Fixes promoted cards falling back to their starting stats after loading a save: the game
/// saves level and income but not the base values promotion doubles.
/// </summary>
internal static class CardBaseValues
{
    public static List<float[]> Capture()
    {
        var list = new List<float[]>();
        foreach (var card in CardGame.Cards)
            list.Add(new[] { card.initIncome, card.initPrice, card.initExpToLevel });
        return list;
    }

    public static void Restore(List<float[]> saved)
    {
        if (saved == null)
            return;
        var cards = CardGame.Cards;
        for (int i = 0; i < cards.Length && i < saved.Count; i++)
        {
            var v = saved[i];
            if (v == null || v.Length < 3)
                continue;
            cards[i].initIncome = v[0];
            cards[i].initPrice = v[1];
            cards[i].initExpToLevel = v[2];
        }
    }
}
