using NeetLoader;
using NeetLoader.Runtime;
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

    /// <summary>Adds the cards for as long as <paramref name="feature"/> lives; art belongs to the mod.</summary>
    public static void Register(ITracker feature, NeetMod owner)
    {
        for (int i = 0; i < Names.Length; i++)
        {
            int source = 5 + i * 5; // Spread the recoloured art across the original set.
            CardGame.AddCard(feature, new CardDefinition
            {
                Name = Names[i],
                ArtProvider = () => SpookyArt.FromCard(owner, source),
            });
        }
    }
}
