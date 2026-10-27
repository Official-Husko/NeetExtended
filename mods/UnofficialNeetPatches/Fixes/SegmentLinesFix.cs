using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.SegmentLinesPatch"/>.</summary>
internal sealed class SegmentLinesFix : ModFeature
{
    public const string FeatureId = "SegmentLines";

    public override string Id => FeatureId;
    public override string Name => "Grandpa doesn't get stuck after the thief";
    public override string Description =>
        "When the bad thief's timer runs out, the game tells Grandpa to follow a routine that doesn't exist, which errors and can keep erroring every time he arrives somewhere. With this, he follows the closest routine he has, or keeps the one he's on.";
}
