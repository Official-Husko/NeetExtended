namespace NeetModKit.Features.Builtin;

internal sealed class CompressedSavesFeature : ModFeature
{
    public const string FeatureId = "CompressedSaves";

    public override string Id => FeatureId;
    public override string Name => "Compressed saves";
    public override string Description =>
        "Saves are written as .nsav files, about 16 times smaller than the game's JSON. Switched off, saves are plain JSON like the original game. Both kinds always load.";
}
