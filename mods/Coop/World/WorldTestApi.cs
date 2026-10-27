using Coop.World.Objects;
using UnityEngine;

namespace Coop.World;

/// <summary>
/// Hands for testing from the dev bridge, since the bridge can't pass objects to game methods:
/// <c>neet call type.call type=Coop.World.WorldTestApi method=Grab args=[1234] --instance 2</c>.
/// Each does what the player's own hand would do, through the game's code.
/// </summary>
public static class WorldTestApi
{
    public static string Grab(int itemId, int hand = 0)
    {
        var o = Find(itemId);
        var hands = Hands();
        if (o == null || hands == null)
            return o == null ? $"No shared item {itemId}." : "No player.";
        hands.Pickup((global::Hands)hand, o.Go);
        bool held = hands.pickups[hand] == o.Go;
        return held ? $"Holding {o}." : $"Couldn't pick up {o} (owner {o.Owner}, holder {o.Holder}).";
    }

    public static string Drop(int hand = 0)
    {
        var hands = Hands();
        if (hands == null || hands.pickups[hand] == null)
            return "Nothing in that hand.";
        string what = hands.pickups[hand].name;
        hands.Drop((global::Hands)hand);
        return "Dropped " + what + ".";
    }

    public static string Throw(int hand = 0)
    {
        var hands = Hands();
        if (hands == null || hands.pickups[hand] == null)
            return "Nothing in that hand.";
        string what = hands.pickups[hand].name;
        hands.Throw((global::Hands)hand);
        return "Threw " + what + ".";
    }

    /// <summary>Uses a shared item the way a hand on it does (every use of its parts, once).</summary>
    public static string Use(int itemId)
    {
        var o = Find(itemId);
        if (o == null)
            return $"No shared item {itemId}.";
        int used = 0;
        foreach (var part in o.Go.GetComponents<Usable.IUsable>())
        {
            part.Use();
            used++;
        }
        return $"Used {o} ({used} parts).";
    }

    /// <summary>Hits another player's body here the way a punch does (through its stats).</summary>
    public static string HitPlayer(int slot, float amount)
    {
        var body = Coop.Players.RemotePlayers.Find((byte)slot);
        if (body == null || !body.TryGetComponent(out ActorStats stats))
            return $"No body for slot {slot} here.";
        stats.stats[typeof(Health)].Affect(body.transform.position, -amount, EffectType.Impact);
        return $"Hit slot {slot} for {amount}; it shows {stats.stats[typeof(Health)].GetVal():0} health here.";
    }

    /// <summary>Hits an NPC here the way a punch does (on a client, its copy).</summary>
    public static string HitNpc(int npcId, float amount)
    {
        var o = Find(npcId);
        if (o == null || !o.Go.TryGetComponent(out ActorStats stats) || !stats.stats.ContainsKey(typeof(Health)))
            return $"No NPC {npcId} with health here.";
        stats.stats[typeof(Health)].Affect(o.Tf.position, -amount, EffectType.Impact);
        return $"Hit {o} for {amount}; {stats.stats[typeof(Health)].GetVal():0} health here.";
    }

    /// <summary>This game's player's health.</summary>
    public static string MyHealth()
    {
        var player = WorldSide.LocalPlayer;
        return player != null && player.TryGetComponent(out PlayerStats stats) ? stats.health.GetVal().ToString("0.0") : "No player.";
    }

    /// <summary>Where a shared thing is here, who simulates it and whether it's a replica.</summary>
    public static string Where(int itemId)
    {
        var o = Find(itemId);
        if (o == null)
            return $"No shared item {itemId}.";
        var p = o.Tf.position;
        string body = o.Body == null ? "no body" : o.Body.isKinematic ? "kinematic" : "dynamic";
        return $"{o} at ({p.x:0.00}, {p.y:0.00}, {p.z:0.00}), owner {o.Owner}, holder {o.Holder}, {(o.IsReplica ? "replica" : "simulated here")}, {body}";
    }

    // Items first, then animals and NPCs (a rat's id is its brain's).
    private static NetObject Find(int id) =>
        NetObjects.Find(new NetRef(NetKind.Item, id)) ?? NetObjects.Find(new NetRef(NetKind.Npc, id));

    private static Player_Abilities Hands()
    {
        var player = WorldSide.LocalPlayer;
        return player != null ? player.GetComponent<Player_Abilities>() : null;
    }
}
