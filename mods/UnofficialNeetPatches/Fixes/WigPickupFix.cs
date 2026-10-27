using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.WigPickupPatch"/>.</summary>
internal sealed class WigPickupFix : ModFeature
{
    public const string FeatureId = "WigPickup";

    public override string Id => FeatureId;
    public override string Name => "Picking up the thief's beanie works";
    public override string Description =>
        "Picking up the beanie of the thief (or any wig with a round collider) threw an error and left your hand holding it half-way, " +
        "with errors every physics step after. The wig now finds its own collider whatever its shape.";
}
