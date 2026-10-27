using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.MoonRestorePatch"/>.</summary>
internal sealed class MoonRestoreFix : ModFeature
{
    public const string FeatureId = "MoonRestore";

    public override string Id => FeatureId;
    public override string Name => "The moon comes back after lightning";
    public override string Description =>
        "After the first flash of lightning the moonlight was switched off for good, so the night got darker than the game meant it to be. " +
        "The moon goes back to its usual light once the flash is over. (Its shadows come back with it.)";
}
