using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.MaskDeathPatch"/>.</summary>
internal sealed class MaskDeathFix : ModFeature
{
    public const string FeatureId = "MaskDeath";

    public override string Id => FeatureId;
    public override string Name => "Dying with a mask on works";
    public override string Description =>
        "Anyone who died wearing a mask (you or a character) got stuck half dead: the death stopped with an error, " +
        "and they couldn't die again. Masks now come off first, and the death goes through.";
}
