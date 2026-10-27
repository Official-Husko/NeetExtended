using System.Collections.Generic;
using LiteNetLib.Utils;

namespace Coop.Assets.Media;

/// <summary>One mod's media folder as the host has it: its files by path inside it, and its list files' text.</summary>
internal sealed class MediaFolderInfo
{
    public string FolderName;
    public string ModId;
    public readonly List<KeyValuePair<string, AssetId>> Files = new List<KeyValuePair<string, AssetId>>();
    public readonly List<KeyValuePair<string, string>> Texts = new List<KeyValuePair<string, string>>();
}

/// <summary>
/// The host's media folders, for the players. Files too big to share are listed with no id, so
/// the channel and tape lists still match the host's (that station or tape just stays empty).
///
/// AssetMedia (Events): kind (0 request, client to host; 1 listing, host to clients), then for a
/// listing: folder count, and per folder: folder name, mod id, files (path, file id), texts (path, text).
/// </summary>
internal static class MediaListing
{
    public const byte Request = 0;
    public const byte Listing = 1;

    // A list file is a few lines. Message strings top out at 64 KB of UTF-8; a file this size can't get there.
    public const int MaxText = 16 * 1024;

    public static void Write(NetDataWriter w, List<MediaFolderInfo> folders)
    {
        w.Put(Listing);
        w.Put((ushort)folders.Count);
        foreach (var folder in folders)
        {
            w.Put(folder.FolderName);
            w.Put(folder.ModId);
            w.Put((ushort)folder.Files.Count);
            foreach (var file in folder.Files)
            {
                w.Put(file.Key);
                file.Value.Write(w);
            }
            w.Put((ushort)folder.Texts.Count);
            foreach (var text in folder.Texts)
            {
                w.Put(text.Key);
                w.Put(text.Value);
            }
        }
    }

    public static List<MediaFolderInfo> Read(NetDataReader r)
    {
        int count = r.GetUShort();
        var folders = new List<MediaFolderInfo>(count);
        for (int i = 0; i < count; i++)
        {
            var folder = new MediaFolderInfo { FolderName = r.GetString(64), ModId = r.GetString(128) };
            int files = r.GetUShort();
            for (int f = 0; f < files; f++)
            {
                string path = r.GetString(512);
                var id = AssetId.Read(r);
                if (MediaPaths.IsSafe(path))
                    folder.Files.Add(new KeyValuePair<string, AssetId>(path, id));
            }
            int texts = r.GetUShort();
            for (int t = 0; t < texts; t++)
            {
                string path = r.GetString(512);
                string text = r.GetString(MaxText);
                if (MediaPaths.IsSafe(path) && text != null)
                    folder.Texts.Add(new KeyValuePair<string, string>(path, text));
            }
            if (MediaPaths.IsSafe(folder.FolderName))
                folders.Add(folder);
        }
        return folders;
    }
}

/// <summary>Paths inside a media folder, as another game sends them.</summary>
internal static class MediaPaths
{
    /// <summary>Relative, without "..", drive letters or odd characters: it only names an entry, but stays tame.</summary>
    public static bool IsSafe(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > 260 || path[0] == '/' || path[0] == '\\' || path.Contains(":") || path.Contains(".."))
            return false;
        foreach (char c in path)
        {
            if (c < 32 || c == '<' || c == '>' || c == '"' || c == '|' || c == '?' || c == '*')
                return false;
        }
        return true;
    }
}
