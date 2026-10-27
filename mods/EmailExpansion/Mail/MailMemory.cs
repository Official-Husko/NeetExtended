using System.Collections.Generic;
using NeetModKit.Rules;
using NeetModKit.Saves;

namespace EmailExpansion.Mail;

/// <summary>
/// What the email mod keeps in the save: how often each email was sent and whether it was read,
/// what each email in the inbox has shown so far (its text and the replies picked), emails waiting
/// to arrive, and when the next random one is due. Times are play time (<see cref="ModFlags.PlaySeconds"/>).
/// </summary>
public static class MailMemory
{
    internal sealed class State
    {
        public Dictionary<string, MailRecord> Mails = new Dictionary<string, MailRecord>();
        public Dictionary<string, Letter> Letters = new Dictionary<string, Letter>();
        public ScheduleList Waiting = new ScheduleList();
        public float NextRandomAt = -1f;
    }

    internal sealed class MailRecord
    {
        public int Sent;
        public float LastSent = -1f;
        public bool Read;
    }

    /// <summary>One email in the inbox, as far as it has been read.</summary>
    internal sealed class Letter
    {
        public string Text = "";
        public List<string> Replies = new List<string>();
        public bool Started;
        public bool Done;
    }

    private static State _state = new State();

    internal static State Current => _state;

    public static int TimesSent(string id) => id != null && _state.Mails.TryGetValue(id, out var r) ? r.Sent : 0;

    public static bool WasRead(string id) => id != null && _state.Mails.TryGetValue(id, out var r) && r.Read;

    /// <summary>Play seconds since the email was last sent, or infinity if never.</summary>
    public static float SecondsSinceSent(string id) =>
        id != null && _state.Mails.TryGetValue(id, out var r) && r.LastSent >= 0f
            ? ModFlags.PlaySeconds - r.LastSent
            : float.PositiveInfinity;

    internal static void RecordSent(string id)
    {
        var record = Record(id);
        record.Sent++;
        record.LastSent = ModFlags.PlaySeconds;
    }

    internal static void RecordRead(string id) => Record(id).Read = true;

    /// <summary>The letter for one inbox entry, made on first use.</summary>
    internal static Letter LetterFor(string key)
    {
        if (!_state.Letters.TryGetValue(key, out var letter))
            _state.Letters[key] = letter = new Letter();
        return letter;
    }

    internal static void Init(EmailExpansionMod mod) =>
        ModSaveData.Register<State>(mod, () => _state, state => _state = state ?? new State());

    private static MailRecord Record(string id)
    {
        if (!_state.Mails.TryGetValue(id, out var record))
            _state.Mails[id] = record = new MailRecord();
        return record;
    }
}
