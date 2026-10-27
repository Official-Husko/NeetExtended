using System.Collections.Generic;
using Coop.Net;
using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Ownership;

/// <summary>
/// The ownership messages. Changes are queued during the frame and go out once, after the frame's
/// spawns, so a claim never arrives before the thing it's about.
/// - WorldClaim (client to host): "I simulate it now", with whether it's in my hand.
/// - WorldOwner (host to all, or to one loser): who owns and who holds each body.
/// - WorldSettled (owner to all through the host): a body came to rest here, final pose; "release" hands it to the host.
/// </summary>
internal static class OwnershipNet
{
    private struct Claim
    {
        public NetRef Ref;
        public bool Holding;
    }

    private struct Settle
    {
        public NetObject Object;
        public bool Release;
    }

    private static readonly List<Claim> Claims = new List<Claim>();
    private static readonly List<NetObject> OwnerChanges = new List<NetObject>();
    private static readonly List<Settle> Settles = new List<Settle>();

    public static void Init()
    {
        Messages.On(MessageIds.WorldClaim, OnClaim);
        Messages.On(MessageIds.WorldOwner, OnOwner);
        Messages.On(MessageIds.WorldSettled, OnSettled);
    }

    public static void QueueClaim(NetObject o, bool holding) => Claims.Add(new Claim { Ref = o.Ref, Holding = holding });

    public static void QueueOwner(NetObject o)
    {
        if (!OwnerChanges.Contains(o))
            OwnerChanges.Add(o);
    }

    public static void QueueSettled(NetObject o, bool release) => Settles.Add(new Settle { Object = o, Release = release });

    public static void Clear()
    {
        Claims.Clear();
        OwnerChanges.Clear();
        Settles.Clear();
    }

    public static void Flush()
    {
        if (Claims.Count > 0)
        {
            var w = Outbox.Begin(MessageIds.WorldClaim);
            w.Put((ushort)Claims.Count);
            foreach (var claim in Claims)
            {
                claim.Ref.Write(w);
                w.Put(claim.Holding);
            }
            Outbox.ToHost(w, Channel.Events);
            Claims.Clear();
        }
        if (Settles.Count > 0)
            FlushSettles();
        if (OwnerChanges.Count > 0)
        {
            var w = Outbox.Begin(MessageIds.WorldOwner);
            w.Put((ushort)OwnerChanges.Count);
            foreach (var o in OwnerChanges)
                WriteOwner(w, o);
            Outbox.ToPlayers(w, Channel.Events);
            OwnerChanges.Clear();
        }
    }

    /// <summary>Host: tells one player how things stand with <paramref name="o"/> (a lost claim).</summary>
    public static void SendOwnerTo(byte slot, NetObject o)
    {
        var w = Outbox.Begin(MessageIds.WorldOwner);
        w.Put((ushort)1);
        WriteOwner(w, o);
        Outbox.ToSlot(slot, w, Channel.Events);
    }

    private static void WriteOwner(NetDataWriter w, NetObject o)
    {
        o.Ref.Write(w);
        w.Put(o.Owner);
        w.Put(o.Holder);
    }

    private static void FlushSettles()
    {
        var w = Outbox.Begin(MessageIds.WorldSettled);
        w.Put(WorldSide.LocalSlot);
        w.Put(Time.time);
        w.Put((ushort)Settles.Count);
        foreach (var settle in Settles)
        {
            var o = settle.Object;
            o.Ref.Write(w);
            w.Put(settle.Release ? (byte)(o.SentFlags | 0x80) : o.SentFlags);
            var p = o.SentPosition;
            var r = o.SentRotation;
            w.Put(p.x);
            w.Put(p.y);
            w.Put(p.z);
            w.Put(r.x);
            w.Put(r.y);
            w.Put(r.z);
            w.Put(r.w);
        }
        Outbox.ToPlayers(w, Channel.Events);
        Settles.Clear();
    }

    private static void OnClaim(in Sender from, NetDataReader reader)
    {
        if (!WorldSide.IsHost || !WorldSide.Running)
            return;
        int count = reader.GetUShort();
        for (int i = 0; i < count; i++)
        {
            var r = NetRef.Read(reader);
            bool holding = reader.GetBool();
            var o = NetObjects.Find(r);
            if (o != null && o.Rig == null && o.Brain == null)
                Owners.OnClaim(from.Slot, o, holding);
        }
    }

    private static void OnOwner(in Sender from, NetDataReader reader)
    {
        if (WorldSide.IsHost || !WorldSide.Running)
            return;
        int count = reader.GetUShort();
        for (int i = 0; i < count; i++)
        {
            var r = NetRef.Read(reader);
            byte owner = reader.GetByte();
            byte holder = reader.GetByte();
            var o = NetObjects.Find(r);
            if (o != null)
                Owners.OnOwner(o, owner, holder);
        }
    }

    private static void OnSettled(in Sender from, NetDataReader reader)
    {
        int start = reader.Position;
        int length = reader.AvailableBytes;
        byte owner = reader.GetByte();
        float time = reader.GetFloat();
        if (WorldSide.IsHost)
        {
            if (owner != from.Slot)
                return;
            Relay.Forward(MessageIds.WorldSettled, reader.RawData, start, length, Channel.Events, from.Slot);
        }
        if (!WorldSide.Running)
            return;
        RemoteClock.Observe(owner, time);
        int count = reader.GetUShort();
        for (int i = 0; i < count; i++)
        {
            var r = NetRef.Read(reader);
            byte flags = reader.GetByte();
            var position = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            var rotation = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            var o = NetObjects.Find(r);
            if (o == null || o.Rig != null)
                continue;
            bool release = (flags & 0x80) != 0;
            if (WorldSide.IsHost)
            {
                Owners.OnSettled(owner, o, release, position, rotation, flags);
            }
            else if (o.IsReplica && o.Owner == owner)
            {
                // The last pose, reliably: playback ends exactly there.
                if (o.Buffer.Add(time, position, rotation))
                    Streaming.ReplicaDriver.Push(o);
            }
        }
    }
}
