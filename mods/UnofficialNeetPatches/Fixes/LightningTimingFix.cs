using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.LightningTimingPatch"/>.</summary>
internal sealed class LightningTimingFix : ModFeature
{
    public const string FeatureId = "LightningTiming";

    public override string Id => FeatureId;
    public override string Name => "Lightning looks the same at any frame rate";
    public override string Description =>
        "The flash of lightning and the lamps flickering with it were tied to the frame rate: at 144 fps they were over in less than half the time they take at 60. " +
        "They now last as long as they do at 60 fps, whatever your frame rate is.";
}
