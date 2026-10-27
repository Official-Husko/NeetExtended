using Coop.Players;
using Coop.Session;
using Coop.World.Forwarding;
using UnityEngine;

namespace Coop.World.Hostiles;

/// <summary>
/// Host: the players enemies can go after. A player is a target while their body is alive and they
/// aren't down: the host's own player, or a remote player's body here. Downed players count as
/// dead for enemies (they leave them alone, and a hitman's job is done).
/// </summary>
internal static class Targets
{
    /// <summary>The player behind <paramref name="body"/> (or one of its parts): the host's own, a remote one, or none.</summary>
    public static byte SlotOf(GameObject body)
    {
        if (body == null)
            return WorldSide.None;
        if (body == WorldSide.LocalPlayer)
            return WorldSide.LocalSlot;
        var puppet = body.GetComponentInParent<PlayerPuppet>();
        return puppet != null ? puppet.Slot : WorldSide.None;
    }

    /// <summary>The body enemies aim at for a player in this game, or null.</summary>
    public static GameObject BodyOf(byte slot)
    {
        if (slot == WorldSide.LocalSlot)
            return WorldSide.LocalPlayer;
        var puppet = RemotePlayers.Find(slot);
        return puppet != null ? puppet.gameObject : null;
    }

    public static bool IsDown(byte slot) => Downed.IsDown(slot) || (slot == WorldSide.LocalSlot && Downed.LocalDown);

    /// <summary>True when <paramref name="slot"/>'s body is here, alive and not down.</summary>
    public static bool IsAlive(byte slot)
    {
        if (slot == WorldSide.None || IsDown(slot))
            return false;
        var body = BodyOf(slot);
        if (body == null)
            return false;
        if (slot == WorldSide.LocalSlot)
        {
            if (body.TryGetComponent(out Player_Abilities rig) && rig.dead)
                return false;
            return !body.TryGetComponent(out PlayerStats stats) || stats.health == null || stats.health.GetVal() > 0f;
        }
        return !body.TryGetComponent(out RemoteActor actor) || ActorStats.canDamageOther(actor);
    }

    /// <summary>The nearest player who's alive and up, or null.</summary>
    public static GameObject NearestAlive(Vector3 from, out byte slot)
    {
        slot = WorldSide.None;
        GameObject best = null;
        float bestSq = float.MaxValue;
        var players = CoopSession.Players;
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (!player.InWorld && !player.IsLocal)
                continue;
            byte s = player.IsLocal ? WorldSide.LocalSlot : player.Slot;
            if (!IsAlive(s))
                continue;
            var body = BodyOf(s);
            float sq = (body.transform.position - from).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = body;
                slot = s;
            }
        }
        // Single player or a session without this game's player in the list.
        if (best == null && players.Count == 0 && IsAlive(WorldSide.LocalSlot))
        {
            best = WorldSide.LocalPlayer;
            slot = WorldSide.LocalSlot;
        }
        return best;
    }
}
