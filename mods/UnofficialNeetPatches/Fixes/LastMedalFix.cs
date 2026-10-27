using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.LastMedalPatch"/>.</summary>
internal sealed class LastMedalFix : ModFeature
{
    public const string FeatureId = "LastMedal";

    public override string Id => FeatureId;
    public override string Name => "Every achievement has a spot on the medal shelf";
    public override string Description =>
        "The medal shelf in the shed makes room for every achievement but the last one, so its medals never show up. With this, it gets its spot.";
}
