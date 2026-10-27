using Coop.Session;
using LiteNetLib.Utils;

namespace Coop.Net;

/// <summary>
/// Sending. Build a message with <see cref="Begin"/>, fill it and send it straight away: the writer is
/// shared, so nothing else may start a message in between. Clients only ever talk to the host; the
/// host passes on what the others need.
/// </summary>
public static class Outbox
{
    /// <summary>For <c>exceptSlot</c>: nobody is left out.</summary>
    public const byte NoSlot = byte.MaxValue;

    private static readonly NetDataWriter Writer = new NetDataWriter(true, 512);

    internal static ITransport Transport { get; set; }

    public static NetDataWriter Begin(ushort id)
    {
        Writer.Reset();
        Writer.Put(id);
        return Writer;
    }

    /// <summary>
    /// On the host: to every other player who is in the world (done loading). On a client: to the host.
    /// </summary>
    public static void ToPlayers(NetDataWriter message, Channel channel, byte exceptSlot = NoSlot)
    {
        if (Transport == null)
            return;
        if (CoopSession.IsClient)
        {
            ToHost(message, channel);
            return;
        }
        var players = CoopSession.Players;
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (!player.IsLocal && player.InWorld && player.Slot != exceptSlot)
                Transport.Send(player.Peer, channel, message);
        }
    }

    public static void ToHost(NetDataWriter message, Channel channel)
    {
        if (Transport != null && CoopSession.IsClient && CoopSession.HostPeer >= 0)
            Transport.Send(CoopSession.HostPeer, channel, message);
    }

    /// <summary>Host only: to one player, loading or not.</summary>
    public static void ToSlot(byte slot, NetDataWriter message, Channel channel)
    {
        var player = CoopSession.FindSlot(slot);
        if (Transport != null && player != null && !player.IsLocal)
            Transport.Send(player.Peer, channel, message);
    }

    public static void ToPeer(int peer, NetDataWriter message, Channel channel) =>
        Transport?.Send(peer, channel, message);
}
