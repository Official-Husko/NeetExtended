using NeetModKit.Conversations;

namespace PhoneExpansion.Calls;

/// <summary>
/// Decides what plays when a call connects. The game starts every call with
/// Dialogue.InitCharacterDialogue(phone, "Phone", knot): "Phone.p" + digits for a dialed number,
/// "Phone.pPrank" + n for an answered incoming call. The router swaps in a modded knot when one fits.
/// </summary>
internal static class CallRouter
{
    public const string Flow = "Phone";

    /// <summary>The modded call in progress, or null.</summary>
    public static PhoneCall Active { get; private set; }

    public static Phone_Interface ActivePhone { get; private set; }

    public static string ActiveKnot { get; private set; }

    /// <summary>Returns false when the line should be busy instead of starting a conversation.</summary>
    public static bool Route(CharacterStories speaker, string flow, ref string knot)
    {
        if (flow != Flow || speaker == null || knot == null)
            return true;
        var phone = speaker.GetComponent<Phone_Interface>();
        if (phone == null)
            return true;

        if (knot.StartsWith(GameNumbers.PrankKnotPrefix))
        {
            var answered = IncomingCalls.TakeAnswered();
            if (answered != null && KnotExists(answered.Knot))
                Begin(answered, phone, ref knot, answered.Knot, played: true);
            return true;
        }

        if (!knot.StartsWith(GameNumbers.KnotPrefix))
            return true;
        return Dial(phone, knot.Substring(GameNumbers.KnotPrefix.Length), ref knot);
    }

    private static bool Dial(Phone_Interface phone, string digits, ref string knot)
    {
        PhoneCall otherwise = null;
        foreach (var call in PhoneBook.WithNumber(digits))
        {
            if (!KnotExists(call.Knot))
                continue;
            if (call.When == null || call.When.Test(call))
            {
                PhoneMemory.RecordDialed(digits);
                Begin(call, phone, ref knot, call.Knot, played: true);
                return true;
            }
            if (otherwise == null && call.OtherwiseKnot != null && KnotExists(call.OtherwiseKnot))
                otherwise = call;
        }

        if (otherwise != null)
        {
            PhoneMemory.RecordDialed(digits);
            Begin(otherwise, phone, ref knot, otherwise.OtherwiseKnot, played: false);
            return true;
        }

        if (GameNumbers.Contains(digits))
        {
            PhoneMemory.RecordDialed(digits);
            return true; // The game's own call.
        }

        // A modded number nobody answers right now: hang up on the player like an unknown number.
        phone.talking = false;
        PhoneLine.PlayBusyTone(phone);
        return false;
    }

    private static void Begin(PhoneCall call, Phone_Interface phone, ref string knot, string target, bool played)
    {
        knot = target;
        Active = call;
        ActivePhone = phone;
        ActiveKnot = target;
        if (played)
            PhoneMemory.RecordPlayed(call.Id);
    }

    public static void End()
    {
        Active = null;
        ActivePhone = null;
        ActiveKnot = null;
    }

    /// <summary>Hangs up a modded call in progress, for saving and unloading.</summary>
    public static void HangUpActive()
    {
        var phone = ActivePhone;
        End();
        if (phone != null)
            PhoneLine.HangUp(phone);
    }

    private static bool KnotExists(string knot)
    {
        var story = GameStory.Current;
        if (story == null || string.IsNullOrEmpty(knot))
            return false;
        int dot = knot.IndexOf('.');
        return story.mainContentContainer.namedContent.ContainsKey(dot < 0 ? knot : knot.Substring(0, dot));
    }
}
