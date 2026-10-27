using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.LoadedPaintPatches"/>.</summary>
internal sealed class LoadedPaintFix : ModFeature
{
    public const string FeatureId = "LoadedPaint";

    public override string Id => FeatureId;
    public override string Name => "Loaded paint shows";
    public override string Description =>
        "Blood, scorch marks and other paint loaded from a save could stay invisible on some surfaces, and the next " +
        "splat there wiped it. In co-op it hit almost every painted surface of the house you joined. Now it shows.";
}
