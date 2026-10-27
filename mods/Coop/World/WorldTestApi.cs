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
        var o = NetObjects.Find(new NetRef(NetKind.Item, itemId));
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

    /// <summary>Where a shared thing is here, who simulates it and whether it's a replica.</summary>
    public static string Where(int itemId)
    {
        var o = NetObjects.Find(new NetRef(NetKind.Item, itemId));
        if (o == null)
            return $"No shared item {itemId}.";
        var p = o.Tf.position;
        string body = o.Body == null ? "no body" : o.Body.isKinematic ? "kinematic" : "dynamic";
        return $"{o} at ({p.x:0.00}, {p.y:0.00}, {p.z:0.00}), owner {o.Owner}, holder {o.Holder}, {(o.IsReplica ? "replica" : "simulated here")}, {body}";
    }

    private static Player_Abilities Hands()
    {
        var player = WorldSide.LocalPlayer;
        return player != null ? player.GetComponent<Player_Abilities>() : null;
    }
}
