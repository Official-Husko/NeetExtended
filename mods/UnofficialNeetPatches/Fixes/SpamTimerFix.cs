using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.SpamTimerPatch"/>.</summary>
internal sealed class SpamTimerFix : ModFeature
{
    public const string FeatureId = "SpamTimer";

    public override string Id => FeatureId;
    public override string Name => "Spam keeps its timer through saves";
    public override string Description =>
        "Spam emails come every few minutes, but after loading a save the next one always arrives 15 seconds in. " +
        "With this, it arrives when it was due.";
}
