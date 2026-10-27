using System.Collections.Generic;
using Coop.Net;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Hazards;

/// <summary>
/// Room fires burn on the host: it spreads them and its flames do the damage there. Every flame the
/// host's fire lights or loses is sent the frame it happens, batched into one message a frame, so a
/// client's flames come and go within a network trip of the host's. A client keeps a fire of its own
/// in that room (smoke, light, crackle, smoke alarms through the game's own fire events; its spreading
/// is off) and follows the list. Its flames still burn the client's own player, but they light nothing
/// else (see FlameSync): the host's fire does that and the result is shared.
///
/// Every few seconds the host also sends each burning room's whole flame list, which the client
/// lines its flames up with. That is the safety net, and how a player who just arrived catches up.
/// A flame put out on a client (water, the extinguisher) is put out on the host too, and a fire a
/// client starts (a spark, a burning item on the furniture) is asked for from the host.
///
/// WorldFireDelta (host to all): count, then per change: kind (0 lit, 1 gone, 2 whole fire out), room,
/// position in centimetres as three shorts (kinds 0 and 1).
/// WorldFire (host to all): room, flame count (0: the fire is out), positions as floats.
/// WorldFireOut (client to host): room, position.
/// WorldFireStart (client to host): room, origin, started by a burning item.
/// </summary>
internal static class FireSync
{
    private const float ResyncSeconds = 5f;
    private const int MaxOpsPerMessage = 250;
    private const byte Lit = 0;
    private const byte Gone = 1;
    private const byte Out = 2;

    private struct Op
    {
        public byte Kind;
        public byte Room;
        public short X, Y, Z;
    }

    private static readonly List<Op> Ops = new List<Op>(64);
    private static readonly List<Vector3> Flames = new List<Vector3>(256);
    private static float _resyncAt;
    private static bool _again;

    /// <summary>True while a client lines its fire up with the host's (those aren't flames a player put out).</summary>
    public static bool Matching { get; internal set; }

    public static void Init()
    {
        Messages.On(MessageIds.WorldFire, FireReceiver.OnSnapshot);
        Messages.On(MessageIds.WorldFireDelta, FireReceiver.OnDelta);
        Messages.On(MessageIds.WorldFireOut, OnFireOut);
        Messages.On(MessageIds.WorldFireStart, FireStarts.OnStart);
    }

    public static void Clear()
    {
        Ops.Clear();
        _resyncAt = 0f;
        _again = false;
        FireStarts.Clear();
    }

    /// <summary>A new player is in: everyone gets every burning room's flame list again.</summary>
    public static void SendAgain() => _again = true;

    /// <summary>Host, from the fire: a flame lit.</summary>
    public static void FlameLit(FireSpreaderSphere sphere, Vector3 at) => Record(Lit, sphere, at);

    /// <summary>Host, from the fire: a flame went away (put out, burnt out, or the fire was cleared).</summary>
    public static void FlameGone(FireSpreaderSphere sphere, Vector3 at) => Record(Gone, sphere, at);

    /// <summary>Host, from the fire: the whole fire in a room is out.</summary>
    public static void FireOut(FireSpreader spreader)
    {
        if (!CanRecord())
            return;
        byte room = (byte)spreader.currentLocation;
        // The game clears a fire from inside its own clearing; once is enough.
        for (int i = 0; i < Ops.Count; i++)
        {
            if (Ops[i].Room == room && Ops[i].Kind == Out)
                return;
        }
        Ops.Add(new Op { Kind = Out, Room = room });
    }

    /// <summary>Host, every frame after the others: sends the frame's changes, and now and then the whole picture.</summary>
    public static void HostTick()
    {
        Flush();
        float now = Time.time;
        if (!_again && now < _resyncAt || Locator.rooms == null)
            return;
        _again = false;
        _resyncAt = now + ResyncSeconds;
        foreach (var pair in Locator.rooms)
        {
            var spreader = FireRooms.SpreaderIn(pair.Key);
            if (spreader == null)
                continue;
            FireRooms.Collect(spreader, Flames);
            SendSnapshot(pair.Key);
        }
    }

    /// <summary>Client, from FireParticle.Kill: a flame here went out by someone's doing.</summary>
    public static void FlameOut(FireParticle flame)
    {
        if (Matching || !WorldSide.Running || !WorldSide.IsClient || flame == null || flame.fireSphere == null || flame.fireSphere.spreader == null)
            return;
        var w = Outbox.Begin(MessageIds.WorldFireOut);
        w.Put((byte)flame.fireSphere.spreader.currentLocation);
        PutPosition(w, flame.transform.position);
        Outbox.ToHost(w, Channel.Events);
    }

    internal static void PutPosition(NetDataWriter w, Vector3 p)
    {
        w.Put(p.x);
        w.Put(p.y);
        w.Put(p.z);
    }

    private static bool CanRecord() => WorldSide.Running && WorldSide.IsHost && !Matching;

    private static void Record(byte kind, FireSpreaderSphere sphere, Vector3 at)
    {
        if (!CanRecord() || sphere == null || sphere.spreader == null)
            return;
        Ops.Add(new Op { Kind = kind, Room = (byte)sphere.spreader.currentLocation, X = Quantize(at.x), Y = Quantize(at.y), Z = Quantize(at.z) });
    }

    private static void Flush()
    {
        int sent = 0;
        while (sent < Ops.Count)
        {
            int n = Mathf.Min(MaxOpsPerMessage, Ops.Count - sent);
            var w = Outbox.Begin(MessageIds.WorldFireDelta);
            w.Put((ushort)n);
            for (int i = sent; i < sent + n; i++)
            {
                var op = Ops[i];
                w.Put(op.Kind);
                w.Put(op.Room);
                if (op.Kind == Out)
                    continue;
                w.Put(op.X);
                w.Put(op.Y);
                w.Put(op.Z);
            }
            Outbox.ToPlayers(w, Channel.Events);
            sent += n;
        }
        Ops.Clear();
    }

    private static void SendSnapshot(Location room)
    {
        var w = Outbox.Begin(MessageIds.WorldFire);
        w.Put((byte)room);
        w.Put((ushort)Flames.Count);
        foreach (var p in Flames)
            PutPosition(w, p);
        Outbox.ToPlayers(w, Channel.Events);
    }

    private static short Quantize(float metres) => (short)Mathf.Clamp(Mathf.RoundToInt(metres * 100f), short.MinValue, short.MaxValue);

    private static void OnFireOut(in Sender from, NetDataReader reader)
    {
        var room = (Location)reader.GetByte();
        var at = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        var spreader = WorldSide.IsHost && WorldSide.Running ? FireRooms.SpreaderIn(room) : null;
        if (spreader != null)
            FireRooms.Nearest(spreader, at, 0.5f)?.Kill();
    }
}
