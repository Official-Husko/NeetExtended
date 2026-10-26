using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.SaveCrashPatch"/>.</summary>
internal sealed class SaveCrashesFix : ModFeature
{
    public const string FeatureId = "SaveCrashes";

    public override string Id => FeatureId;
    public override string Name => "One broken thing doesn't stop saving";
    public override string Description =>
        "If a single object in the house couldn't be saved, the game gave up on the whole save, and nothing got saved " +
        "again until a restart (sleeping and dying save too). Now that object is left out, the rest is saved, and the " +
        "problem goes into errors.log.";
}
