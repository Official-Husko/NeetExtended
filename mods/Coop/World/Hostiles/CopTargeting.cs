using HarmonyLib;
using UnityEngine;

namespace Coop.World.Hostiles;

/// <summary>
/// Host: the cop van's agents with more than one player in the house. A player they spot becomes
/// the nearest player who's up; they shoot their one target until it's down or dead (a downed
/// player counts as a kill), then walk back to the van and leave, like the game's agents after a
/// kill. A hitman's agents (<see cref="Hitman"/>) skip the search and go straight for their target.
/// </summary>
internal static class CopTargeting
{
    // A nearer player only takes over a spotted target when this much closer.
    private const float SwitchMargin = 1.5f;

    // How close a cop has to be to knock something out of a hand (BrainAgentCop.HasNEETLineOfSight).
    private const float DisarmReach = 1.7f;
    private const float DisarmEvery = 0.5f;

    private static readonly AccessTools.FieldRef<BrainAgentCop, RaycastHit> LastHit =
        AccessTools.FieldRefAccess<BrainAgentCop, RaycastHit>("hit");

    private static readonly float[] NextDisarm = new float[byte.MaxValue + 1];

    private static readonly System.Func<BrainAgentCop, NodeState> GoToVan =
        AccessTools.MethodDelegate<System.Func<BrainAgentCop, NodeState>>(AccessTools.Method(typeof(BrainAgentCop), "SetVanDestination"));

    private static bool Hosting => WorldSide.Running && WorldSide.IsHost;

    /// <summary>Before CheckForNeet: a hitman doesn't search, it knows who it's after.</summary>
    public static bool BeforeScan(BrainAgentCop cop, ref NodeState result)
    {
        if (!Hosting || !Hitman.Hunts(cop, out byte slot))
            return true;
        // The agent's own Start picks a raid's patience; a hitman doesn't give up on the way.
        cop.leaveTimer = Mathf.Max(cop.leaveTimer, Hitman.AgentPatience);
        if (!Targets.IsAlive(slot))
        {
            Kill(cop);
            result = NodeState.Failure;
            return false;
        }
        cop.moveTgt.Obj = Targets.BodyOf(slot);
        cop.spottedNEET = true;
        result = NodeState.Success;
        return false;
    }

    /// <summary>After CheckForNeet: a spotted player becomes the nearest player who's up (by their whole body, not a hand).</summary>
    public static void AfterScan(BrainAgentCop cop, ref NodeState result)
    {
        if (!Hosting || result != NodeState.Success || Hitman.Hunts(cop, out _))
            return;
        var target = Pick(cop.transform.position, cop.moveTgt.Obj);
        if (target == cop.moveTgt.Obj)
            return;
        if (target == null)
        {
            cop.spottedNEET = false;
            cop.moveTgt.Obj = null;
            result = NodeState.Failure;
            return;
        }
        cop.moveTgt.Obj = target;
    }

    /// <summary>Before HasNEETLineOfSight, every step of a chase: a target that went down is a kill; one that's gone is dropped.</summary>
    public static bool BeforeSight(BrainAgentCop cop, ref NodeState result)
    {
        if (!Hosting || !cop.spottedNEET)
            return true;
        var target = cop.moveTgt.Obj;
        if (target == null)
        {
            // Destroyed (a player who left): look again, or (a hitman) go home.
            if (Hitman.Hunts(cop, out _))
                Kill(cop);
            else
                cop.spottedNEET = false;
            result = NodeState.Failure;
            return false;
        }
        byte slot = Targets.SlotOf(target);
        if (slot == WorldSide.None || Targets.IsAlive(slot))
            return true;
        Kill(cop);
        result = NodeState.Failure;
        return false;
    }

    /// <summary>
    /// After HasNEETLineOfSight: when something that isn't a door blocks the shot at close range the
    /// game knocks an item out of the target's hand; a remote player loses it in their own game.
    /// </summary>
    public static void AfterSight(BrainAgentCop cop, NodeState result)
    {
        if (!Hosting || result != NodeState.Failure || !cop.spottedNEET)
            return;
        var target = cop.moveTgt.Obj;
        byte slot = Targets.SlotOf(target);
        if (target == null || slot == WorldSide.None || slot == WorldSide.LocalSlot)
            return;
        var hit = LastHit(cop);
        if (hit.collider == null || hit.collider.GetComponent<Door>() != null || Vector3.Distance(target.transform.position, cop.transform.position) >= DisarmReach)
            return;
        float now = Time.time;
        if (now < NextDisarm[slot])
            return;
        NextDisarm[slot] = now + DisarmEvery;
        Forwarding.Forwarder.Disarmed(slot, Random.Range(0, 2));
    }

    /// <summary>Before CheckTargetDead: down counts as dead, and a target without stats can't be checked.</summary>
    public static bool BeforeDeadCheck(BrainAgentCop cop, ref NodeState result)
    {
        if (!Hosting)
            return true;
        var target = cop.moveTgt.Obj;
        byte slot = Targets.SlotOf(target);
        if (target != null && slot != WorldSide.None && Targets.IsDown(slot))
        {
            Kill(cop);
            result = NodeState.Success;
            return false;
        }
        if (target == null || !target.TryGetComponent(out ActorStats _))
        {
            result = NodeState.Failure;
            return false;
        }
        return true;
    }

    /// <summary>The game's kill: count it, forget the target, everyone back to the van.</summary>
    public static void Kill(BrainAgentCop cop)
    {
        cop.killCount++;
        cop.spottedNEET = false;
        cop.moveTgt.Obj = null;
        GoToVan(cop);
    }

    /// <summary>
    /// What an enemy at <paramref name="from"/> should go after instead of <paramref name="spotted"/>:
    /// itself when it isn't a player (an NPC), else the nearest player who's up (null when nobody is).
    /// </summary>
    public static GameObject Pick(Vector3 from, GameObject spotted)
    {
        byte slot = Targets.SlotOf(spotted);
        if (slot == WorldSide.None)
            return spotted;
        var nearest = Targets.NearestAlive(from, out _);
        if (nearest == null)
            return null;
        var body = Targets.BodyOf(slot);
        if (!Targets.IsAlive(slot) || body == null)
            return nearest;
        float toSpotted = Vector3.Distance(from, body.transform.position);
        float toNearest = Vector3.Distance(from, nearest.transform.position);
        return toNearest + SwitchMargin < toSpotted ? nearest : body;
    }
}
