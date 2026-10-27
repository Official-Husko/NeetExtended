using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.DeathDropsPatches"/>.</summary>
internal sealed class DeathDropsFix : ModFeature
{
    public const string FeatureId = "DeathDrops";

    public override string Id => FeatureId;
    public override string Name => "Your stuff stays where you died";
    public override string Description =>
        "When you died, the clothes you had on were gone for good: the game left them hidden under the house, and every " +
        "save kept them there. Being blown up or turned into a skeleton could take what you held or wore on your face too. " +
        "Now everything you had falls where you died, so you can go back for it.";
}
