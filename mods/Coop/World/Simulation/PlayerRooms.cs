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
        foreach (var player in CoopSession.Players)
        {
            if (!player.IsLocal && player.InWorld)
                return true;
        }
        return false;
    }

    /// <summary>True when another player (not this game's own) is in <paramref name="room"/>.</summary>
    public static bool RemoteIn(Location room)
    {
        foreach (var player in CoopSession.Players)
        {
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
}
