using System.Collections.Generic;
using Coop.Net;
using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Npcs;

/// <summary>
/// Client: plays the host's NPC poses back on the puppets, on the host's clock like everything else
/// the host moves. Position and rotation go through the kinematic body every physics step; animator,
/// hands and showing/hiding follow the sample being shown.
/// </summary>
internal static class NpcPoseReceiver
{
    private const float SnapDistance = 3f;
    private const float IdleAfter = 1f;

    private static readonly List<NetObject> Active = new List<NetObject>(32);

    public static void Init() => Messages.On(MessageIds.WorldNpcPoses, OnPoses);

    public static void Clear()
    {
        foreach (var o in Active)
            o.Rig.Driving = false;
        Active.Clear();
    }

    private static void OnPoses(in Sender from, NetDataReader reader)
    {
        if (WorldSide.IsHost || !WorldSide.Running)
            return;
        float time = reader.GetFloat();
        RemoteClock.Observe(WorldSide.HostSlot, time);
        while (reader.AvailableBytes >= 5 + NpcPose.Size)
        {
            var r = NetRef.Read(reader);
            var pose = NpcPose.Read(reader);
            var o = NetObjects.Find(r);
            if (o?.Rig == null || !o.PosedByHost || !o.Alive)
                continue;
            if (o.Rig.Poses.Add(time, pose) && !o.Rig.Driving)
            {
                o.Rig.Driving = true;
                Active.Add(o);
            }
        }
    }

    public static void FixedTick()
    {
        float play = RemoteClock.PlaybackTime(WorldSide.HostSlot);
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            var o = Active[i];
            var rig = o.Rig;
            if (!o.Alive || o.Index < 0 || !o.PosedByHost)
            {
                Drop(i);
                continue;
            }
            rig.Poses.Find(play, out int older, out int newer, out float t);
            ref var a = ref rig.Poses.At(older);
            ref var b = ref rig.Poses.At(newer);
            Place(o, Vector3.LerpUnclamped(a.Position, b.Position, t), Quaternion.SlerpUnclamped(a.Rotation, b.Rotation, t));
            if (play - rig.Poses.NewestTime > IdleAfter)
                Drop(i);
        }
    }

    /// <summary>Animator and hands, from the sample being shown; only when it differs from what's up.</summary>
    public static void LateTick()
    {
        float play = RemoteClock.PlaybackTime(WorldSide.HostSlot);
        for (int i = 0; i < Active.Count; i++)
        {
            var o = Active[i];
            var rig = o.Rig;
            if (!o.Alive)
                continue;
            rig.Poses.Find(play, out int older, out _, out _);
            ref var pose = ref rig.Poses.At(older);
            bool active = (pose.Flags & NpcPose.Active) != 0;
            if (o.Go.activeSelf != active)
                o.Go.SetActive(active);
            if (!active)
                continue;
            var shown = rig.Shown;
            if (pose.Bools != shown.Bools || pose.Dancing != shown.Dancing || pose.Speed != shown.Speed || pose.Flags != shown.Flags
                || pose.Kicks != shown.Kicks)
                rig.Show(pose);
        }
    }

    private static void Place(NetObject o, Vector3 position, Quaternion rotation)
    {
        var body = o.Body;
        if (body == null || (body.position - position).sqrMagnitude > SnapDistance * SnapDistance)
        {
            o.Tf.SetPositionAndRotation(position, rotation);
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
            }
            return;
        }
        if (!body.isKinematic)
            body.isKinematic = true;
        body.MovePosition(position);
        body.MoveRotation(rotation);
    }

    private static void Drop(int index)
    {
        Active[index].Rig.Driving = false;
        int last = Active.Count - 1;
        Active[index] = Active[last];
        Active.RemoveAt(last);
    }
}
