using System.Collections.Generic;
using Coop.Net;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Hazards;

/// <summary>
/// Room fires burn on the host: it spreads them and its flames do the damage there. Once a second
/// the host sends each burning room's flame positions. A client keeps a fire of its own in that room
/// (smoke, light, crackle, smoke alarms through the game's own fire events) and matches its flames to
/// the host's list: new ones lit, gone ones put out. Its flames still burn the client's own player.
/// A flame put out on a client (water, the extinguisher) is put out on the host too.
///
/// WorldFire (host to all): room, flame count (0: the fire is out), positions.
/// WorldFireOut (client to host): room, position.
/// </summary>
internal static class FireSync
{
    private const float Interval = 1f;
    private const float SameFlame = 0.1f;

    private static readonly Dictionary<Location, int> Sent = new Dictionary<Location, int>();
    private static readonly List<Location> Gone = new List<Location>();
    private static readonly List<Vector3> Flames = new List<Vector3>(256);
    private static bool[] _matched = new bool[256];
    private static float _next;

    /// <summary>True while a client lines its flames up with the host's (those aren't put out by a player).</summary>
    public static bool Matching { get; private set; }

    public static void Init()
    {
        Messages.On(MessageIds.WorldFire, OnFire);
        Messages.On(MessageIds.WorldFireOut, OnFireOut);
    }

    public static void Clear() => Sent.Clear();

    public static void SendAgain() => Sent.Clear();

    /// <summary>Host, once a second: rooms whose flames changed, and rooms whose fire went out.</summary>
    public static void HostTick()
    {
        float now = Time.time;
        if (now < _next || Locator.rooms == null)
            return;
        _next = now + Interval;
        foreach (var pair in Locator.rooms)
        {
            var spreader = SpreaderIn(pair.Key);
            if (spreader == null)
                continue;
            Collect(spreader);
            int signature = Signature();
            if (Sent.TryGetValue(pair.Key, out int last) && last == signature)
                continue;
            Sent[pair.Key] = signature;
            Send(pair.Key);
        }
        Gone.Clear();
        foreach (var room in Sent.Keys)
        {
            if (SpreaderIn(room) == null)
                Gone.Add(room);
        }
        foreach (var room in Gone)
        {
            Sent.Remove(room);
            Flames.Clear();
            Send(room);
        }
    }

    /// <summary>Client, from FireParticle.Kill: a flame here went out by someone's doing.</summary>
    public static void FlameOut(FireParticle flame)
    {
        if (Matching || !WorldSide.Running || !WorldSide.IsClient || flame == null || flame.fireSphere == null || flame.fireSphere.spreader == null)
            return;
        var w = Outbox.Begin(MessageIds.WorldFireOut);
        w.Put((byte)flame.fireSphere.spreader.currentLocation);
        var p = flame.transform.position;
        w.Put(p.x);
        w.Put(p.y);
        w.Put(p.z);
        Outbox.ToHost(w, Channel.Events);
    }

    private static void Send(Location room)
    {
        var w = Outbox.Begin(MessageIds.WorldFire);
        w.Put((byte)room);
        w.Put((ushort)Flames.Count);
        foreach (var p in Flames)
        {
            w.Put(p.x);
            w.Put(p.y);
            w.Put(p.z);
        }
        Outbox.ToPlayers(w, Channel.Events);
    }

    private static void OnFire(in Sender from, NetDataReader reader)
    {
        var room = (Location)reader.GetByte();
        int count = reader.GetUShort();
        Flames.Clear();
        for (int i = 0; i < count; i++)
            Flames.Add(new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat()));
        if (WorldSide.IsHost || !WorldSide.Running || Locator.rooms == null || !Locator.rooms.ContainsKey(room))
            return;
        var spreader = SpreaderIn(room);
        if (count == 0)
        {
            if (spreader != null)
                spreader.Clear();
            return;
        }
        spreader ??= Locator.StartRoomFire(room, Locator.GetRoomCenter(room), noStarterSphere: true);
        if (spreader != null)
            Match(spreader, room);
    }

    // The client's fire keeps all its flames on one sphere (its spreading is off here).
    private static void Match(FireSpreader spreader, Location room)
    {
        FireSpreaderSphere carrier = null;
        foreach (var sphere in spreader.spheres)
        {
            if (sphere != null && (carrier = sphere.GetComponent<FireSpreaderSphere>()) != null)
                break;
        }
        carrier ??= spreader.SpawnFireSphere(Locator.GetRoomCenter(room), byUsableCol: false, noInitParticle: true);
        if (carrier == null)
            return;
        if (_matched.Length < Flames.Count)
            _matched = new bool[Flames.Count * 2];
        System.Array.Clear(_matched, 0, Flames.Count);

        Matching = true;
        try
        {
            var parts = carrier.fireParts;
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
                    flame.Kill();
            }
            for (int i = 0; i < Flames.Count; i++)
            {
                if (!_matched[i])
                    carrier.SpawnFireParticle(Flames[i]);
            }
        }
        finally
        {
            Matching = false;
        }
    }

    private static int Nearest(Vector3 position)
    {
        int best = -1;
        float bestSq = SameFlame * SameFlame;
        for (int i = 0; i < Flames.Count; i++)
        {
            if (_matched[i])
                continue;
            float sq = (Flames[i] - position).sqrMagnitude;
            if (sq <= bestSq)
            {
                bestSq = sq;
                best = i;
            }
        }
        return best;
    }

    private static void OnFireOut(in Sender from, NetDataReader reader)
    {
        var room = (Location)reader.GetByte();
        var at = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        var spreader = WorldSide.IsHost && WorldSide.Running ? SpreaderIn(room) : null;
        if (spreader == null)
            return;
        Collect(spreader);
        FireParticle closest = null;
        float bestSq = 0.5f * 0.5f;
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
        closest?.Kill();
    }

    private static FireSpreader SpreaderIn(Location room)
    {
        var locator = Locator.GetRoom(room);
        var go = locator != null ? locator.fireSpreader : null;
        return go != null ? go.GetComponent<FireSpreader>() : null;
    }

    private static void Collect(FireSpreader spreader)
    {
        Flames.Clear();
        foreach (var sphere in spreader.spheres)
        {
            var parts = sphere != null ? sphere.GetComponent<FireSpreaderSphere>()?.fireParts : null;
            if (parts == null)
                continue;
            foreach (var part in parts)
            {
                if (part != null)
                    Flames.Add(part.transform.position);
            }
        }
    }

    // Flames don't move, so how many there are and roughly where is enough to notice a change.
    private static int Signature()
    {
        unchecked
        {
            int signature = Flames.Count;
            foreach (var p in Flames)
                signature = signature * 31 + Mathf.RoundToInt(p.x * 10f) * 7 + Mathf.RoundToInt(p.y * 10f) * 13 + Mathf.RoundToInt(p.z * 10f);
            return signature;
        }
    }
}
