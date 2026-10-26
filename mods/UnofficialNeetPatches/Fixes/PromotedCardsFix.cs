using System.Collections.Generic;
using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>
/// Promoting a card in the computer's card game doubles its base values, but the game doesn't save
/// them, so promoted cards fall back to their starting stats after a load. The base values are kept
/// in this mod's save data instead (see <see cref="CardBaseValues"/>).
/// </summary>
internal sealed class PromotedCardsFix : ModFeature
{
    public override string Id => "PromotedCards";
    public override string Name => "Keep promoted card stats";
    public override string Description => "Promoted cards in the computer's card game lose their doubled earnings after loading a save. This keeps them.";
    public override string AppliesWhen => "on the next load";

    /// <summary>What goes into the save. Always captured, so switching the fix on later still has data.</summary>
    public sealed class SaveData
    {
        /// <summary>Income, price and exp per card, in card order.</summary>
        public List<float[]> CardBases = new List<float[]>();
    }

    public SaveData Save() => new SaveData { CardBases = CardBaseValues.Capture() };

    public void Load(SaveData data)
    {
        if (Enabled)
            CardBaseValues.Restore(data.CardBases);
    }
}
