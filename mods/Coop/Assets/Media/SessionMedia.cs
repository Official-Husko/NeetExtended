using System.Collections.Generic;
using Coop.Assets.Transfer;
using Coop.Net;
using Coop.Session;

namespace Coop.Assets.Media;

/// <summary>
/// Host: the session's TV stations and tapes, everyone's folders merged (<see cref="MediaMerge"/>).
/// Players send their own folders when they come in and when they change; the host makes the list
/// again, fetches the players' files (it shows them too, and passes them on), and sends the list to
/// everyone. Built at most once a frame.
/// </summary>
internal static class SessionMedia
{
    private static readonly SortedDictionary<byte, List<MediaFolderInfo>> Players = new SortedDictionary<byte, List<MediaFolderInfo>>();
    private static readonly Dictionary<byte, int> Merged = new Dictionary<byte, int>();
    private static List<MediaFolderInfo> _current = new List<MediaFolderInfo>();
    private static bool _dirty;

    public static void MarkDirty() => _dirty = true;

    /// <summary>A player's own folders, the <paramref name="number"/>th time they sent them.</summary>
    public static void FromPlayer(byte slot, int number, List<MediaFolderInfo> folders)
    {
        string who = CoopSession.FindSlot(slot)?.Name ?? "player " + slot;
        foreach (var folder in folders)
        {
            folder.Owner = slot;
            foreach (var file in folder.Files)
            {
                if (!file.Value.IsValid)
                    continue;
                FileLabels.Set(file.Value, file.Key);
                // They have it: anyone who needs it gets it through the host, which takes it now.
                Hub.Offered(file.Value, slot, $"{Kind(folder.FolderName)} {file.Key} from {who}");
            }
        }
        Players[slot] = folders;
        Merged[slot] = number;
        _dirty = true;
    }

    public static void PlayerLeft(byte slot)
    {
        if (Players.Remove(slot) | Merged.Remove(slot))
            _dirty = true;
    }

    public static void Clear()
    {
        Players.Clear();
        Merged.Clear();
        _current = new List<MediaFolderInfo>();
        _dirty = false;
    }

    public static void Tick()
    {
        if (!_dirty || !CoopSession.IsHost)
            return;
        _dirty = false;
        var all = new List<KeyValuePair<byte, List<MediaFolderInfo>>>
        {
            new KeyValuePair<byte, List<MediaFolderInfo>>(CoopPlayer.HostSlot, LocalMedia.Current),
        };
        foreach (var player in Players)
        {
            if (CoopSession.FindSlot(player.Key) != null)
                all.Add(player);
        }
        _current = MediaMerge.Merge(all);
        MediaStandIn.Apply(_current, CoopPlayer.HostSlot);
        Send(Outbox.NoSlot);
    }

    /// <summary>The list to one player, or to everyone (<see cref="Outbox.NoSlot"/>), loading or not.</summary>
    public static void Send(byte slot)
    {
        if (!CoopSession.IsHost)
            return;
        var w = Outbox.Begin(MessageIds.AssetMedia);
        w.Put(MediaListing.Listing);
        MediaListing.WriteFolders(w, _current);
        MediaListing.WriteMerged(w, Merged);
        if (slot != Outbox.NoSlot)
        {
            Outbox.ToSlot(slot, w, Channel.Events);
            return;
        }
        foreach (var player in CoopSession.Players)
        {
            if (!player.IsLocal)
                Outbox.ToSlot(player.Slot, w, Channel.Events);
        }
    }

    public static string Kind(string folderName) =>
        folderName == "tv" ? "the TV station" : folderName == "mixtapes" ? "the mixtape" : "the file";
}
