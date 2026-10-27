using NeetModKit.Rules;

namespace PhoneExpansion.Calls;

/// <summary>
/// When a call rings in by itself: "random" (in place of one of the game's prank calls), "order"
/// (after ordering from a computer store), "arrive" (after the house loads), "wake" (when the
/// player wakes up) or "manual" (only when something rings it, like another call's "ring" action).
/// The fields are <see cref="TriggerRules"/>'.
/// </summary>
public sealed class IncomingRules : TriggerRules
{
    /// <summary>The triggers a call can use; the first is the default.</summary>
    public static readonly string[] Triggers = { Random, Order, Arrive, Wake, Manual };

    internal static readonly IncomingRules Defaults = new IncomingRules();
}
