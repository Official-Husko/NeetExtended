using Coop.World.Npcs;
using Coop.World.Objects;

namespace Coop.World.Ownership;

/// <summary>
/// Rats, the cat and dead NPCs can be picked up. Their brain runs on the host, so while someone else
/// holds or throws one, the host pauses it the way the animal's own pickup does (no walking, no
/// thinking), and wakes it with the animal's own drop once it's back (a corpse has nothing to pause).
/// On a client it goes back to being a puppet when it's handed back; a rat in this game's player's
/// hand wriggles and bites here (<see cref="HeldAnimals"/>).
/// </summary>
internal static class BrainHandoff
{
    /// <summary>A brain whose body can be held like an item: a rat, the cat, a dead NPC.</summary>
    public static bool Holdable(NetObject o) =>
        o.Brain != null && (o.Rig == null
            ? o.Brain is BrainRat || o.Brain is BrainCat
            : o.Rig.Abilities != null && o.Rig.Abilities.dead);

    /// <summary>Host: another player has it now.</summary>
    public static void Pause(NetObject o)
    {
        if (Holdable(o) && o.Brain is Player_Abilities.IPickupTriggerable pickup)
            pickup.PickupTrigger(null);
    }

    /// <summary>Host: it's back; the animal carries on as if it was just dropped.</summary>
    public static void Resume(NetObject o)
    {
        if (Holdable(o) && o.Brain is Player_Abilities.IDropTriggerable drop)
            drop.DropTrigger();
    }

    /// <summary>Client: this game simulates it now (held or thrown); it falls like a dropped animal does.</summary>
    public static void TakenHere(NetObject o)
    {
        if (!Holdable(o))
            return;
        if (o.Body != null)
            o.Body.useGravity = true;
        HeldAnimals.Taken(o);
    }

    /// <summary>
    /// Client: let go of here. The animal's own drop just woke its walking (its brain is off here);
    /// it should fall and roll until it rests, and the host wakes it.
    /// </summary>
    public static void ReleasedHere(NetObject o)
    {
        if (!Holdable(o))
            return;
        o.Brain.StopAllCoroutines();
        if (o.Brain.TryGetComponent(out UnityEngine.AI.NavMeshAgent nav))
            nav.enabled = false;
    }

    /// <summary>Client: handed back; it's the host's puppet again.</summary>
    public static void HandedBack(NetObject o)
    {
        if (Holdable(o))
            NpcPuppets.Puppetize(o);
    }
}
