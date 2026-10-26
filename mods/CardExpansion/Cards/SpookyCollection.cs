using NeetLoader;
using NeetModKit.Cards;

namespace CardExpansion.Cards;

/// <summary>
/// Twelve Halloween cards. Like the game's own cards they're named after real conditions,
/// here the ones behind vampire, werewolf and zombie legends.
/// </summary>
internal static class SpookyCollection
{
    private static readonly string[] Names =
    {
        "Porphyra", "Lycanthra", "Cotarda", "Renfielda", "Ergota", "Wendiga",
        "Capgrasse", "Tetrodia", "Hypertricha", "Necrosa", "Rabiette", "Vampyra",
    };

    public static void Register(NeetMod owner)
    {
        for (int i = 0; i < Names.Length; i++)
        {
            int source = 5 + i * 5; // Spread the recoloured art across the original set.
            CardGame.AddCard(owner, new CardDefinition
            {
                Name = Names[i],
                ArtProvider = () => SpookyArt.FromCard(owner, source),
            });
        }
    }
}
