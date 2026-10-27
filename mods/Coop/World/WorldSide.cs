using Coop.Session;
using NeetModKit.Game;

namespace Coop.World;

/// <summary>
/// Which side of the shared house this game is on, read by the hot paths of World. A client stops
/// running the world as soon as it starts loading the host's house (<see cref="WorldRole.RunsWorld"/>).
/// </summary>
internal static class WorldSide
{
    public const byte HostSlot = CoopPlayer.HostSlot;

    /// <summary>"Nobody": no holder, no owner.</summary>
    public const byte None = byte.MaxValue;

    /// <summary>World sync is up for this house: the registry is built and messages flow.</summary>
    public static bool Running { get; internal set; }

    public static bool IsHost => CoopSession.IsHost;

    /// <summary>This game shows the host's house (loading it or in it).</summary>
    public static bool IsClient => CoopSession.IsClient && !WorldRole.RunsWorld;

    public static byte LocalSlot => CoopSession.Local?.Slot ?? HostSlot;

    /// <summary>This game's player body, or null between bodies.</summary>
    public static UnityEngine.GameObject LocalPlayer => HouseManager._ != null ? HouseManager._.player : null;
}
