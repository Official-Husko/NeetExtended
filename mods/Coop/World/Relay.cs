using Coop.Net;

namespace Coop.World;

/// <summary>
/// Host: passes a client's message on to the other players unchanged. Clients only talk to the host,
/// so everything one client does reaches the others this way.
/// </summary>
internal static class Relay
{
    /// <param name="start">Where the payload starts (the reader's position right after the message id).</param>
    public static void Forward(ushort id, byte[] data, int start, int length, Channel channel, byte fromSlot)
    {
        var w = Outbox.Begin(id);
        w.Put(data, start, length);
        Outbox.ToPlayers(w, channel, exceptSlot: fromSlot);
    }
}
