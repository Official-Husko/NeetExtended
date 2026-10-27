using System.Collections.Generic;
using Coop.Players;
using Coop.Session;
using Coop.World.Forwarding;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Coop.World.Hazards;

/// <summary>
/// The house's kill planes and teleporters act on the player who touches them, in the game that
/// simulates that player. A remote body here is a kinematic stand-in the game's own checks never
/// see (no brain, no tag, and kinematic against a static plane sends no collision), so this checks
/// each body against them and, on touching one, tells the body's own game where the hazard sends it
/// (<see cref="BodyMoves"/>). Hazards are looked up once per scene and checked by bounds, once per
/// physics step; only the moment of touching counts.
/// </summary>
internal static class BodyHazards
{
    private struct Hazard
    {
        public Collider Area;
        public KillPlane Plane;
        public Teleporter Teleporter;
    }

    private static readonly List<Hazard> Hazards = new List<Hazard>();
    // Per body: which hazards it is inside (one bit each).
    private static readonly Dictionary<byte, ulong> Inside = new Dictionary<byte, ulong>();
    private static int _scene = -1;

    public static void Init()
    {
        CoopLoop.FixedUpdate += Tick;
        CoopSession.Ended += _ => Clear();
        CoopLoop.Unloading += Clear;
    }

    public static void Clear()
    {
        Hazards.Clear();
        Inside.Clear();
        _scene = -1;
    }

    private static void Tick()
    {
        if (!WorldSide.Running || HouseManager._ == null)
            return;
        int scene = SceneManager.GetActiveScene().handle;
        if (scene != _scene)
            Find(scene);
        if (Hazards.Count == 0)
            return;
        foreach (var puppet in RemotePlayers.All)
        {
            if (puppet == null || puppet.Shoulder != null)
                continue;
            Inside.TryGetValue(puppet.Slot, out ulong now);
            ulong before = now;
            var bounds = puppet.Bounds;
            for (int i = 0; i < Hazards.Count; i++)
            {
                var hazard = Hazards[i];
                ulong bit = 1UL << i;
                // A hazard that's switched off isn't touched.
                bool touching = hazard.Area != null && hazard.Area.enabled && hazard.Area.gameObject.activeInHierarchy
                    && hazard.Area.bounds.Intersects(bounds);
                if (!touching)
                {
                    now &= ~bit;
                    continue;
                }
                if ((now & bit) == 0)
                {
                    now |= bit;
                    Touch(puppet, hazard);
                }
            }
            if (now != before)
                Inside[puppet.Slot] = now;
        }
    }

    private static void Touch(PlayerPuppet puppet, in Hazard hazard)
    {
        if (hazard.Plane != null)
            BodyMoves.Moved(puppet.Slot, HouseManager._.packageDropOffPoint.position + Vector3.up * 2f, bringHeld: false);
        else if (hazard.Teleporter != null)
            BodyMoves.Moved(puppet.Slot, puppet.transform.position + hazard.Teleporter.offset, bringHeld: true);
    }

    // Once per scene; inactive hazards are included, since the house can switch them on.
    private static void Find(int scene)
    {
        _scene = scene;
        Hazards.Clear();
        Inside.Clear();
        foreach (var plane in Object.FindObjectsOfType<KillPlane>(true))
            Add(plane.GetComponent<Collider>(), plane, null);
        foreach (var teleporter in Object.FindObjectsOfType<Teleporter>(true))
            Add(teleporter.GetComponent<Collider>(), null, teleporter);
        CoopMod.Log.Info($"Watching {Hazards.Count} kill planes and teleporters for other players' bodies.");
    }

    private static void Add(Collider area, KillPlane plane, Teleporter teleporter)
    {
        if (area != null && Hazards.Count < 64)
            Hazards.Add(new Hazard { Area = area, Plane = plane, Teleporter = teleporter });
    }
}
