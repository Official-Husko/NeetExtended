using HarmonyLib;
using UnityEngine;

namespace Coop.World.Hostiles;

/// <summary>
/// Host: the other things that go after players, with more than one player in the house.
/// - The hostile NPC: a spotted player becomes the nearest player who's up; once its target is
///   down, dead or gone it goes after the next one, anywhere. It faces its target, not the host.
/// - Trick-or-treaters face the nearest player.
/// - House spiders leap at the nearest player who's up within their reach (the game only looked
///   for things with a brain, which remote players' bodies don't have), and their bite's energy
///   drain reaches remote players too.
/// </summary>
internal static class NpcTargeting
{
    private const float SpiderReach = 4f;
    // What Spider_House.OnCollisionEnter takes from a bitten body's energy.
    private const float SpiderEnergyBite = -10f;

    private static readonly AccessTools.FieldRef<BrainNeetAI, bool> Spotted =
        AccessTools.FieldRefAccess<BrainNeetAI, bool>("spottedNEET");

    private static readonly AccessTools.FieldRef<BrainNeetAI, Player_Movement> Movement =
        AccessTools.FieldRefAccess<BrainNeetAI, Player_Movement>("player_Movement");

    private static bool Hosting => WorldSide.Running && WorldSide.IsHost;

    /// <summary>After BrainNeetAI.CheckForNeet (hostile): a spotted player becomes the nearest player who's up.</summary>
    public static void AfterScan(BrainNeetAI npc, ref NodeState result)
    {
        if (!Hosting || result != NodeState.Success || npc.brainType != BrainNeetAI.BrainType.Hostile)
            return;
        var target = CopTargeting.Pick(npc.transform.position, npc.pickupTgt.Obj);
        if (target == npc.pickupTgt.Obj)
            return;
        if (target == null)
        {
            Spotted(npc) = false;
            result = NodeState.Failure;
            return;
        }
        npc.pickupTgt.Obj = target;
    }

    /// <summary>Before BrainNeetAI.HasNeetTarget (hostile): a target that's down, dead or gone hands over to the next player.</summary>
    public static void BeforeHasTarget(BrainNeetAI npc)
    {
        if (!Hosting || npc.brainType != BrainNeetAI.BrainType.Hostile || !Spotted(npc))
            return;
        var target = npc.pickupTgt.Obj;
        if (target != null)
        {
            byte slot = Targets.SlotOf(target);
            if (slot == WorldSide.None || Targets.IsAlive(slot))
                return;
        }
        var next = Targets.NearestAlive(npc.transform.position, out _);
        if (next != null)
            npc.pickupTgt.Obj = next;
        else
            Spotted(npc) = false;
    }

    /// <summary>Before BrainNeetAI.SetDestinationReachedRot: hostile NPCs face their target, trick-or-treaters the nearest player.</summary>
    public static bool BeforeFacing(BrainNeetAI npc, ref NodeState result)
    {
        if (!Hosting || (npc.brainType != BrainNeetAI.BrainType.Hostile && npc.brainType != BrainNeetAI.BrainType.TrickOrTreater))
            return true;
        var movement = Movement(npc);
        if (movement != null && movement.slipped)
            return true;
        var face = npc.brainType == BrainNeetAI.BrainType.Hostile && Spotted(npc) && npc.pickupTgt.Obj != null
            ? npc.pickupTgt.Obj
            : Targets.NearestAlive(npc.transform.position, out _);
        if (face == null)
            return true;
        var me = npc.transform.position;
        var at = face.transform.position;
        var flat = new Vector3(at.x, me.y, at.z) - me;
        if (flat.sqrMagnitude > 0.0001f)
            npc.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
        result = NodeState.Success;
        return false;
    }

    /// <summary>After Spider_House.ScanForTargets: the nearest player who's up and in reach, else the game's pick unless it's a downed player.</summary>
    public static void AfterSpiderScan(Spider_House spider, ref NodeState result)
    {
        if (!Hosting)
            return;
        var from = spider.transform.position + spider.transform.forward * 0.3f;
        var nearest = Targets.NearestAlive(from, out _);
        if (nearest != null && (nearest.transform.position - from).sqrMagnitude <= SpiderReach * SpiderReach)
        {
            if (spider.target == null || spider.target.Obj != nearest)
                spider.target = new AITarget(nearest);
            result = NodeState.Success;
            return;
        }
        if (result != NodeState.Success || spider.target == null)
            return;
        byte slot = Targets.SlotOf(spider.target.Obj);
        if (slot != WorldSide.None && !Targets.IsAlive(slot))
        {
            spider.target = null;
            result = NodeState.Failure;
        }
    }

    /// <summary>After Spider_House.OnCollisionEnter: the bite's energy drain reaches a remote player's own game (their body here has no energy).</summary>
    public static void AfterSpiderBite(Spider_House spider, Collision other)
    {
        if (!Hosting || spider.target == null || other.gameObject != spider.target.Obj)
            return;
        byte slot = Targets.SlotOf(other.gameObject);
        if (slot != WorldSide.None && slot != WorldSide.LocalSlot)
            Forwarding.Forwarder.StoryStat(slot, "Energy", set: false, SpiderEnergyBite);
    }

    /// <summary>After SkeletonParasite.Update: its skull looks at the player it's after (the game always turned it to the host's camera).</summary>
    public static void AfterParasiteUpdate(SkeletonParasite parasite)
    {
        if (!Hosting || !parasite.readyToChase || parasite.possesses)
            return;
        var target = Targets.NearestAlive(parasite.transform.position, out byte slot);
        if (target == null || slot == WorldSide.LocalSlot)
            return;
        parasite.transform.LookAt(target.transform.position + Vector3.up * 1.6f);
    }
}
