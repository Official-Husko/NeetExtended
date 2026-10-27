using System;
using System.Collections.Generic;
using NeetLoader.Runtime;

namespace NeetModKit.Saves;

/// <summary>
/// While held, the game can't write saves: SaveAll (autosave, sleeping, dying) and the pause menu's
/// "reset save" do nothing. For a game that is showing someone else's house. Several holders can
/// overlap; saving comes back when the last one lets go.
/// </summary>
public static class SaveLock
{
    private sealed class Hold : IDisposable
    {
        public string Reason;
        public void Dispose() => Holds.Remove(this);
    }

    private static readonly List<Hold> Holds = new List<Hold>();

    public static bool IsLocked => Holds.Count > 0;

    /// <summary>Why saving is off (the first holder's reason), for log lines.</summary>
    public static string Reason => Holds.Count > 0 ? Holds[0].Reason : null;

    /// <summary>Blocks saving until the handle is disposed or <paramref name="owner"/> unloads.</summary>
    public static IDisposable Acquire(ITracker owner, string reason)
    {
        var hold = new Hold { Reason = reason };
        Holds.Add(hold);
        return owner.Track(hold);
    }
}
