using Coop.World.Objects;
using UnityEngine;
using UnityEngine.Animations;

namespace Coop.World.Ownership;

/// <summary>
/// Owner side: notices bodies that stopped moving. Their final pose goes out reliably (a lost stream
/// packet can't leave them slightly off elsewhere), and a client hands them back to the host, unless
/// it still holds them, touches them or they ride on something (worn, boxed, stacked).
/// </summary>
internal static class RestWatcher
{
    private const float Interval = 0.25f;
    private const float RestAfter = 0.6f;
    private const float TouchGrace = 1f;

    private static float _next;

    public static void FixedTick()
    {
        float now = Time.time;
        if (now < _next)
            return;
        _next = now + Interval;

        bool host = WorldSide.IsHost;
        var all = NetObjects.All;
        for (int i = all.Count - 1; i >= 0; i--)
        {
            var o = all[i];
            if (!o.Moving || o.IsReplica || o.PosedByHost || !o.Alive)
                continue;
            if (!host && !o.OwnedHere)
                continue;
            if (o.Holder != WorldSide.None || now - o.LastChange < RestAfter)
                continue;

            o.SentPosition = o.Tf.position;
            o.SentRotation = o.Tf.rotation;
            if (host)
            {
                o.Moving = false;
                OwnershipNet.QueueSettled(o, release: false);
                continue;
            }
            if (now - o.LastTouched < TouchGrace || RidesOnSomething(o))
                continue;
            o.Moving = false;
            OwnershipNet.QueueSettled(o, release: true);
            Owners.LocalRest(o);
        }
    }

    // Worn by someone (a mask on a head) or frozen into something (a box, a stack). Doors hang in
    // their frames too, but those don't move them.
    private static bool RidesOnSomething(NetObject o)
    {
        var parent = o.Tf.parent;
        if (parent != null && parent.GetComponentInParent<Brain>() != null)
            return true;
        var constraint = o.Go.GetComponent<ParentConstraint>();
        return constraint != null && constraint.constraintActive;
    }
}
