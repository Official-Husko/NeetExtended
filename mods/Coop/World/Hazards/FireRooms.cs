using System.Collections.Generic;
using UnityEngine;

namespace Coop.World.Hazards;

/// <summary>Looking at a room's fire: its spreader, its flames, the sphere a client keeps them on.</summary>
internal static class FireRooms
{
    public static FireSpreader SpreaderIn(Location room)
    {
        var locator = Locator.GetRoom(room);
        var go = locator != null ? locator.fireSpreader : null;
        return go != null ? go.GetComponent<FireSpreader>() : null;
    }

    /// <summary>Every flame of the fire, where it burns.</summary>
    public static void Collect(FireSpreader spreader, List<Vector3> into)
    {
        into.Clear();
        foreach (var sphere in spreader.spheres)
        {
            var parts = sphere != null ? sphere.GetComponent<FireSpreaderSphere>()?.fireParts : null;
            if (parts == null)
                continue;
            foreach (var part in parts)
            {
                if (part != null)
                    into.Add(part.transform.position);
            }
        }
    }

    /// <summary>The flame of the fire nearest to a point, within a distance (null when none is).</summary>
    public static FireParticle Nearest(FireSpreader spreader, Vector3 at, float within)
    {
        FireParticle closest = null;
        float bestSq = within * within;
        foreach (var sphere in spreader.spheres)
        {
            var parts = sphere != null ? sphere.GetComponent<FireSpreaderSphere>()?.fireParts : null;
            if (parts == null)
                continue;
            foreach (var part in parts)
            {
                if (part == null)
                    continue;
                float sq = (part.transform.position - at).sqrMagnitude;
                if (sq < bestSq && part.TryGetComponent(out FireParticle flame))
                {
                    bestSq = sq;
                    closest = flame;
                }
            }
        }
        return closest;
    }

    /// <summary>A client keeps all of a room's flames on one sphere (its spreading is off here).</summary>
    public static FireSpreaderSphere Carrier(FireSpreader spreader, Location room)
    {
        foreach (var sphere in spreader.spheres)
        {
            if (sphere != null && sphere.TryGetComponent(out FireSpreaderSphere found))
                return found;
        }
        return spreader.SpawnFireSphere(Locator.GetRoomCenter(room), byUsableCol: false, noInitParticle: true);
    }
}
