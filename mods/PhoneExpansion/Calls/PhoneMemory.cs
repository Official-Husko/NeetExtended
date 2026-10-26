using System.Collections.Generic;
using NeetModKit.Saves;
using UnityEngine;

namespace PhoneExpansion.Calls;

/// <summary>
/// What the phone remembers, kept in the save: flags set by calls, how often each call was played
/// and each number reached, and calls waiting to ring. Time is counted in play time (seconds the
/// house was loaded), so a "call back in 5 minutes" survives saving and loading.
/// </summary>
public static class PhoneMemory
{
    internal sealed class State
    {
        public Dictionary<string, int> Flags = new Dictionary<string, int>();
        public Dictionary<string, CallRecord> Calls = new Dictionary<string, CallRecord>();
        public Dictionary<string, int> Dialed = new Dictionary<string, int>();
        public List<ScheduledRing> Scheduled = new List<ScheduledRing>();
        public float PlaySeconds;
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
    private static float _clockBase;
    private static float _clockSince;

    internal static State Current => _state;

    /// <summary>Seconds of play in this save.</summary>
    public static float PlaySeconds => _clockBase + (Time.time - _clockSince);

    public static int Flag(string name) => name != null && _state.Flags.TryGetValue(name, out int value) ? value : 0;

    public static void SetFlag(string name, int value)
    {
        if (value == 0)
            _state.Flags.Remove(name);
        else
            _state.Flags[name] = value;
    }

    public static void AddFlag(string name, int amount) => SetFlag(name, Flag(name) + amount);

    /// <summary>How many times the call with this id was played (dialed or answered).</summary>
    public static int TimesPlayed(string callId) =>
        callId != null && _state.Calls.TryGetValue(callId, out var record) ? record.Times : 0;

    /// <summary>Play time in seconds since the call was last played, or infinity if never.</summary>
    public static float SecondsSincePlayed(string callId) =>
        callId != null && _state.Calls.TryGetValue(callId, out var record) && record.LastPlayed >= 0f
            ? PlaySeconds - record.LastPlayed
            : float.PositiveInfinity;

    /// <summary>How many times a number connected to anyone (the game's own calls included).</summary>
    public static int TimesDialed(string digits) => digits != null && _state.Dialed.TryGetValue(digits, out int n) ? n : 0;

    internal static IReadOnlyDictionary<string, int> Flags => _state.Flags;

    internal static void RecordPlayed(string callId)
    {
        if (!_state.Calls.TryGetValue(callId, out var record))
            _state.Calls[callId] = record = new CallRecord();
        record.Times++;
        record.LastPlayed = PlaySeconds;
    }

    internal static void RecordDialed(string digits) => _state.Dialed[digits] = TimesDialed(digits) + 1;

    internal static void Init(PhoneExpansionMod mod) =>
        ModSaveData.Register<State>(mod, Snapshot, Restore);

    private static State Snapshot()
    {
        _state.PlaySeconds = PlaySeconds;
        return _state;
    }

    private static void Restore(State state)
    {
        _state = state ?? new State();
        _clockBase = _state.PlaySeconds;
        _clockSince = Time.time;
    }
}
