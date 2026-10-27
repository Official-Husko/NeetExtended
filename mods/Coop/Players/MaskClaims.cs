using Coop.World.Objects;
using Coop.World.Ownership;

namespace Coop.Players;

/// <summary>
/// A player simulates the masks they wear. A mask put on by hand is already theirs, but one that came
/// back with a returning body (the game's own load puts it on) belongs to the host, whose copy would
/// then be moved by the host's stream while it sits on this player's camera. This takes those over,
/// the way touching a replica does. The host parks a player's masks while they're away
/// (<see cref="MaskParking"/>), so a mask that wasn't claimed before the host's stream or manifest
/// placed it would be dragged to the parking spot with its offset to the camera ruined; the world
/// start and the manifest claim what the player wears first.
/// </summary>
internal static class MaskClaims
{
    /// <summary>This game's player's masks, now (the house just loaded with them on, before a replica could be moved off the head).</summary>
    public static void ClaimWorn()
    {
        var player = HouseManager._ != null ? HouseManager._.player : null;
        if (player != null && player.TryGetComponent(out Player_Abilities abilities))
            Claim(abilities);
    }

    public static void Claim(Player_Abilities abilities)
    {
        var worn = abilities.masks;
        if (worn == null)
            return;
        for (int i = 0; i < worn.Count; i++)
        {
            var item = worn[i] != null ? NetObjects.Find(worn[i]) : null;
            if (item != null && item.IsReplica)
                Claim(item);
        }
    }

    /// <summary>Whether this player's game has the item on its head.</summary>
    public static bool IsWorn(NetObject item)
    {
        var player = HouseManager._ != null ? HouseManager._.player : null;
        return item.Go != null && player != null && player.TryGetComponent(out Player_Abilities abilities)
            && abilities.masks != null && abilities.masks.Contains(item.Go);
    }

    /// <summary>Takes a worn item over: kinematic whatever the old owner last said, simulated here.</summary>
    public static void Claim(NetObject item)
    {
        item.GameKinematic = true;
        Owners.LocalTouch(item);
    }
}
