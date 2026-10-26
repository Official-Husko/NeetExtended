using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.GhostSavePatches"/>.</summary>
internal sealed class GhostSavesFix : ModFeature
{
    public const string FeatureId = "GhostSaves";

    public override string Id => FeatureId;
    public override string Name => "Things that are gone stay gone";
    public override string Description =>
        "Some things keep their place in the save after they're gone. A fire you put out gets saved as a fire again, " +
        "and a destroyed fuse made every save after it fail. This leaves things that no longer exist out of the save.";
}
