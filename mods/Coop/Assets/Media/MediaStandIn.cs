using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Coop.Assets.Transfer;
using Coop.Session;
using NeetLoader.Mods;
using NeetModKit.IO;

namespace Coop.Assets.Media;

/// <summary>
/// The session's TV stations and tapes in this game: the merged list stands in for this game's own
/// media folders (<see cref="ModContent.Substitute"/>). A folder of this game's own points at its
/// real files, everyone else's at the cache. Every file is listed right away, so channel numbers and
/// tape items match everyone's; one that hasn't arrived yet plays once it's here
/// (<see cref="ModContent.ReportArrived"/>), without the folders loading again. Leaving gives this
/// game its own folders back.
/// </summary>
internal static class MediaStandIn
{
    private static readonly List<AssetId> WantedList = new List<AssetId>();
    private static readonly HashSet<string> WantedKeys = new HashSet<string>(StringComparer.Ordinal);
    private static bool _standing;
    private static List<MediaFolderInfo> _last = new List<MediaFolderInfo>();

    /// <summary>The files of the last list this game doesn't have (what a joiner gets before its house).</summary>
    public static IReadOnlyList<AssetId> Wanted => WantedList;

    public static void Init() => Downloads.Arrived += OnArrived;

    public static void Apply(List<MediaFolderInfo> listing, byte localSlot)
    {
        var owner = CoopMod.Instance;
        if (owner == null)
            return;
        // Only the host's own folders: its real ones do.
        if (localSlot == CoopPlayer.HostSlot && listing.TrueForAll(f => f.Owner == localSlot))
        {
            if (_standing)
                Release();
            return;
        }

        WantedList.Clear();
        WantedKeys.Clear();
        _last = listing;
        int wanted = 0;
        foreach (string name in LocalMedia.FolderNames)
        {
            var folders = new List<ModContent.Folder>();
            foreach (var info in listing)
            {
                if (string.Equals(info.FolderName, name, StringComparison.OrdinalIgnoreCase))
                    folders.Add(Build(info, localSlot, ref wanted));
            }
            ModContent.Substitute(owner, name, folders);
        }
        _standing = true;
        if (wanted > 0)
            CoopMod.Log.Info($"Files: the session's TV stations and tapes need {wanted} file(s) this game doesn't have yet; getting them.");
    }

    /// <summary>For the bridge: the folders this game shows in place of its own.</summary>
    public static string Describe()
    {
        if (!_standing)
            return "Showing this game's own folders.";
        var text = new StringBuilder();
        foreach (var folder in _last)
        {
            int here = 0;
            foreach (var file in folder.Files)
            {
                if (folder.Owner == CoopSession.Local?.Slot || LocalFiles.FindToUse(file.Value) != null)
                    here++;
            }
            text.Append(folder.FolderName).Append(' ').Append(folder.ModId).Append(" of slot ").Append(folder.Owner).Append(": ")
                .Append(here).Append('/').Append(folder.Files.Count).Append(" files here, ").Append(folder.Texts.Count).Append(" list(s)\n");
        }
        return text.ToString();
    }

    /// <summary>This game's own folders again (left the session).</summary>
    public static void Release()
    {
        WantedList.Clear();
        WantedKeys.Clear();
        _standing = false;
        if (CoopMod.Instance == null)
            return;
        foreach (string name in LocalMedia.FolderNames)
            ModContent.Substitute(CoopMod.Instance, name, null);
    }

    private static ModContent.Folder Build(MediaFolderInfo info, byte localSlot, ref int wanted)
    {
        var mod = ModRegistry.Find(info.ModId) ?? CoopMod.Instance.Info;
        bool own = info.Owner == localSlot;
        string real = (own ? LocalMedia.DirectoryOf(info.ModId, info.FolderName) : null) ?? Path.Combine(mod.Directory, info.FolderName);
        // Where the folder would be here, so messages name it like a local one (and say whose it is).
        string path = own || info.Owner == CoopPlayer.HostSlot ? real : real + " (" + OwnerName(info.Owner) + ")";
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in info.Files)
        {
            if (own)
            {
                files[file.Key] = Path.Combine(real, file.Key);
                continue;
            }
            var id = file.Value;
            if (!id.IsValid)
            {
                // Too big to share, or not hashed yet: listed (so the lists match) but not here.
                files[file.Key] = Path.Combine(FileCache.Folder, "unshared", file.Key);
                continue;
            }
            string local = LocalFiles.FindToUse(id);
            files[file.Key] = local ?? FileCache.PathOf(id);
            if (local != null)
                continue;
            if (!Downloads.IsGetting(id.FileName))
                wanted++;
            if (WantedKeys.Add(id.FileName))
                WantedList.Add(id);
            FileLabels.Set(id, file.Key);
            Downloads.Want(id, SessionMedia.Kind(info.FolderName) + " " + file.Key, null);
        }
        foreach (var text in info.Texts)
        {
            string stored = StoreText(text.Value);
            if (stored != null)
                files[text.Key] = stored;
        }
        return new ModContent.Folder(mod, path, files);
    }

    // A station or tape whose file was missing can play now.
    private static void OnArrived(AssetId id)
    {
        if (WantedKeys.Contains(id.FileName))
            ModContent.ReportArrived(FileCache.PathOf(id));
    }

    private static string OwnerName(byte slot)
    {
        string name = CoopSession.FindSlot(slot)?.Name ?? "";
        var clean = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_')
                clean.Append(c);
        }
        return clean.Length > 0 ? clean.ToString().Trim() : "player " + (slot + 1);
    }

    // A list file goes into the cache under its own hash, like any other file.
    private static string StoreText(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        string hash;
        using (var sha = SHA256.Create())
            hash = AssetId.ToHex(sha.ComputeHash(bytes));
        var id = new AssetId(hash, bytes.Length, ".jsonc");
        string path = FileCache.PathOf(id);
        try
        {
            if (FileCache.Find(id) == null)
                File.WriteAllBytes(path, bytes);
            return path;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            CoopMod.Log.Warn($"Files: couldn't keep a list file of the session's media: {e.Message}");
            return null;
        }
    }
}
