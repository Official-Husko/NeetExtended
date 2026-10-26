using CardExpansion.Cards;
using NeetModKit.Features;

namespace CardExpansion.Features;

internal sealed class SpookyCardsFeature : ModFeature
{
    public override string Id => "SpookyCards";
    public override string Name => "Spooky card set";
    public override string Description => "Twelve Halloween cards after the game's 66, with recoloured art.";
    public override string AppliesWhen => "after the house reloads";

    protected override void OnEnable() => SpookyCollection.Register(this, Owner);
}
