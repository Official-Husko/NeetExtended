using NeetModKit.Features;
using PhoneExpansion.Calls;

namespace PhoneExpansion.Features;

/// <summary>Who's calling, on the phone's screen while it rings.</summary>
internal sealed class CallerIdFeature : ModFeature
{
    public override string Id => "CallerId";
    public override string Name => "Caller ID";
    public override string Description => "Shows who's calling on the phone's screen while it rings. The game's own prank calls show up as UNKNOWN.";

    protected override void OnEnable() => CallerScreen.Enabled = true;

    protected override void OnDisable()
    {
        CallerScreen.Enabled = false;
        CallerScreen.ClearAll();
    }
}
