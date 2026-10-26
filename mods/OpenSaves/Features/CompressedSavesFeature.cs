using NeetModKit.Features;

namespace OpenSaves.Features;

internal sealed class CompressedSavesFeature : ModFeature
{
    public const string FeatureId = "CompressedSaves";

    public override string Id => FeatureId;
    public override string Name => "Compressed saves";
    public override string Description =>
        "Write saves as .nsav files, about 16 times smaller. Switched off, saves are plain JSON like the original game. Both always load while this mod is installed.";
}
