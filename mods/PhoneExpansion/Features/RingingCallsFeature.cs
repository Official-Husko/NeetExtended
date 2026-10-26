using NeetModKit.Features;
using PhoneExpansion.Calls;

namespace PhoneExpansion.Features;

/// <summary>Modded calls ringing in by themselves.</summary>
internal sealed class RingingCallsFeature : ModFeature
{
    public override string Id => "RingingCalls";
    public override string Name => "New calls ring in";
    public override string Description =>
        "New calls sometimes ring in place of the game's random prank calls, after store orders or when you get home. " +
        "How often is set in the settings. Calls you arranged in a conversation (callbacks) ring either way.";

    protected override void OnEnable() => IncomingCalls.Enabled = true;

    protected override void OnDisable() => IncomingCalls.Enabled = false;
}
