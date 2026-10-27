using NeetModKit.Features;
using Randomizer.Parts;

namespace Randomizer.Features;

internal sealed class RandomNeedsFeature : ModFeature
{
    public override string Id => "RandomNeeds";
    public override string Name => "Random needs";
    public override string Description =>
        "Hunger, energy, bladder and hygiene drain faster or slower than usual, differently every game (how far is in the settings).";

    protected override void OnEnable()
    {
        Needs.Enabled = true; Needs.Apply();
    }

    protected override void OnDisable()
    {
        Needs.Enabled = false; Needs.Apply();
    }
}
