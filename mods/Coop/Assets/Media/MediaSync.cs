using System.Collections.Generic;
using Coop.Net;
using Coop.Session;
using LiteNetLib.Utils;

namespace Coop.Assets.Media;

/// <summary>
/// Everyone's TV stations and tapes in the session. Each game hashes its own media folders
/// (<see cref="LocalMedia"/>); a client sends them to the host as soon as it's let in and again when
/// they change. The host merges everyone's (<see cref="SessionMedia"/>) and sends the list to all,
/// and each game shows it in place of its own folders (<see cref="MediaStandIn"/>).
/// </summary>
internal static class MediaSync
{
    private static int _sent;
    private static int _merged = -1;

    /// <summary>
    /// Client: the last list from the host has this game's own folders in it (as last sent). A joiner
    /// waits for that before its house loads, so it has the same tapes as everyone.
    /// </summary>
    public static bool HasCurrentList => CoopSession.IsClient && _sent > 0 && _merged >= _sent;

    public static void Init(CoopMod mod)
    {
        Messages.On(MessageIds.AssetMedia, OnMessage);
        LocalMedia.Init(mod);
        MediaStandIn.Init();
        LocalMedia.Ready += OnLocalChanged;
        CoopSession.Started += () =>
        {
            if (CoopSession.IsHost)
                SessionMedia.MarkDirty();
        };
        CoopSession.Welcomed += SendMine;
        CoopSession.PlayerArrived += player => SessionMedia.Send(player.Slot);
        CoopSession.PlayerLeft += player => SessionMedia.PlayerLeft(player.Slot);
        CoopSession.Resumed += () =>
        {
            if (CoopSession.IsHost)
            {
                // The players' folders went with the last copy of the mod.
                SessionMedia.MarkDirty();
                var w = Outbox.Begin(MessageIds.AssetMedia);
                w.Put(MediaListing.Request);
                Outbox.ToPlayers(w, Channel.Events);
                return;
            }
            Ask();
            SendMine();
        };
        CoopSession.Ended += _ =>
        {
            MediaStandIn.Release();
            SessionMedia.Clear();
            _sent = 0;
            _merged = -1;
        };
    }

    public static void Tick() => SessionMedia.Tick();

    /// <summary>Client: send me the session's list (after a hot reload, so the files still missing are asked for again).</summary>
    public static void Ask()
    {
        if (!CoopSession.IsClient)
            return;
        var w = Outbox.Begin(MessageIds.AssetMedia);
        w.Put(MediaListing.Request);
        Outbox.ToHost(w, Channel.Events);
    }

    private static void OnLocalChanged()
    {
        if (CoopSession.IsHost)
            SessionMedia.MarkDirty();
        else if (CoopSession.IsClient && CoopSession.Local != null)
            SendMine();
    }

    // Client: this game's own folders, for the session's list.
    private static void SendMine()
    {
        if (!CoopSession.IsClient || CoopSession.Local == null)
            return;
        var folders = LocalMedia.Current;
        var w = Outbox.Begin(MessageIds.AssetMedia);
        w.Put(MediaListing.Mine);
        w.Put(++_sent);
        MediaListing.WriteFolders(w, folders);
        Outbox.ToHost(w, Channel.Events);
    }

    private static void OnMessage(in Sender from, NetDataReader reader)
    {
        byte kind = reader.GetByte();
        switch (kind)
        {
            case MediaListing.Request:
                if (CoopSession.IsHost)
                    SessionMedia.Send(from.Slot);
                else if (from.Slot == CoopPlayer.HostSlot)
                    SendMine();
                break;
            case MediaListing.Mine:
                if (CoopSession.IsHost && from.Slot != CoopPlayer.HostSlot)
                {
                    int number = reader.GetInt();
                    SessionMedia.FromPlayer(from.Slot, number, MediaListing.ReadFolders(reader));
                }
                break;
            case MediaListing.Listing:
                if (CoopSession.IsClient && from.Slot == CoopPlayer.HostSlot && CoopSession.Local != null)
                {
                    var folders = MediaListing.ReadFolders(reader);
                    Dictionary<byte, int> merged = MediaListing.ReadMerged(reader);
                    _merged = merged.TryGetValue(CoopSession.Local.Slot, out int number) ? number : -1;
                    MediaStandIn.Apply(folders, CoopSession.Local.Slot);
                }
                break;
        }
    }
}
