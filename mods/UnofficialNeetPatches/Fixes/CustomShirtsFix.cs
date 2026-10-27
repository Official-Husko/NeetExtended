using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.CustomShirtsPatch"/>.</summary>
internal sealed class CustomShirtsFix : ModFeature
{
    public const string FeatureId = "CustomShirts";

    public override string Id => FeatureId;
    public override string Name => "Custom shirts show when you wear them";
    public override string Description =>
        "A shirt from the custom shop showed your picture while you held it, but once you put it on it was plain " +
        "until you saved and loaded. It now shows the picture right away.";
}
