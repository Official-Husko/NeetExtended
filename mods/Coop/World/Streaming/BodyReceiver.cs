using Coop.Net;
using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Streaming;

/// <summary>
/// Takes in body streams. The host passes each client's stream on to the others first. Entries for
/// unknown things (not spawned here yet), for bodies simulated here and from anyone but the current
/// owner (a late packet after a handover) are skipped.
/// </summary>
internal static class BodyReceiver
{
    private const int EntrySize = 5 + 1 + 12 + 16;

    public static void Init() => Messages.On(MessageIds.WorldBodies, OnBodies);

    private static void OnBodies(in Sender from, NetDataReader reader)
    {
        int start = reader.Position;
        int length = reader.AvailableBytes;
        byte owner = reader.GetByte();
        float time = reader.GetFloat();
        if (WorldSide.IsHost)
        {
            // A client only speaks for itself.
            if (owner != from.Slot)
                return;
            Relay.Forward(MessageIds.WorldBodies, reader.RawData, start, length, Channel.WorldStream, from.Slot);
        }
        if (!WorldSide.Running)
            return;
        RemoteClock.Observe(owner, time);

        while (reader.AvailableBytes >= EntrySize)
        {
            var r = NetRef.Read(reader);
            byte flags = reader.GetByte();
            var position = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            var rotation = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            var scale = Vector3.one;
            if ((flags & BodyFlags.Scale) != 0)
                scale = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());

            var o = NetObjects.Find(r);
            if (o == null || !o.IsReplica || o.Owner != owner || o.PosedByHost || !o.Alive)
                continue;
            ApplyFlags(o, flags, scale);
            // Worn on a player's body: the body moves it, but where the stream says it is stays known
            // for when it comes off.
            if (o.Buffer.Add(time, position, rotation) && !o.OnPuppet)
                ReplicaDriver.Push(o);
        }
    }

    private static void ApplyFlags(NetObject o, byte flags, Vector3 scale)
    {
        o.GameKinematic = (flags & BodyFlags.Kinematic) != 0;
        bool active = (flags & BodyFlags.Active) != 0;
        if (o.Go.activeSelf != active)
            o.Go.SetActive(active);
        if ((flags & BodyFlags.Scale) != 0)
            o.Tf.localScale = scale;
    }
}
