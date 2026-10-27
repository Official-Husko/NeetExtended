using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.RepeatingSpamPatch"/>.</summary>
internal sealed class RepeatingSpamFix : ModFeature
{
    public const string FeatureId = "RepeatingSpam";

    public override string Id => FeatureId;
    public override string Name => "Spam emails only come once";
    public override string Description =>
        "Each of the game's spam emails is meant to arrive once, but after loading a save they all come again, " +
        "so the inbox fills up with copies. With this, the game remembers which ones you already got.";
}
