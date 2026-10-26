using NeetModKit.Events;
using NeetModKit.Features;
using PerformanceFixes.Rendering;

namespace PerformanceFixes.Features;

/// <summary>See <see cref="Peepholes"/>.</summary>
internal sealed class PeepholeFeature : ModFeature
{
    public const string FeatureId = "Peephole";

    public override string Id => FeatureId;
    public override string Name => "Peephole camera only near the door";
    public override string Description =>
        "A camera films the porch for the front door's peephole every frame, wherever you are in the house, and it " +
        "costs about a sixth of each frame. With this it only runs while you're near the front door.";

    protected override void OnEnable() => GameEvents.WhenHouseReady(this, Peepholes.Find);

    protected override void OnDisable() => Peepholes.Restore();

    public override void OnUpdate() => Peepholes.Update();
}
