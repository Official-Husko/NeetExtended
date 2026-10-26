using System;
using System.Collections.Generic;

namespace NeetModKit.Game;

/// <summary>
/// The house's doors (<see cref="HouseManager.doors"/>). Locked doors are matched by their doorId,
/// for example BASEMENT, DADYARD or Attic.
/// </summary>
public static class Doors
{
    /// <summary>Every door in the house; empty outside it.</summary>
    public static IEnumerable<Door> All
    {
        get
        {
            var house = GameRefs.House;
            if (house == null || house.doors == null)
                yield break;
            foreach (var door in house.doors)
            {
                if (door != null)
                    yield return door;
            }
        }
    }

    /// <summary>Doors whose doorId is one of <paramref name="ids"/> (case-insensitive).</summary>
    public static IEnumerable<Door> WithIds(params string[] ids)
    {
        foreach (var door in All)
        {
            foreach (string id in ids)
            {
                if (string.Equals(door.doorId, id, StringComparison.OrdinalIgnoreCase))
                {
                    yield return door;
                    break;
                }
            }
        }
    }

    /// <summary>Unlocks the doors with this id. Returns how many were locked before.</summary>
    public static int Unlock(string id) => SetLocked(WithIds(id), false);

    /// <summary>Unlocks every door in the house. Returns how many were locked before.</summary>
    public static int UnlockAll() => SetLocked(All, false);

    /// <summary>Locks or unlocks doors through the game's setter, so unlock listeners fire.</summary>
    public static int SetLocked(IEnumerable<Door> doors, bool locked)
    {
        int changed = 0;
        foreach (var door in doors)
        {
            if (door.Locked == locked)
                continue;
            door.Locked = locked;
            changed++;
        }
        return changed;
    }
}
