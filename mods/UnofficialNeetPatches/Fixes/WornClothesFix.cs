using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.WornClothesPatch"/>.</summary>
internal sealed class WornClothesFix : ModFeature
{
    public const string FeatureId = "WornClothes";

    public override string Id => FeatureId;
    public override string Name => "Worn clothes stay put";
    public override string Description =>
        "Clothes you wore when you saved fell through the world forever after loading (one reached 20,000 km down). " +
        "They stay parked, like the ones you put on yourself.";
}
