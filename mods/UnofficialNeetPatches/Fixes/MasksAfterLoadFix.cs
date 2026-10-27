using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.MasksAfterLoadPatch"/>.</summary>
internal sealed class MasksAfterLoadFix : ModFeature
{
    public const string FeatureId = "MasksAfterLoad";

    public override string Id => FeatureId;
    public override string Name => "Every mask comes back after loading";
    public override string Description =>
        "Wearing a hat and a cigarette (or glasses) and loading the save only put the first one back on; the rest were left " +
        "hanging where your head had been. All of them go back on now.";
}
