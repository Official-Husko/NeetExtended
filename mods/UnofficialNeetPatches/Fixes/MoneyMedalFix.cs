using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.MoneyMedalPatch"/>.</summary>
internal sealed class MoneyMedalFix : ModFeature
{
    public const string FeatureId = "MoneyMedal";

    public override string Id => FeatureId;
    public override string Name => "The money platinum medal checks the right thing";
    public override string Description =>
        "The platinum goal for total money reads whether you got the platinum for the biggest single money gain instead, so it can show as done when it isn't. With this, it reads its own.";
}
