using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.TvAfterLoadPatch"/>.</summary>
internal sealed class TvAfterLoadFix : ModFeature
{
    public const string FeatureId = "TvAfterLoad";

    public override string Id => FeatureId;
    public override string Name => "The TV shows after loading";
    public override string Description =>
        "A TV that was on when the game saved came back on after loading, with its sound playing, but with a dark screen. " +
        "It shows its channel again.";
}
