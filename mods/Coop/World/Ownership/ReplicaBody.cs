using Coop.World.Objects;
using Coop.World.State;
using Coop.World.Streaming;
using UnityEngine;

namespace Coop.World.Ownership;

/// <summary>
/// Switches a body between "simulated here" (exactly as the game made it) and "replica" (kinematic,
/// smoothed, moved by its owner's stream). What the game wants (<see cref="NetObject.GameKinematic"/>)
/// is kept aside so it comes back when the body is simulated here again.
/// </summary>
internal static class ReplicaBody
{
    public static void MakeReplica(NetObject o)
    {
        if (o.IsReplica)
            return;
        StateSync.Flush(o);
        if (o.Body != null)
        {
            o.GameKinematic = o.Body.isKinematic;
            o.Body.isKinematic = true;
            o.Body.interpolation = RigidbodyInterpolation.Interpolate;
        }
        o.IsReplica = true;
        o.Moving = false;
        o.Buffer.Clear();
    }

    public static void MakeOwned(NetObject o)
    {
        if (!o.IsReplica)
            return;
        o.IsReplica = false;
        ReplicaDriver.Stop(o);
        if (o.Body != null)
        {
            // No game body uses interpolation; the game's kinematic wish comes back. A replica is
            // always kinematic, so a dynamic one means the game has just freed it (taking clothes
            // off, a fuse popping out) on its way into a hand: that stands.
            o.Body.interpolation = RigidbodyInterpolation.None;
            if (o.Body.isKinematic)
                o.Body.isKinematic = o.GameKinematic;
        }
        o.SentPosition = o.Tf.position;
        o.SentRotation = o.Tf.rotation;
        o.SentFlags = BodyFlags.Of(o);
        NetObjects.MarkMoving(o, Time.time);
    }

    /// <summary>Jumps a replica to where its stream ended (used when it changes owner) and stops playback.</summary>
    public static void SettleAtNewest(NetObject o)
    {
        if (o.Buffer.TryNewest(out var position, out var rotation))
            Place(o, position, rotation);
        o.Buffer.Clear();
        ReplicaDriver.Stop(o);
    }

    public static void Place(NetObject o, Vector3 position, Quaternion rotation)
    {
        o.Tf.SetPositionAndRotation(position, rotation);
        if (o.Body != null)
        {
            o.Body.position = position;
            o.Body.rotation = rotation;
        }
    }
}
