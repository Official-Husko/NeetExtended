using Coop.Net;
using Coop.Session;
using LiteNetLib.Utils;

namespace Coop.Assets.Transfer;

/// <summary>
/// The file transfer protocol. A game that needs a file asks the one that has it (a client always
/// asks the host; the host asks whichever client offered it) and gets it in chunks:
///
/// AssetWant (Events): transfer id (uint, the asker's), file id (size may be -1), offset to start at.
/// AssetChunk (Bulk): transfer id, total size, offset, bytes. Always at least one, even for an empty rest.
/// AssetAck (Events): transfer id, bytes received so far. The sender keeps a window ahead of it.
/// AssetMissing (Events): transfer id, reason (<see cref="Gone"/>, <see cref="Coming"/>, <see cref="TooBig"/>);
/// with transfer id 0 and <see cref="Restarted"/>: the sender hot reloaded and forgot its transfers.
/// AssetCancel (Events): transfer id: not needed any more.
/// AssetOffer (Events, client to host): file id: this game has it and put it in the house.
/// </summary>
internal static class AssetNet
{
    /// <summary>Nobody here has it.</summary>
    public const byte Gone = 0;

    /// <summary>The host is getting it from another player; keep waiting.</summary>
    public const byte Coming = 1;

    /// <summary>Over the sender's size limit.</summary>
    public const byte TooBig = 2;

    /// <summary>This game hot reloaded: whatever it was sending, ask for again now.</summary>
    public const byte Restarted = 3;

    /// <summary>After a hot reload: the others ask again at once instead of after a silence.</summary>
    public static void SayRestarted()
    {
        var w = Outbox.Begin(MessageIds.AssetMissing);
        w.Put(0u);
        w.Put(Restarted);
        Outbox.ToPlayers(w, Channel.Events);
    }

    /// <summary>A client only reaches the host (slot 0); the host reaches any player.</summary>
    public static void ToSlot(byte slot, NetDataWriter message, Channel channel)
    {
        if (CoopSession.IsClient)
        {
            if (slot == CoopPlayer.HostSlot)
                Outbox.ToHost(message, channel);
        }
        else if (CoopSession.IsHost)
        {
            Outbox.ToSlot(slot, message, channel);
        }
    }

    public static void Missing(byte slot, uint transfer, byte reason)
    {
        var w = Outbox.Begin(MessageIds.AssetMissing);
        w.Put(transfer);
        w.Put(reason);
        ToSlot(slot, w, Channel.Events);
    }

    /// <summary>Whether files can move right now: a session past the hello.</summary>
    public static bool CanTalk => CoopSession.IsHost || CoopSession.State == SessionState.Joining || CoopSession.State == SessionState.Playing;

    /// <summary>Host: a player in the world takes file chunks (a loading one gets its house first).</summary>
    public static bool Receives(byte slot)
    {
        if (CoopSession.IsClient)
            return slot == CoopPlayer.HostSlot;
        var player = CoopSession.FindSlot(slot);
        return player != null && !player.IsLocal && player.InWorld;
    }
}
