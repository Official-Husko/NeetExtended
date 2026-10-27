using Coop.World.Npcs;
using Coop.World.Objects;

namespace Coop.World.Ownership;

/// <summary>
/// Rats and the cat are animals you can pick up. Their brain runs on the host, so while someone else
/// holds or throws one, the host pauses it the way the animal's own pickup does (no walking, no
/// thinking), and wakes it with the animal's own drop once it's back. On a client the animal goes
/// back to being a puppet when it's handed back.
/// </summary>
internal static class BrainHandoff
{
    /// <summary>A brain whose body can be held like an item (not an NPC rig or a corpse).</summary>
    public static bool Holdable(NetObject o) => o.Brain != null && o.Rig == null && (o.Brain is BrainRat || o.Brain is BrainCat);

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
        if (Holdable(o) && o.Body != null)
            o.Body.useGravity = true;
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
