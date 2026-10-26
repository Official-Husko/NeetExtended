using System.Linq;
using DevBridge.Server;
using NeetModKit.Cards;
using Newtonsoft.Json.Linq;

namespace DevBridge.Commands;

internal static class CardCommands
{
    [BridgeCommand("cards.get", "Anime card game: crystals, unlocked count and every card.", Params = new[] { "all?: include locked cards, default true" })]
    private static object Get(JObject p)
    {
        bool all = p.Bool("all", true);
        var cards = CardGame.Cards
            .Where(c => all || !c.Locked)
            .Select(c => new JObject
            {
                ["id"] = c.cardId,
                ["name"] = c.girlName != null ? c.girlName.text : null,
                ["object"] = c.name,
                ["locked"] = c.Locked,
                ["level"] = c.Level,
                ["medal"] = c.medalLvl,
                ["income"] = c.Income,
                ["price"] = c.purchasePrice,
                ["exp"] = System.Math.Round(c.exp, 2),
                ["expToLevel"] = c.expToLevel,
            });
        return new JObject
        {
            ["crystals"] = CardGame.Crystals,
            ["maxUnlocked"] = CardGame.MaxUnlocked,
            ["extraCards"] = new JArray(CardGame.ExtraCards.Select(c => c.Name).ToArray()),
            ["cards"] = new JArray(cards.ToArray()),
        };
    }

    [BridgeCommand("cards.crystals", "Set the crystal count.", Params = new[] { "value" })]
    private static object SetCrystals(JObject p)
    {
        CardGame.Crystals = p.Float("value");
        return CardGame.Crystals;
    }
}
