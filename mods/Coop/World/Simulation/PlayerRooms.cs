using Coop.Players;
using Coop.Session;
using UnityEngine;

namespace Coop.World.Simulation;

/// <summary>
/// Host: which rooms players are in. The game only knows its own player; the others are the
/// remote bodies, placed by where they last were.
/// </summary>
internal static class PlayerRooms
{
    public static bool AnyRemote()
    {
        var players = CoopSession.Players;
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (!player.IsLocal && player.InWorld)
                return true;
        }
        return false;
    }

    /// <summary>True when another player (not this game's own) is in <paramref name="room"/>.</summary>
    public static bool RemoteIn(Location room)
    {
        var players = CoopSession.Players;
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (player.IsLocal || !player.InWorld)
                continue;
            var body = RemotePlayers.Find(player.Slot);
            if (body != null && Locator.LocatePoint(body.transform.position) == room)
                return true;
        }
        return false;
    }

    /// <summary>True when any player, this game's own included, is in <paramref name="room"/>.</summary>
    public static bool AnyoneIn(Location room)
    {
        var own = WorldSide.LocalPlayer;
        if (own != null && own.TryGetComponent(out Brain brain) && brain.currentLocation == room)
            return true;
        return RemoteIn(room);
    }

    public static Location Of(Vector3 point) => Locator.LocatePoint(point);

    /// <summary>The player closest to <paramref name="from"/>: this game's own, or another player's body here.</summary>
    public static GameObject Nearest(Vector3 from, out byte slot)
    {
        var best = WorldSide.LocalPlayer;
        slot = WorldSide.LocalSlot;
        float bestSq = best != null ? (best.transform.position - from).sqrMagnitude : float.MaxValue;
        var players = CoopSession.Players;
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (player.IsLocal || !player.InWorld)
                continue;
            var body = RemotePlayers.Find(player.Slot);
            if (body == null)
                continue;
            float sq = (body.transform.position - from).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = body.gameObject;
                slot = player.Slot;
            }
        }
        return best;
    }
}
