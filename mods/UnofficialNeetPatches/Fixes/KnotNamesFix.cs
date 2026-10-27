using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.KnotNamesPatch"/>.</summary>
internal sealed class KnotNamesFix : ModFeature
{
    public const string FeatureId = "KnotNames";

    public override string Id => FeatureId;
    public override string Name => "Characters say their reaction lines";
    public override string Description =>
        "Poopman has lines for when you hurt him or throw something gross at him, but the game looks for them under a slightly different spelling of his name and never finds them. With this, he says them.";
}
