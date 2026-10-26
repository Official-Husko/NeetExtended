using NeetModKit.Features;

namespace CardExpansion.Features;

/// <summary>Switches RedeemRatePatch on and off.</summary>
internal sealed class BetterExchangeFeature : ModFeature
{
    public const string FeatureId = "BetterExchange";

    public override string Id => FeatureId;
    public override string Name => "Better crystal exchange";
    public override string Description => "Convert pays the amount set in Settings per 10K crystals ($5 by default) instead of $3.";
}
