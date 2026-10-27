using System;
using System.Collections.Generic;
using NeetLoader.Runtime;

namespace NeetModKit.Game;

/// <summary>
/// Whether this game runs the house, or shows a house another game runs (a co-op client). Things
/// that happen on their own (timed calls and emails, random events) should only happen where the
/// house runs, or they'd happen once per player.
/// </summary>
public static class WorldRole
{
    private sealed class Hold : IDisposable
    {
        public void Dispose()
        {
            if (Holds.Remove(this) && Holds.Count == 0)
                Changed?.Invoke();
        }
    }

    private static readonly List<Hold> Holds = new List<Hold>();

    /// <summary>False while this game shows someone else's house.</summary>
    public static bool RunsWorld => Holds.Count == 0;

    public static event Action Changed;

    /// <summary>From now until disposed (or <paramref name="owner"/> unloads), this game shows another game's house.</summary>
    public static IDisposable ShowOtherWorld(ITracker owner)
    {
        var hold = new Hold();
        Holds.Add(hold);
        if (Holds.Count == 1)
            Changed?.Invoke();
        return owner.Track(hold);
    }
}
