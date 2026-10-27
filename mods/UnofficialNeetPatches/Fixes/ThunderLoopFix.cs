using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.ThunderLoopPatch"/>.</summary>
internal sealed class ThunderLoopFix : ModFeature
{
    public const string FeatureId = "ThunderLoop";

    public override string Id => FeatureId;
    public override string Name => "Thunder keeps going";
    public override string Description =>
        "Once the story set off its own lightning, there was never any thunder again until the game was restarted. " +
        "Thunder now carries on at random as it did before.";
}
