using Coop.World.Objects;
using Coop.World.Spawning;
using UnityEngine;

namespace Coop.World.Ownership;

/// <summary>
/// Who simulates each body. The host owns everything until a player picks something up or bumps
/// into it; it comes back to the host once it rests. Clients change their own table straight away
/// (it feels instant) and the host's answer corrects them; the host settles every conflict.
/// </summary>
internal static class Owners
{
    // This game's player grabbed it.
    public static void LocalGrab(NetObject o)
    {
        byte me = WorldSide.LocalSlot;
        bool changed = o.Owner != me || o.Holder != me;
        o.Owner = me;
        o.Holder = me;
        ReplicaBody.MakeOwned(o);
        if (WorldSide.IsHost)
        {
            if (changed)
                OwnershipNet.QueueOwner(o);
        }
        else
        {
            BrainHandoff.TakenHere(o);
            OwnershipNet.QueueClaim(o, holding: true);
        }
    }

    // This game's player let go (dropped, threw, put it on).
    public static void LocalRelease(NetObject o)
    {
        if (o.Holder != WorldSide.LocalSlot)
            return;
        o.Holder = WorldSide.None;
        if (WorldSide.IsHost)
        {
            OwnershipNet.QueueOwner(o);
            return;
        }
        BrainHandoff.ReleasedHere(o);
        OwnershipNet.QueueClaim(o, holding: false);
    }

    // This game's player walked into a replica: take it so it can be pushed.
    public static void LocalTouch(NetObject o)
    {
        o.Owner = WorldSide.LocalSlot;
        o.Holder = WorldSide.None;
        ReplicaBody.MakeOwned(o);
        if (WorldSide.IsHost)
            OwnershipNet.QueueOwner(o);
        else
            OwnershipNet.QueueClaim(o, holding: false);
    }

    // Host: an NPC picked up something a client was simulating.
    public static void HostTake(NetObject o)
    {
        if (o.Owner == WorldSide.HostSlot && o.Holder == WorldSide.None)
            return;
        bool fromClient = o.Owner != WorldSide.HostSlot;
        o.Owner = WorldSide.HostSlot;
        o.Holder = WorldSide.None;
        ReplicaBody.MakeOwned(o);
        if (fromClient)
            BrainHandoff.Resume(o);
        OwnershipNet.QueueOwner(o);
    }

    // Client: it came to rest here; the host simulates it from now on.
    public static void LocalRest(NetObject o)
    {
        o.Owner = WorldSide.HostSlot;
        o.Holder = WorldSide.None;
        ReplicaBody.MakeReplica(o);
        BrainHandoff.HandedBack(o);
    }

    /// <summary>Host: a client wants to simulate <paramref name="o"/>.</summary>
    public static void OnClaim(byte slot, NetObject o, bool holding)
    {
        if (o.Holder != WorldSide.None && o.Holder != slot)
        {
            // Someone else has it in their hand: the claimer loses.
            OwnershipNet.SendOwnerTo(slot, o);
            return;
        }
        bool wasHosts = o.Owner == WorldSide.HostSlot;
        o.Owner = slot;
        o.Holder = holding ? slot : WorldSide.None;
        ReplicaBody.MakeReplica(o);
        if (wasHosts)
            BrainHandoff.Pause(o);
        OwnershipNet.QueueOwner(o);
    }

    /// <summary>Host: a body a client simulated came to rest; with <paramref name="release"/> the host takes it back.</summary>
    public static void OnSettled(byte slot, NetObject o, bool release, Vector3 position, Quaternion rotation, byte flags)
    {
        if (o.Owner != slot)
            return;
        ReplicaBody.Place(o, position, rotation);
        o.Buffer.Clear();
        if (!release)
            return;
        o.GameKinematic = (flags & BodyFlags.Kinematic) != 0;
        o.Owner = WorldSide.HostSlot;
        o.Holder = WorldSide.None;
        ReplicaBody.MakeOwned(o);
        BrainHandoff.Resume(o);
        OwnershipNet.QueueOwner(o);
    }

    /// <summary>Client: the host's word on who has <paramref name="o"/>.</summary>
    public static void OnOwner(NetObject o, byte owner, byte holder)
    {
        byte me = WorldSide.LocalSlot;
        bool ownerChanged = o.Owner != owner;
        o.Owner = owner;
        o.Holder = holder;
        if (o.Brain != null && !BrainHandoff.Holdable(o))
            return;
        if (owner == me)
        {
            ReplicaBody.MakeOwned(o);
            BrainHandoff.TakenHere(o);
            return;
        }
        if (!o.IsReplica)
        {
            DropIfHeldHere(o);
            ReplicaBody.MakeReplica(o);
            BrainHandoff.HandedBack(o);
        }
        else if (ownerChanged)
        {
            // The old owner's stream has ended; the new owner's starts on its own clock.
            ReplicaBody.SettleAtNewest(o);
        }
    }

    /// <summary>Lets go of an item this game's player holds, without telling anyone (we lost it).</summary>
    public static void DropIfHeldHere(NetObject o)
    {
        var usable = o.Usable;
        var player = WorldSide.LocalPlayer;
        if (usable == null || player == null || usable.player != player)
            return;
        using (SpawnContext.Quiet())
            usable.DropHand();
    }
}
