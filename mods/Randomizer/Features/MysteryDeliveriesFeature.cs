using NeetModKit.Features;
using Randomizer.Parts;

namespace Randomizer.Features;

internal sealed class MysteryDeliveriesFeature : ModFeature
{
    public const string FeatureId = "MysteryDeliveries";

    public override string Id => FeatureId;
    public override string Name => "Mystery deliveries";
    public override string Description =>
        "Sometimes an order brings something else from the same store, of a similar price, for what you paid. How often is in the settings.";

    protected override void OnEnable()
    {
        MysteryOrders.Enabled = true;
    }

    protected override void OnDisable()
    {
        MysteryOrders.Enabled = false;
    }
}
