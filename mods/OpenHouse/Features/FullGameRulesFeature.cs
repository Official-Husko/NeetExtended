using NeetLoader.Runtime;
using NeetModKit.Events;
using NeetModKit.Features;
using NeetModKit.Game;

namespace OpenHouse.Features;

/// <summary>
/// Switches off the game's demo flag (HouseManager.demoMode). In this build it does two things:
/// the black and occult markets will take orders, and people wandering the house may walk into
/// the pantry.
/// </summary>
internal sealed class FullGameRulesFeature : ModFeature
{
    public override string Id => "FullGameRules";
    public override string Name => "Lift demo limits";
    public override string Description =>
        "Turns off the demo flag: the black and occult markets take orders, and people may wander into the pantry. Things the demo held back may not be finished.";
    public override bool EnabledByDefault => false;

    protected override void OnEnable() => GameEvents.WhenHouseReady(this, () =>
    {
        var house = GameRefs.House;
        if (house == null || !house.demoMode)
            return;
        house.demoMode = false;
        Track(Disposable.Create(() =>
        {
            if (house != null)
                house.demoMode = true;
        }));
    });
}
