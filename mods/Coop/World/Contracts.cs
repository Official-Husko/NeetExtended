using Coop.World.Hostiles;

namespace Coop.World;

/// <summary>
/// Paid jobs on players, for co-op content (a phone line, rules). Host only.
/// </summary>
public static class Contracts
{
    /// <summary>
    /// After <paramref name="delaySeconds"/>, the cop van comes and its agents go after only the player
    /// in <paramref name="targetSlot"/> (host or remote), wherever they are in the house. When that
    /// player is dead or down they leave as usual; if the player leaves the game first, they leave too.
    /// If a raid's van is already in the street, the hitman's van waits for it to go.
    /// </summary>
    /// <returns>False when this game isn't hosting a session in the house, that player isn't in it, or a hitman is already on the way or at work.</returns>
    public static bool SendHitman(byte targetSlot, float delaySeconds) => Hitman.Send(targetSlot, delaySeconds);

    /// <summary>True while a hitman is on the way or at work (one at a time).</summary>
    public static bool HitmanBusy => Hitman.Busy;

    /// <summary>Who the current hitman is after, or 255 when there's none.</summary>
    public static byte HitmanTarget => Hitman.Target;
}
