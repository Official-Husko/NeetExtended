using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.SpawnersAfterLoadPatch"/>.</summary>
internal sealed class SpawnersAfterLoadFix : ModFeature
{
    public const string FeatureId = "SpawnersAfterLoad";

    public override string Id => FeatureId;
    public override string Name => "Spawners keep working after loading";
    public override string Description =>
        "After a save loaded, everything that spawns by timer stopped for the rest of the session: loose change, dust, sink water, " +
        "toilet flushes, blood and paint sprays. They now start again two seconds after the load.";
}
