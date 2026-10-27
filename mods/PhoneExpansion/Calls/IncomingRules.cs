using NeetModKit.Rules;

namespace PhoneExpansion.Calls;

/// <summary>When a call rings in by itself.</summary>
public sealed class IncomingRules
{
    public const string Random = "random";
    public const string Order = "order";
    public const string Arrive = "arrive";
    public const string Manual = "manual";

    /// <summary>
    /// What makes it ring: "random" (mixed in with the game's prank calls), "order" (after ordering
    /// from a computer store), "arrive" (after the house loads) or "manual" (only when something
    /// rings it, like another call's "ring" action).
    /// </summary>
    public string Trigger = Random;

    /// <summary>How likely it is picked among the other calls that could ring (default 1).</summary>
    public float Weight = 1f;

    /// <summary>Only ring while this holds.</summary>
    public Condition When;

    /// <summary>Only ever ring once per save.</summary>
    public bool Once;

    /// <summary>Minutes of play before it can ring again.</summary>
    public float CooldownMinutes;

    /// <summary>For "order" and "arrive": seconds to wait before ringing.</summary>
    public float DelaySeconds = 20f;

    /// <summary>For "order": only after orders from this store (Normal, BlackMarket, OccultMarket). Null for any.</summary>
    public string Store;
}
