using System.Collections.Generic;
using NeetModKit.Rules;
using NeetModKit.Saves;

namespace PhoneExpansion.Calls;

/// <summary>
/// What the phone remembers, kept in the save: how often each call was played and each number
/// reached, and calls waiting to ring. Times are play time (<see cref="ModFlags.PlaySeconds"/>), so a
/// "call back in 5 minutes" survives saving and loading. Flags live in <see cref="ModFlags"/>,
/// shared with every other mod.
/// </summary>
public static class PhoneMemory
{
    internal sealed class State
    {
        public Dictionary<string, CallRecord> Calls = new Dictionary<string, CallRecord>();
        public Dictionary<string, int> Dialed = new Dictionary<string, int>();
        public List<ScheduledRing> Scheduled = new List<ScheduledRing>();

        // Saves from before flags were shared keep them here; they're handed to ModFlags on load.
        public Dictionary<string, int> Flags;
        public float PlaySeconds;

        public bool ShouldSerializeFlags() => false;
        public bool ShouldSerializePlaySeconds() => false;
    }

    internal sealed class CallRecord
    {
        public int Times;
        public float LastPlayed = -1f;
    }

    internal sealed class ScheduledRing
    {
        public string CallId;
        public float DueAt;
    }

    private static State _state = new State();

    internal static State Current => _state;

    /// <summary>How many times the call with this id was played (dialed or answered).</summary>
    public static int TimesPlayed(string callId) =>
        callId != null && _state.Calls.TryGetValue(callId, out var record) ? record.Times : 0;

    /// <summary>Play time in seconds since the call was last played, or infinity if never.</summary>
    public static float SecondsSincePlayed(string callId) =>
        callId != null && _state.Calls.TryGetValue(callId, out var record) && record.LastPlayed >= 0f
            ? ModFlags.PlaySeconds - record.LastPlayed
            : float.PositiveInfinity;

    /// <summary>How many times a number connected to anyone (the game's own calls included).</summary>
    public static int TimesDialed(string digits) => digits != null && _state.Dialed.TryGetValue(digits, out int n) ? n : 0;

    internal static void RecordPlayed(string callId)
    {
        if (!_state.Calls.TryGetValue(callId, out var record))
            _state.Calls[callId] = record = new CallRecord();
        record.Times++;
        record.LastPlayed = ModFlags.PlaySeconds;
    }

    internal static void RecordDialed(string digits) => _state.Dialed[digits] = TimesDialed(digits) + 1;

    internal static void Init(PhoneExpansionMod mod) =>
        ModSaveData.Register<State>(mod, () => _state, Restore);

    private static void Restore(State state)
    {
        _state = state ?? new State();
        if (_state.Flags != null || _state.PlaySeconds > 0f)
        {
            ModFlags.Adopt(_state.Flags, _state.PlaySeconds);
            _state.Flags = null;
            _state.PlaySeconds = 0f;
        }
    }
}
