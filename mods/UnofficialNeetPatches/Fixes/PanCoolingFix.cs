using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.PanCoolingPatch"/>.</summary>
internal sealed class PanCoolingFix : ModFeature
{
    public const string FeatureId = "PanCooling";

    public override string Id => FeatureId;
    public override string Name => "Pans cool down after food burns in them";
    public override string Description =>
        "A frying pan that had food burn in it stayed hot for good, even long after the stove was off. " +
        "It now cools down like any other pan.";
}
