using Coop.World.Objects;
using Coop.World.Ownership;

namespace Coop.Players;

/// <summary>
/// A player simulates the masks they wear. A mask put on by hand is already theirs, but one that came
/// back with a returning body (the game's own load puts it on) belongs to the host, whose copy would
/// then be moved by the host's stream while it sits on this player's camera. This takes those over,
/// the way touching a replica does.
/// </summary>
internal static class MaskClaims
{
    public static void Claim(Player_Abilities abilities)
    {
        var worn = abilities.masks;
        if (worn == null)
            return;
        for (int i = 0; i < worn.Count; i++)
        {
            var item = worn[i] != null ? NetObjects.Find(worn[i]) : null;
            if (item == null || !item.IsReplica)
                continue;
            // Worn means kinematic, whatever the old owner last said (a mask that fell when its
            // wearer left was left falling in the host's copy).
            item.GameKinematic = true;
            Owners.LocalTouch(item);
        }
    }
}
