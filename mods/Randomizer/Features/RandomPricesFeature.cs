using NeetModKit.Features;
using Randomizer.Parts;

namespace Randomizer.Features;

internal sealed class RandomPricesFeature : ModFeature
{
    public override string Id => "RandomPrices";
    public override string Name => "Random prices";
    public override string Description =>
        "Store prices move up or down from the game's, differently every game (how far is in the settings). Coins and money keep their value.";

    protected override void OnEnable()
    {
        Prices.Enabled = true; Prices.Apply();
    }

    protected override void OnDisable()
    {
        Prices.Enabled = false; Prices.Apply();
    }
}
