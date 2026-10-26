using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.StuckSlowdownPatch"/>.</summary>
internal sealed class StuckSlowdownsFix : ModFeature
{
    public const string FeatureId = "StuckSlowdowns";

    public override string Id => FeatureId;
    public override string Name => "Crouch and weight slowdowns don't stick";
    public override string Description =>
        "Save while crouching (or heavy) and the slowdown is saved too, but after loading nothing ever takes it off: " +
        "you walk at 80% speed for good. This drops those from the save; standing, crouching and weight add them again as usual.";
}
