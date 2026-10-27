using NeetModKit.Rules;

namespace PhoneExpansion.Calls;

/// <summary>
/// One call: a number the player can dial, a call that rings in, or both. What's said lives in an
/// Ink knot (written by hand or generated from a call file's "say" list).
/// </summary>
public sealed class PhoneCall : IRuleSubject
{
    /// <summary>Unique id, "ModId/name" for calls from files.</summary>
    public string Id;

    /// <summary>Digits to dial (3 to 7), or null for a call that only rings in.</summary>
    public string Number;

    /// <summary>Who's calling, for the mod menu.</summary>
    public string Name;

    /// <summary>Short text for the phone's screen while it rings. Defaults to the name.</summary>
    public string CallerId;

    /// <summary>Ink knot played when the call connects.</summary>
    public string Knot;

    /// <summary>When dialing: the call only picks up if this holds. Null means always.</summary>
    public Condition When;

    /// <summary>
    /// Knot played when <see cref="When"/> doesn't hold. Without one, the game's own call plays if the
    /// number is one of the game's, and otherwise the line is busy.
    /// </summary>
    public string OtherwiseKnot;

    /// <summary>Set to make the call ring in by itself.</summary>
    public IncomingRules Incoming;

    /// <summary>Where it came from (a file path or a mod name), for messages.</summary>
    public string Source;

    public string ScreenText => (CallerId ?? Name ?? "UNKNOWN").ToUpperInvariant();

    public string DisplayNumber => Number == null ? "" : Number.Length > 3 ? Number.Substring(0, 3) + "-" + Number.Substring(3) : Number;

    public bool ReplacesGameNumber => Number != null && GameNumbers.Contains(Number);

    string IRuleSubject.Id => Id;

    int IRuleSubject.Times => PhoneMemory.TimesPlayed(Id);

    public override string ToString() => Id;
}
