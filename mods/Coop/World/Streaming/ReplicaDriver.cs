using System.Collections.Generic;
using Coop.Net;
using Coop.World.Objects;
using UnityEngine;

namespace Coop.World.Streaming;

/// <summary>
/// Moves replicas that have stream data to play back, every physics step. Kinematic bodies go by
/// MovePosition so they still push the local player and loose items; things without a body are placed
/// directly. A replica drops off the list once playback has caught up and it has stayed put.
/// </summary>
internal static class ReplicaDriver
{
    // Further than this from where it should be (a teleport, a respawn) it jumps instead of sliding.
    private const float SnapDistance = 3f;
    private const float IdleAfter = 0.5f;

    private static readonly List<NetObject> Active = new List<NetObject>(256);

    public static int Count => Active.Count;

    public static void Push(NetObject o)
    {
        if (o.Driving)
            return;
        o.Driving = true;
        Active.Add(o);
    }

    /// <summary>Takes it off the list (lazily, on the next step).</summary>
    public static void Stop(NetObject o) => o.Driving = false;

    public static void Clear()
    {
        foreach (var o in Active)
            o.Driving = false;
        Active.Clear();
    }

    public static void FixedTick()
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            var o = Active[i];
            if (!o.Driving || o.Index < 0 || !o.IsReplica || o.PosedByHost || o.OnPuppet || o.Buffer.IsEmpty || !o.Alive)
            {
                Drop(o, i);
                continue;
            }
            float play = RemoteClock.PlaybackTime(o.Owner);
            o.Buffer.Sample(play, out var position, out var rotation);
            Move(o, position, rotation);
            if (o.Buffer.IsSettled(play, IdleAfter))
                Drop(o, i);
        }
    }

    private static void Move(NetObject o, Vector3 position, Quaternion rotation)
    {
        var body = o.Body;
        if (body == null)
        {
            o.Tf.SetPositionAndRotation(position, rotation);
            return;
        }
        // Game code that switches a replica back to dynamic is overruled; the owner's flags say what it wants.
        if (!body.isKinematic)
            body.isKinematic = true;
        if ((body.position - position).sqrMagnitude > SnapDistance * SnapDistance)
        {
            ReplicaPlace(o, position, rotation);
            return;
        }
        body.MovePosition(position);
        body.MoveRotation(rotation);
    }

    private static void ReplicaPlace(NetObject o, Vector3 position, Quaternion rotation)
    {
        o.Tf.SetPositionAndRotation(position, rotation);
        o.Body.position = position;
        o.Body.rotation = rotation;
    }

    private static void Drop(NetObject o, int index)
    {
        o.Driving = false;
        int last = Active.Count - 1;
        Active[index] = Active[last];
        Active.RemoveAt(last);
    }
}
