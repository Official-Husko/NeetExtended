using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.FrameRateSettingsPatch"/>.</summary>
internal sealed class FrameRateSettingsFix : ModFeature
{
    public const string FeatureId = "FrameRateSettings";

    public override string Id => FeatureId;
    public override string Name => "Frame rate and vsync settings stick";
    public override string Description =>
        "The settings menu lets you pick a frame rate and vsync, but the game switches back to 120 fps without vsync " +
        "every time the house loads or a character shows up. With this, your choice stays.";
}
