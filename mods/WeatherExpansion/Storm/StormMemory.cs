using System;
using NeetModKit.Game;
using NeetModKit.Rules;

namespace WeatherExpansion.Storm;

/// <summary>
/// Where the storm stands, as the save keeps it: the weather now, how far into it, what is planned next
/// and how long until then. Times are relative (seconds), because the play clock a state was started on
/// is not the one a loaded game runs on. The cycle writes <see cref="Record"/> whenever a phase begins
/// and reads <see cref="TakeSaved"/> once when the house is ready.
/// </summary>
internal static class StormMemory
{
    /// <summary>The part that goes into the save file.</summary>
    public sealed class Saved
    {
        public bool Has;
        public int Kind;
        public float Intensity = 1f;
        public float Age;
        public float Left;
        public int NextKind;
        public float NextIntensity = 1f;
    }

    private static StormPhase _phase;
    private static StormPhase _next;
    private static float _startedAt;
    private static float _nextAt;
    private static bool _has;
    private static Saved _pending;
    // What the save had, kept to write back while the cycle is off so a switched-off feature doesn't lose it.
    private static Saved _loaded;

    /// <summary>A save was loaded (or the mod was reloaded with state to carry): the cycle should pick it up.</summary>
    public static event Action Restored;

    public static void Record(StormPhase phase, float startedAt, StormPhase next, float nextAt)
    {
        _phase = phase;
        _startedAt = startedAt;
        _next = next;
        _nextAt = nextAt;
        _has = true;
    }

    /// <summary>The save entry for the weather now; nothing to keep until the cycle has run.</summary>
    public static Saved ForSave()
    {
        if (!_has)
            return _loaded ?? new Saved();
        float now = ModFlags.PlaySeconds;
        return new Saved
        {
            Has = true,
            Kind = (int)_phase.Kind,
            Intensity = _phase.Intensity,
            Age = Math.Max(0f, now - _startedAt),
            Left = Math.Max(0f, _nextAt - now),
            NextKind = (int)_next.Kind,
            NextIntensity = _next.Intensity,
        };
    }

    public static void FromSave(Saved saved)
    {
        _has = false;
        _loaded = saved != null && saved.Has && Valid(saved) ? saved : null;
        _pending = _loaded;
        Restored?.Invoke();
    }

    /// <summary>The loaded entry, once. Null when the save had none (a new game, or from before this mod).</summary>
    public static Saved TakeSaved()
    {
        var saved = _pending;
        _pending = null;
        return saved;
    }

    private static bool Valid(Saved s) =>
        Enum.IsDefined(typeof(WeatherKind), s.Kind) && Enum.IsDefined(typeof(WeatherKind), s.NextKind) &&
        !float.IsNaN(s.Intensity) && !float.IsNaN(s.NextIntensity) && !float.IsNaN(s.Left) && !float.IsNaN(s.Age);
}
