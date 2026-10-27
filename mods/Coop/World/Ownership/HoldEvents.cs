using Coop.World.Objects;
using Coop.World.Spawning;
using UnityEngine;

namespace Coop.World.Ownership;

/// <summary>What picking up and letting go mean for ownership (see <see cref="Patches.HoldPatches"/>).</summary>
internal static class HoldEvents
{
    private const float Reach = 2.1f;

    private static int _usableLayer = -1;

    public static void Grabbed(Usable usable, GameObject holder)
    {
        if (!WorldSide.Running || SpawnContext.Silent || holder == null)
            return;
        var o = NetObjects.Find(usable.gameObject);
        if (o == null || o.Brain != null)
            return;
        if (holder == WorldSide.LocalPlayer)
            Owners.LocalGrab(o);
        else if (WorldSide.IsHost)
            Owners.HostTake(o); // An NPC took it.
    }

    public static void Released(Usable usable, GameObject previousHolder, bool delay)
    {
        if (!WorldSide.Running || delay || SpawnContext.Silent || previousHolder == null || previousHolder != WorldSide.LocalPlayer)
            return;
        var o = NetObjects.Find(usable.gameObject);
        if (o != null && o.Brain == null)
            Owners.LocalRelease(o);
    }

    /// <summary>
    /// Pickup(Hands) looks for its target inside Pickup(Hands, GameObject); this does the same raycast
    /// first and hands the result on, so the guard can see the target and the game doesn't cast twice.
    /// </summary>
    public static bool MayPickUp(Player_Abilities hands, ref GameObject target)
    {
        if (!WorldSide.Running)
            return true;
        if (target == null && !Aim(hands, out target))
            return true;
        var o = NetObjects.Find(target);
        if (o != null && o.HeldByOther)
            return false;
        // NPC bodies, rats and cats keep running on the host; handing them to a client comes later.
        if (WorldSide.IsClient && target.GetComponent<Brain>() != null)
            return false;
        return true;
    }

    private static bool Aim(Player_Abilities hands, out GameObject target)
    {
        target = null;
        var camera = hands.mainCamera;
        if (camera == null)
            return false;
        if (_usableLayer < 0)
            _usableLayer = LayerMask.NameToLayer("Usable");
        var t = camera.transform;
        if (!Physics.Raycast(t.position, t.forward, out var hit, Reach, hands.usablesMask, QueryTriggerInteraction.Ignore))
            return false;
        if (hit.collider.gameObject.layer != _usableLayer)
            return false;
        target = hit.collider.gameObject;
        return true;
    }
}
