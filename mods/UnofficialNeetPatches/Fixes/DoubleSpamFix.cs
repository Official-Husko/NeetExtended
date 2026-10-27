using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.DoubleSpamPatch"/>.</summary>
internal sealed class DoubleSpamFix : ModFeature
{
    public const string FeatureId = "DoubleSpam";

    public override string Id => FeatureId;
    public override string Name => "Spam doesn't double up after loading";
    public override string Description =>
        "After loading a save, the game runs its spam timer twice, so spam emails come twice as often as they " +
        "should. With this, only one timer runs.";
}
