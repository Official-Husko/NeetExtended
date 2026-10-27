using NeetModKit.Features;
using Randomizer.Parts;

namespace Randomizer.Features;

internal sealed class ShuffledHouseFeature : ModFeature
{
    public override string Id => "ShuffledHouse";
    public override string Name => "Shuffled house";
    public override string Description =>
        "On a new game, loose things in the rooms you can reach swap places. Keys, notes and things fixed in place stay. Only works for games started with it on.";

    protected override void OnEnable()
    {
        HouseShuffle.Enabled = true;
    }

    protected override void OnDisable()
    {
        HouseShuffle.Enabled = false;
    }
}
