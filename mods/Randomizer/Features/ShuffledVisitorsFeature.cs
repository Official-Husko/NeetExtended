using NeetModKit.Features;
using Randomizer.Parts;

namespace Randomizer.Features;

internal sealed class ShuffledVisitorsFeature : ModFeature
{
    public override string Id => "ShuffledVisitors";
    public override string Name => "Shuffled visitors";
    public override string Description =>
        "The people who knock at night come in a different order every game. Ones that already came keep their place.";

    protected override void OnEnable()
    {
        Visitors.Enabled = true; Visitors.Apply();
    }

    protected override void OnDisable()
    {
        Visitors.Enabled = false; Visitors.Apply();
    }
}
