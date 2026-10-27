using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Economy;

/// <summary>
/// Puts the host's numbers into one card here through the card's own setters (texts, medal, the next
/// card showing up), except experience: its setter pays out crystals, so the field and bar are set
/// directly.
/// </summary>
internal static class CardState
{
    public static void Apply(AnimeCard card, NetDataReader r)
    {
        int level = r.GetShort();
        int medal = r.GetByte();
        bool locked = r.GetBool();
        float exp = r.GetFloat();
        float expToLevel = r.GetFloat();
        float income = r.GetFloat();
        float price = r.GetFloat();
        float initIncome = r.GetFloat();
        float initPrice = r.GetFloat();
        if (card == null)
            return;

        card.expToLevel = expToLevel;
        card.purchasePrice = price;
        card.initIncome = initIncome;
        card.initPrice = initPrice;
        if (card.Locked != locked)
            card.Locked = locked;
        if (card.medalLvl != medal)
        {
            card.medalLvl = medal;
            card.SetCardMedalTexture();
        }
        if (card.Level != level)
            card.Level = level;
        if (card.promoteBtn != null && level < Mathf.Max(1, medal) * 50)
            card.promoteBtn.SetActive(false);
        if (card.Income != income)
            card.Income = income;
        card.exp = exp;
        if (card.expBar != null && expToLevel > 0f)
            card.expBar.transform.localScale = new Vector3(Mathf.Clamp01(exp / expToLevel), 1f, 1f);
        if (!locked && card.buyButton != null)
            card.buyButton.text = "Up. " + Mathf.Round(price) + "*";
    }
}
