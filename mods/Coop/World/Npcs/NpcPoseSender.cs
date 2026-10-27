using Coop.Net;
using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Npcs;

/// <summary>
/// Host: 15 times a second, the NPC rigs whose pose changed. Read late in the frame, after the
/// animator parameters were set.
///
/// WorldNpcPoses: sender time, then entries until the packet ends: ref, <see cref="NpcPose"/>.
/// </summary>
internal static class NpcPoseSender
{
    private const float Interval = 1f / 15f;
    private const int PacketBudget = 1000;

    private static float _next;

    public static void LateTick()
    {
        float now = Time.time;
        if (now < _next)
            return;
        _next = now + Interval;

        NetDataWriter w = null;
        var rigs = NetObjects.Rigs;
        for (int i = 0; i < rigs.Count; i++)
        {
            var o = rigs[i];
            var rig = o.Rig;
            if (rig == null || !o.PosedByHost || !o.Alive)
                continue;
            var pose = rig.Read(o.Tf);
            if (rig.HasSent && pose.SameAs(rig.Sent))
                continue;
            rig.Sent = pose;
            rig.HasSent = true;
            if (w == null)
            {
                w = Outbox.Begin(MessageIds.WorldNpcPoses);
                w.Put(now);
            }
            o.Ref.Write(w);
            pose.Write(w);
            if (w.Length > PacketBudget)
            {
                Outbox.ToPlayers(w, Channel.WorldStream);
                w = null;
            }
        }
        if (w != null)
            Outbox.ToPlayers(w, Channel.WorldStream);
    }

    /// <summary>Makes every rig go out again on the next tick (a player joined or resynced).</summary>
    public static void SendAllAgain()
    {
        foreach (var o in NetObjects.Rigs)
            o.Rig.HasSent = false;
    }
}
