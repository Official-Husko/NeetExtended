namespace PhoneExpansion.Calls;

/// <summary>Ringing calls from code: <c>PhoneRing.Ring("MyMod/landlord", 120f)</c>.</summary>
public static class PhoneRing
{
    /// <summary>
    /// Rings the call with this id after <paramref name="delaySeconds"/> of play (0 = as soon as the
    /// line is free). Returns false if there's no such call.
    /// </summary>
    public static bool Ring(string callId, float delaySeconds = 0f)
    {
        var call = PhoneBook.Find(callId);
        if (call == null)
            return false;
        IncomingCalls.Ring(call, delaySeconds);
        return true;
    }

    public static void Ring(PhoneCall call, float delaySeconds = 0f) => IncomingCalls.Ring(call, delaySeconds);

    /// <summary>Takes a call off the waiting list.</summary>
    public static void Cancel(PhoneCall call) => IncomingCalls.Cancel(call);

    /// <summary>The modded call ringing right now, if any.</summary>
    public static PhoneCall Ringing => IncomingCalls.Ringing;

    /// <summary>The modded call being talked on right now, if any.</summary>
    public static PhoneCall Active => CallRouter.Active;
}
