using Coop.Net;
using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Streaming;

/// <summary>
/// Owner side: 20 times a second, the bodies this game simulates that moved, changed flags or scale
/// since they were last sent. Packets stay under the size that goes out unfragmented; a tick sends at
/// most <see cref="MaxEntries"/>, starting where the last full tick stopped, so a burst of debris
/// catches up over a few ticks instead of flooding.
///
/// WorldBodies: owner slot, sender time, then entries until the packet ends:
/// ref, flags, position (3 floats), rotation (4 floats), [scale (3 floats) when flagged].
/// </summary>
internal static class BodyStream
{
    private const float Interval = 0.05f;
    private const int MaxEntries = 120;
    private const int PacketBudget = 1000;
    private const float MoveSq = 0.001f * 0.001f;
    private const float TurnDot = 0.999999f;
    private const float ScaleSq = 0.0001f * 0.0001f;
    private const float OutOfWorld = -1000f;

    private static float _next;
    private static int _cursor;

    public static void FixedTick()
    {
        float now = Time.time;
        if (now < _next)
            return;
        _next = now + Interval;

        var all = NetObjects.All;
        int count = all.Count;
        if (count == 0)
            return;
        bool host = WorldSide.IsHost;
        byte me = WorldSide.LocalSlot;
        NetDataWriter w = null;
        int sent = 0;
        int start = _cursor % count;
        _cursor = 0;

        for (int k = 0; k < count; k++)
        {
            int i = (start + k) % count;
            var o = all[i];
            if (o.IsReplica || o.Rig != null || (!host && o.Owner != me) || !o.Alive)
                continue;
            if (!Changed(o, out byte flags, out bool scaled))
                continue;
            if (sent == MaxEntries)
            {
                _cursor = i;
                break;
            }
            w ??= Begin(me, now);
            Write(w, o, flags, scaled);
            o.LastChange = now;
            o.Moving = true;
            sent++;
            if (w.Length > PacketBudget)
            {
                Outbox.ToPlayers(w, Channel.WorldStream);
                w = null;
            }
        }
        if (w != null)
            Outbox.ToPlayers(w, Channel.WorldStream);
    }

    private static NetDataWriter Begin(byte slot, float now)
    {
        var w = Outbox.Begin(MessageIds.WorldBodies);
        w.Put(slot);
        w.Put(now);
        return w;
    }

    private static bool Changed(NetObject o, out byte flags, out bool scaled)
    {
        flags = BodyFlags.Of(o);
        // A sleeping body that kept its flags hasn't moved since it was last looked at.
        if (o.Body != null && !o.Body.isKinematic && flags == o.SentFlags && !o.Moving && o.Body.IsSleeping())
        {
            scaled = false;
            return false;
        }
        var t = o.Tf;
        var position = t.position;
        // Fallen out of the world (some worn clothes keep falling forever): nothing left to show.
        if (position.y < OutOfWorld && o.SentPosition.y < OutOfWorld && flags == o.SentFlags)
        {
            scaled = false;
            return false;
        }
        var rotation = t.rotation;
        var scale = t.localScale;
        scaled = (scale - o.SentScale).sqrMagnitude > ScaleSq;
        bool moved = (position - o.SentPosition).sqrMagnitude > MoveSq
            || Mathf.Abs(Quaternion.Dot(rotation, o.SentRotation)) < TurnDot;
        if (!moved && !scaled && flags == o.SentFlags)
            return false;
        o.SentPosition = position;
        o.SentRotation = rotation;
        o.SentFlags = flags;
        if (scaled)
            o.SentScale = scale;
        return true;
    }

    private static void Write(NetDataWriter w, NetObject o, byte flags, bool scaled)
    {
        o.Ref.Write(w);
        w.Put(scaled ? (byte)(flags | BodyFlags.Scale) : flags);
        var p = o.SentPosition;
        var r = o.SentRotation;
        w.Put(p.x);
        w.Put(p.y);
        w.Put(p.z);
        w.Put(r.x);
        w.Put(r.y);
        w.Put(r.z);
        w.Put(r.w);
        if (scaled)
        {
            var s = o.SentScale;
            w.Put(s.x);
            w.Put(s.y);
            w.Put(s.z);
        }
    }
}
