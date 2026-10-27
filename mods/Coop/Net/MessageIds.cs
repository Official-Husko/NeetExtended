namespace Coop.Net;

/// <summary>
/// Every message type, in one place so ids never clash. Ranges: 1-19 session, 20-39 join,
/// 40-59 players, 100-199 world. Raise <see cref="Protocol.Version"/> when a payload changes.
/// </summary>
public static class MessageIds
{
    // Session
    public const ushort Hello = 1;
    public const ushort Welcome = 2;
    public const ushort PlayerJoined = 3;
    public const ushort PlayerLeft = 4;
    public const ushort Ready = 5;
    public const ushort Resync = 6;

    // Join
    public const ushort Snapshot = 20;

    // Players
    public const ushort Pose = 40;
    public const ushort Appearance = 41;
    public const ushort Body = 42;

    // World: 100-199, see World/.
    public const ushort WorldSpawn = 100;
    public const ushort WorldDestroy = 101;
    public const ushort WorldBodies = 102;
    public const ushort WorldSettled = 103;
    public const ushort WorldClaim = 104;
    public const ushort WorldOwner = 105;
    public const ushort WorldNpcPoses = 106;
    public const ushort WorldNpcEvent = 107;
    public const ushort WorldState = 108;
    public const ushort MoneyDelta = 109;
    public const ushort MoneyTotal = 110;
    public const ushort WorldManifest = 111;
}
