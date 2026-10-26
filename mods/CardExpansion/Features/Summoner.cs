using System.Collections.Generic;
using NeetModKit.Cards;
using UnityEngine;

namespace CardExpansion.Features;

/// <summary>
/// Spend crystals to summon a random unlocked card: it pays out a few bars of its income, or on a
/// shiny roll gets five free levels.
/// </summary>
internal sealed class Summoner
{
    private const int ShinyLevels = 5;
    private readonly List<AnimeCard> _unlocked = new List<AnimeCard>();

    public string Summon(CardExpansionConfig config, CardState state)
    {
        if (CardGame.Crystals < config.SummonCost)
            return $"Need {config.SummonCost}* to summon";

        _unlocked.Clear();
        foreach (var card in CardGame.Cards)
        {
            if (!card.Locked)
                _unlocked.Add(card);
        }
        if (_unlocked.Count == 0)
            return "Unlock a card first";

        CardGame.Crystals -= config.SummonCost;
        state.Summons++;

        var picked = _unlocked[Random.Range(0, _unlocked.Count)];
        string name = picked.girlName != null ? picked.girlName.text : "A card";
        picked.ClickedCard(); // Same bounce and sound as clicking it.

        if (Random.value < config.ShinyChance && picked.Level < CardGame.LevelCap(picked))
        {
            int before = picked.Level;
            CardGame.SetLevel(picked, before + ShinyLevels);
            state.Shinies++;
            return $"SHINY {name}! +{picked.Level - before} levels";
        }

        float payout = picked.Income * config.SummonPayoutBars;
        CardGame.Crystals += payout;
        return $"{name} paid {Mathf.Round(payout)}*";
    }
}
