using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.CassettePricePatch"/>.</summary>
internal sealed class CassettePriceFix : ModFeature
{
    public const string FeatureId = "CassettePrice";

    public override string Id => FeatureId;
    public override string Name => "Custom cassettes cost what the shop says";
    public override string Description =>
        "The cassette site's custom tape shows its price, then charges 10$ more (the surcharge custom pictures have). With this, a custom tape costs what it says.";
}
