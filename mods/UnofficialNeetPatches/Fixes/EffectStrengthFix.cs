using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.EffectStrengthPatch"/>.</summary>
internal sealed class EffectStrengthFix : ModFeature
{
    public const string FeatureId = "EffectStrength";

    public override string Id => FeatureId;
    public override string Name => "Effects as strong at any frame rate";
    public override string Description =>
        "Lasting effects like diarrhea or hormone pills change your stats a little every frame, so they're four times as " +
        "strong at 120 fps as at 30. With this they work per second instead, matched to the game's own 120 fps, so nothing " +
        "changes there.";
}
