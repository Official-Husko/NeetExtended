using System;
using System.Collections.Generic;
using NeetLoader.Runtime;

namespace NeetModKit.Sharing;

/// <summary>
/// Who decides the mod setup: which mods run, their features and their shared settings. Normally
/// the player. A co-op client hands it to the host for the mods that shape the shared house, and the
/// mod menu then shows those mods without their switches. <see cref="Version"/> moves on every change
/// that should redraw a page: a lock, a host's feature or setting.
/// </summary>
public static class ModControl
{
    private sealed class Lock : IDisposable
    {
        public string Reason;
        public Func<string, bool> Covers;

        public void Dispose()
        {
            if (Locks.Remove(this))
                Touch();
        }
    }

    private static readonly List<Lock> Locks = new List<Lock>();

    /// <summary>Goes up whenever who decides, or what was decided for this game, changes.</summary>
    public static int Version { get; private set; }

    public static event Action Changed;

    public static bool AnyLocked => Locks.Count > 0;

    /// <summary>
    /// From now until disposed (or <paramref name="owner"/> unloads), the mods <paramref name="covers"/>
    /// says yes to are decided elsewhere, for <paramref name="reason"/> ("The host decides this in co-op.").
    /// </summary>
    public static IDisposable HandOver(ITracker owner, string reason, Func<string, bool> covers)
    {
        var hold = new Lock { Reason = reason, Covers = covers };
        Locks.Add(hold);
        Touch();
        return owner.Track(hold);
    }

    /// <summary>Why this game can't change <paramref name="modId"/> now, or null when it can.</summary>
    public static string LockedBecause(string modId)
    {
        for (int i = Locks.Count - 1; i >= 0; i--)
        {
            if (Locks[i].Covers(modId))
                return Locks[i].Reason;
        }
        return null;
    }

    /// <summary>For the parts that apply another game's decisions: redraw what shows them.</summary>
    public static void Touch()
    {
        Version++;
        Changed?.Invoke();
    }
}
