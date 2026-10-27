using Coop.Net;
using Coop.Session;
using LiteNetLib.Utils;

namespace Coop.Assets.Media;

/// <summary>
/// Gets the host's media folders to the players: to each one as soon as they arrive (before their
/// house loads, so the host's tapes exist as items there), to everyone when a folder changes, and to
/// a client that asks (after a hot reload).
/// </summary>
internal static class MediaSync
{
    public static void Init(CoopMod mod)
    {
        Messages.On(MessageIds.AssetMedia, OnMessage);
        MediaHost.Init(mod);
        MediaHost.Ready += () =>
        {
            if (CoopSession.IsHost)
                Send(Outbox.NoSlot);
        };
        CoopSession.PlayerArrived += player => Send(player.Slot);
        CoopSession.Resumed += Ask;
        CoopSession.Ended += _ => MediaClient.Release();
    }

    public static void Tick()
    {
        MediaHost.Tick();
        MediaClient.Tick();
    }

    /// <summary>Client: send me your folders (after a hot reload, so the files still missing are asked for again).</summary>
    public static void Ask()
    {
        if (!CoopSession.IsClient)
            return;
        var w = Outbox.Begin(MessageIds.AssetMedia);
        w.Put(MediaListing.Request);
        Outbox.ToHost(w, Channel.Events);
    }

    /// <summary>Host: the list to one player, or everyone (<see cref="Outbox.NoSlot"/>). Everyone gets it again when it's done.</summary>
    private static void Send(byte slot)
    {
        if (!CoopSession.IsHost)
            return;
        var w = Outbox.Begin(MessageIds.AssetMedia);
        MediaListing.Write(w, MediaHost.Current);
        if (slot == Outbox.NoSlot)
        {
            // Everyone who's here, loading or not.
            foreach (var player in CoopSession.Players)
            {
                if (!player.IsLocal)
                    Outbox.ToSlot(player.Slot, w, Channel.Events);
            }
        }
        else
        {
            Outbox.ToSlot(slot, w, Channel.Events);
        }
    }

    private static void OnMessage(in Sender from, NetDataReader reader)
    {
        byte kind = reader.GetByte();
        if (kind == MediaListing.Request)
        {
            if (CoopSession.IsHost)
                Send(from.Slot);
            return;
        }
        if (kind == MediaListing.Listing && CoopSession.IsClient && from.Slot == CoopPlayer.HostSlot)
            MediaClient.Apply(MediaListing.Read(reader));
    }
}
