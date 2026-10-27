using Coop.World.Objects;
using UnityEngine;

namespace Coop.World.Ownership;

/// <summary>
/// Client: a replica is always kinematic, so one that turned dynamic was freed by this game itself:
/// clothes taken off without a hand, what a dead player had falling where they died, anything the
/// game lets go of without the hand events. This game simulates it anyway, so it claims it like an
/// item its player bumped into; otherwise the host never hears it moved, and its stream and smoothing
/// keep pulling at a body that is also falling here.
/// </summary>
internal static class FreedReplicas
{
    private const float Interval = 0.25f;

    private static float _next;

    public static void Tick()
    {
        if (Time.time < _next)
            return;
        _next = Time.time + Interval;
        var all = NetObjects.All;
        for (int i = 0; i < all.Count; i++)
        {
            var o = all[i];
            if (o.IsReplica && o.Body != null && !o.Body.isKinematic && o.Alive)
                Owners.LocalTouch(o);
        }
    }
}
