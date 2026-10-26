using NeetModKit.Cards;
using UnityEngine;

namespace CardExpansion.Features;

/// <summary>Clicks every unlocked card on a timer once bought. Works with the site closed too.</summary>
internal sealed class AutoClicker
{
    private AnimeCard[] _cards = new AnimeCard[0];
    private float _timer;

    /// <summary>Card objects are fixed per house load, so look them up once instead of every tick.</summary>
    public void RefreshCards() => _cards = CardGame.Cards;

    public string Buy(CardExpansionConfig config, CardState state)
    {
        if (state.AutoClickerOwned)
        {
            state.AutoClickerOn = !state.AutoClickerOn;
            return state.AutoClickerOn ? "Auto-clicker on" : "Auto-clicker off";
        }
        if (CardGame.Crystals < config.AutoClickerCost)
            return $"Need {config.AutoClickerCost}* for the auto-clicker";

        CardGame.Crystals -= config.AutoClickerCost;
        state.AutoClickerOwned = true;
        state.AutoClickerOn = true;
        return "Auto-clicker bought!";
    }

    public void Tick(CardExpansionConfig config, CardState state)
    {
        if (!state.AutoClickerOwned || !state.AutoClickerOn || _cards.Length == 0)
            return;

        float interval = 1f / Mathf.Max(0.1f, config.AutoClicksPerSecond);
        _timer += Time.deltaTime;
        if (_timer < interval)
            return;
        _timer = Mathf.Min(_timer - interval, interval); // No burst after a long pause.

        for (int i = 0; i < _cards.Length; i++)
        {
            var card = _cards[i];
            if (card != null && !card.Locked)
                card.Exp += CardGame.ClickExp(card);
        }
    }
}
