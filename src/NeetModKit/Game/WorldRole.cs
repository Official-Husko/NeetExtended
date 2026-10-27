using System;
using System.Collections.Generic;
using NeetLoader.Runtime;

namespace NeetModKit.Game;

/// <summary>
/// Whether this game runs the house, or shows a house another game runs (a co-op client), and
/// whether other players share it. Things that happen on their own (timed calls and emails, random
/// events) should only happen where the house runs, or they'd happen once per player; and nothing
/// should stop time in a shared house, or it stops for everyone.
/// </summary>
public static class WorldRole
{
    private sealed class Hold : IDisposable
    {
        private readonly List<Hold> _list;

        public Hold(List<Hold> list) => _list = list;

        public void Dispose()
        {
            if (_list.Remove(this) && _list.Count == 0)
                Changed?.Invoke();
        }
    }

    private static readonly List<Hold> OtherWorld = new List<Hold>();
    private static readonly List<Hold> Shared = new List<Hold>();

    /// <summary>False while this game shows someone else's house.</summary>
    public static bool RunsWorld => OtherWorld.Count == 0;

    /// <summary>
    /// True while other players are in this game's house or this game is in theirs (a co-op
    /// session). The framework's windows don't pause the game then.
    /// </summary>
    public static bool IsShared => Shared.Count > 0;

    /// <summary><see cref="RunsWorld"/> or <see cref="IsShared"/> changed.</summary>
    public static event Action Changed;

    /// <summary>From now until disposed (or <paramref name="owner"/> unloads), this game shows another game's house.</summary>
    public static IDisposable ShowOtherWorld(ITracker owner) => Take(owner, OtherWorld);

    /// <summary>From now until disposed (or <paramref name="owner"/> unloads), other players share this house.</summary>
    public static IDisposable ShareWorld(ITracker owner) => Take(owner, Shared);

    private static IDisposable Take(ITracker owner, List<Hold> list)
    {
        var hold = new Hold(list);
        list.Add(hold);
        if (list.Count == 1)
            Changed?.Invoke();
        return owner.Track(hold);
    }
}
