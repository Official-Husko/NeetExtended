using System.Collections.Generic;
using LiteNetLib.Utils;
using Coop.Net;
using UnityEngine;

namespace Coop.World.Hazards;

/// <summary>
/// Client: puts the host's fire into this game's copy of it. A change from the host lights or puts
/// out one flame; the periodic list is matched flame by flame (new ones lit, gone ones put out).
/// </summary>
internal static class FireReceiver
{
    private const float SameFlame = 0.1f;
    private const float Centimetre = 0.01f;

    private static readonly List<Vector3> Listed = new List<Vector3>(256);
    private static readonly List<Location> GoneRooms = new List<Location>(64);
    private static readonly List<Vector3> GonePlaces = new List<Vector3>(64);
    private static readonly List<FireParticle> Extras = new List<FireParticle>(64);
    private static readonly List<GameObject> Spheres = new List<GameObject>(64);
    private static bool[] _matched = new bool[256];

    public static void OnDelta(in Sender from, NetDataReader reader)
    {
        int count = reader.GetUShort();
        bool apply = !WorldSide.IsHost && WorldSide.Running && Locator.rooms != null;
        GoneRooms.Clear();
        GonePlaces.Clear();
        for (int i = 0; i < count; i++)
        {
            byte kind = reader.GetByte();
            var room = (Location)reader.GetByte();
            if (kind == 2)
            {
                if (apply)
                    Out(room);
                continue;
            }
            var at = new Vector3(reader.GetShort(), reader.GetShort(), reader.GetShort()) * Centimetre;
            if (!apply || !Locator.rooms.ContainsKey(room))
                continue;
            if (kind == 0)
                Light(room, at);
            else
            {
                GoneRooms.Add(room);
                GonePlaces.Add(at);
            }
        }
        // Flames that went are put out after the new ones are lit, so a fire that moves on never
        // looks put out for a moment (the game clears a fire when its last flame goes).
        for (int i = 0; i < GoneRooms.Count; i++)
            PutOut(GoneRooms[i], GonePlaces[i]);
    }

    public static void OnSnapshot(in Sender from, NetDataReader reader)
    {
        var room = (Location)reader.GetByte();
        int count = reader.GetUShort();
        Listed.Clear();
        for (int i = 0; i < count; i++)
            Listed.Add(new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat()));
        if (WorldSide.IsHost || !WorldSide.Running || Locator.rooms == null || !Locator.rooms.ContainsKey(room))
            return;
        if (count == 0)
        {
            Out(room);
            return;
        }
        var spreader = Spreader(room);
        if (spreader != null)
            Match(spreader, room);
    }

    private static void Out(Location room)
    {
        var spreader = FireRooms.SpreaderIn(room);
        if (spreader == null)
            return;
        FireSync.Matching = true;
        try
        {
            spreader.Clear();
        }
        finally
        {
            FireSync.Matching = false;
        }
    }

    private static void Light(Location room, Vector3 at)
    {
        var spreader = Spreader(room);
        var carrier = spreader != null ? FireRooms.Carrier(spreader, room) : null;
        if (carrier == null)
            return;
        FireSync.Matching = true;
        try
        {
            carrier.SpawnFireParticle(at);
        }
        finally
        {
            FireSync.Matching = false;
        }
    }

    private static void PutOut(Location room, Vector3 at)
    {
        var spreader = FireRooms.SpreaderIn(room);
        var flame = spreader != null ? FireRooms.Nearest(spreader, at, SameFlame) : null;
        if (flame == null)
            return;
        FireSync.Matching = true;
        try
        {
            flame.Kill();
        }
        finally
        {
            FireSync.Matching = false;
        }
    }

    // The room's fire here, started empty when the host's first flame arrives.
    private static FireSpreader Spreader(Location room)
    {
        var spreader = FireRooms.SpreaderIn(room);
        if (spreader != null)
            return spreader;
        FireSync.Matching = true;
        try
        {
            return Locator.StartRoomFire(room, Locator.GetRoomCenter(room), noStarterSphere: true);
        }
        finally
        {
            FireSync.Matching = false;
        }
    }

    private static void Match(FireSpreader spreader, Location room)
    {
        var carrier = FireRooms.Carrier(spreader, room);
        if (carrier == null)
            return;
        if (_matched.Length < Listed.Count)
            _matched = new bool[Listed.Count * 2];
        System.Array.Clear(_matched, 0, Listed.Count);
        Extras.Clear();
        Spheres.Clear();
        Spheres.AddRange(spreader.spheres);

        // Flames of every sphere count: the game's own spheres from before the first list too.
        foreach (var sphere in Spheres)
        {
            var parts = sphere != null && sphere.TryGetComponent(out FireSpreaderSphere s) ? s.fireParts : null;
            if (parts == null)
                continue;
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var part = parts[i];
                if (part == null)
                {
                    parts.RemoveAt(i);
                    continue;
                }
                int near = Nearest(part.transform.position);
                if (near >= 0)
                    _matched[near] = true;
                else if (part.TryGetComponent(out FireParticle flame))
                    Extras.Add(flame);
            }
        }
        Spheres.Clear();

        FireSync.Matching = true;
        try
        {
            for (int i = 0; i < Listed.Count; i++)
            {
                if (!_matched[i])
                    carrier.SpawnFireParticle(Listed[i]);
            }
            foreach (var flame in Extras)
            {
                if (flame != null)
                    flame.Kill();
            }
        }
        finally
        {
            FireSync.Matching = false;
        }
        Extras.Clear();
    }

    private static int Nearest(Vector3 position)
    {
        int best = -1;
        float bestSq = SameFlame * SameFlame;
        for (int i = 0; i < Listed.Count; i++)
        {
            if (_matched[i])
                continue;
            float sq = (Listed[i] - position).sqrMagnitude;
            if (sq <= bestSq)
            {
                bestSq = sq;
                best = i;
            }
        }
        return best;
    }
}
