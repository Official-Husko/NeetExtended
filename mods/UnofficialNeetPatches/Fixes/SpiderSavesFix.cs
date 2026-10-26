using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.SpiderSavePatch"/>.</summary>
internal sealed class SpiderSavesFix : ModFeature
{
    public const string FeatureId = "SpiderSaves";

    public override string Id => FeatureId;
    public override string Name => "Saving with house spiders";
    public override string Description =>
        "The game can't save while a dead house spider is lying around (or anything else is stuck to a spider): saving " +
        "stops with an error, and dying gets stuck instead of moving on. This lets those saves go through.";
}
