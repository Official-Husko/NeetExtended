using Coop.World.Objects;
using NeetModKit.Game;
using UnityEngine;

namespace Coop.World.Flames;

/// <summary>
/// Who may light what. A flame is lit by the game that simulates what it sits on (an item's owner, the
/// host for the house); everyone else gets the result. Putting a flame out works from any game. The
/// room fire's flames on a client are only a copy of the host's, so they never light anything that
/// isn't simulated there.
/// </summary>
internal static class FlameRules
{
    /// <summary>This game decides whether the flame lights (and sends it).</summary>
    public static bool Simulates(Flame flame)
    {
        if (!WorldSide.Running)
            return true;
        if (IsRoomFire(flame))
            return WorldRole.RunsWorld;
        var info = FlameBook.Info(flame);
        if (info != null)
            return Simulates(info);
        // The stove is the house's; anything else (a spray's fireball) belongs to the game that made it.
        return GasSync.IsStove(flame) ? WorldRole.RunsWorld : true;
    }

    public static bool Simulates(FlameInfo info)
    {
        if (info.Scene)
            return WorldRole.RunsWorld;
        var o = NetObjects.Find(info.Ref);
        return o == null || !o.IsReplica;
    }

    /// <summary>The thing is simulated here, so it's here that it catches fire or burns.</summary>
    public static bool Simulates(GameObject thing)
    {
        if (!WorldSide.Running)
            return true;
        var o = NetObjects.Find(thing);
        return o != null ? !o.IsReplica : WorldRole.RunsWorld;
    }

    public static bool IsRoomFire(Flame flame) => flame.TryGetComponent(out FireParticle _);
}
