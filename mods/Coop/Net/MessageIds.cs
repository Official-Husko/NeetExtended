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

    // World: 100-199, see World/.
}
