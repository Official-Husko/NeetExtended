using Coop.World.Objects;

namespace Coop.World.Ownership;

/// <summary>Host: what a leaving player simulated or held comes back to the host where it was last seen.</summary>
internal static class Departures
{
    public static void PlayerLeft(byte slot)
    {
        var all = NetObjects.All;
        for (int i = 0; i < all.Count; i++)
        {
            var o = all[i];
            if (o.Owner == slot)
            {
                o.Owner = WorldSide.HostSlot;
                o.Holder = WorldSide.None;
                ReplicaBody.SettleAtNewest(o);
                ReplicaBody.MakeOwned(o);
                BrainHandoff.Resume(o);
                OwnershipNet.QueueOwner(o);
            }
            else if (o.Holder == slot)
            {
                o.Holder = WorldSide.None;
                OwnershipNet.QueueOwner(o);
            }
        }
    }

    /// <summary>Host: the session is over; every replica is simulated here again.</summary>
    public static void TakeEverythingBack()
    {
        var all = NetObjects.All;
        for (int i = 0; i < all.Count; i++)
        {
            var o = all[i];
            if (!o.IsReplica || !o.Alive)
                continue;
            o.Owner = WorldSide.HostSlot;
            o.Holder = WorldSide.None;
            ReplicaBody.SettleAtNewest(o);
            ReplicaBody.MakeOwned(o);
        }
    }
}
