using NeetModKit.Features;
using PerformanceFixes.Painting;

namespace PerformanceFixes.Features;

/// <summary>See <see cref="PaintSaver"/>.</summary>
internal sealed class FastPaintSavesFeature : ModFeature
{
    public const string FeatureId = "FastPaintSaves";

    public override string Id => FeatureId;
    public override string Name => "Faster saves";
    public override string Description =>
        "Every save writes a picture of each painted surface in the house (blood, dirt, water and so on), all of them, " +
        "every time. Once the house is messy that freezes the game for half a second or more. With this, only surfaces " +
        "that changed since the last save are written, in the background.";

    protected override void OnEnable() => PaintSaver.Start();

    protected override void OnDisable() => PaintSaver.Stop();
}
