using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.HiddenPickupsPatch"/>.</summary>
internal sealed class HiddenPickupsFix : ModFeature
{
    public const string FeatureId = "HiddenPickups";

    public override string Id => FeatureId;
    public override string Name => "Characters leave hidden items alone";
    public override string Description =>
        "Characters could pick up things that belong to objects that are switched off, like the beanie of the thief before he arrives. " +
        "With this they skip them.";
}
