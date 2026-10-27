using System;
using System.Collections.Generic;

namespace NeetModKit.Rules;

/// <summary>
/// Things waiting to happen after some play time ("ring this call in 5 minutes"), by id. Keep it in
/// your save data: times are <see cref="ModFlags.PlaySeconds"/>, so waiting survives saving and
/// loading.
/// </summary>
public sealed class ScheduleList
{
    public sealed class Entry
    {
        public string Id;
        public float DueAt;
    }

    public List<Entry> Entries = new List<Entry>();

    public int Count => Entries.Count;

    /// <summary>Waits <paramref name="seconds"/> of play, then <paramref name="id"/> is due. False if it's already waiting.</summary>
    public bool Add(string id, float seconds)
    {
        if (IndexOf(id) >= 0)
            return false;
        Entries.Add(new Entry { Id = id, DueAt = ModFlags.PlaySeconds + Math.Max(0f, seconds) });
        return true;
    }

    public bool Contains(string id) => IndexOf(id) >= 0;

    public void Remove(string id)
    {
        int i = IndexOf(id);
        if (i >= 0)
            Entries.RemoveAt(i);
    }

    /// <summary>
    /// The first id that's due, taken off the list, or null. <paramref name="handle"/> decides what
    /// happens: return true when done, false to try again after <paramref name="retrySeconds"/>.
    /// </summary>
    public string TakeDue(Func<string, bool> handle, float retrySeconds = 15f)
    {
        float now = ModFlags.PlaySeconds;
        for (int i = 0; i < Entries.Count; i++)
        {
            var entry = Entries[i];
            if (entry.DueAt > now)
                continue;
            if (handle(entry.Id))
            {
                Entries.RemoveAt(i);
                return entry.Id;
            }
            entry.DueAt = now + retrySeconds;
        }
        return null;
    }

    private int IndexOf(string id)
    {
        for (int i = 0; i < Entries.Count; i++)
        {
            if (string.Equals(Entries[i].Id, id, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
}
