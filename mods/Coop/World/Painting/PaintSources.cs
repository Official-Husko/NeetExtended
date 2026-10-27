using System.Collections.Generic;
using UnityEngine;

namespace Coop.World.Painting;

/// <summary>
/// What is painting right now, for the few painters that run in every game at once: a sink or bath
/// tap is shared as on or off, and then each game runs its own water (the stream's particles and the
/// overflow's blobs), so each game paints that water itself and none of it is sent. The game's
/// CollisionPainter and ParticlePainter say which object paints (<see cref="Patches.PainterPatches"/>).
/// </summary>
internal static class PaintSources
{
    private const int MaxBlobs = 2048;

    // Instance ids: painters that are part of a tap, the taps' overflow spawners, blobs those made.
    private static readonly HashSet<int> TapPainters = new HashSet<int>();
    private static readonly HashSet<int> TapSpawners = new HashSet<int>();
    private static readonly HashSet<int> TapWater = new HashSet<int>();

    /// <summary>The object whose painter is running, or null.</summary>
    public static GameObject Painter;

    public static void Rebuild()
    {
        Clear();
        var house = HouseManager._;
        if (house == null || house.sinkFaucets == null)
            return;
        foreach (var tap in house.sinkFaucets)
        {
            if (tap == null)
                continue;
            AddPainters(tap.gameObject);
            if (tap.waterStreamPart != null)
                AddPainters(tap.waterStreamPart.gameObject);
            if (tap.streamObj != null)
                AddPainters(tap.streamObj);
            if (tap.TryGetComponent(out Spawner spawner))
                TapSpawners.Add(spawner.GetInstanceID());
        }
    }

    /// <summary>True when the current splat comes from something every game runs by itself.</summary>
    public static bool Mirrored()
    {
        var painter = Painter;
        if (painter == null)
            return false;
        int id = painter.GetInstanceID();
        return TapPainters.Contains(id) || TapWater.Contains(id);
    }

    /// <summary>From Spawner.Spawn: remembers the blobs a tap's overflow makes.</summary>
    public static void Spawned(Spawner spawner, GameObject made)
    {
        if (made == null || !TapSpawners.Contains(spawner.GetInstanceID()))
            return;
        if (TapWater.Count >= MaxBlobs)
            TapWater.Clear();
        TapWater.Add(made.GetInstanceID());
    }

    public static void Clear()
    {
        Painter = null;
        TapPainters.Clear();
        TapSpawners.Clear();
        TapWater.Clear();
    }

    private static void AddPainters(GameObject root)
    {
        foreach (var painter in root.GetComponentsInChildren<ParticlePainter>(true))
            TapPainters.Add(painter.gameObject.GetInstanceID());
        foreach (var painter in root.GetComponentsInChildren<CollisionPainter>(true))
            TapPainters.Add(painter.gameObject.GetInstanceID());
    }
}
