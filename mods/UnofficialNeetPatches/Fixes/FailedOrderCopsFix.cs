using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.FailedOrderCopsPatch"/>.</summary>
internal sealed class FailedOrderCopsFix : ModFeature
{
    public const string FeatureId = "FailedOrderCops";

    public override string Id => FeatureId;
    public override string Name => "Cops only come for orders that went through";
    public override string Description =>
        "Ordering from the black or occult market has a chance to bring the cops. The game rolls that chance before checking whether the order goes through, so the demo's blocked orders and orders you can't afford could still bring a cop van. With this, only real orders can.";
}
