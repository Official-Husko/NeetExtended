using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.SkeletonPantsPatch"/>.</summary>
internal sealed class SkeletonPantsFix : ModFeature
{
    public const string FeatureId = "SkeletonPants";

    public override string Id => FeatureId;
    public override string Name => "Skeletons don't ruin the black pants";
    public override string Description =>
        "A skeleton that had to crap threw an error and dirtied the black pants in the game's item list, so every " +
        "pair you bought or found after that was dirty and sticky until you restarted. Skeletons now just drop a turd.";
}
