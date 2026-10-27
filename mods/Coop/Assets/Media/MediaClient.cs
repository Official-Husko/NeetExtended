using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Coop.Assets.Transfer;
using NeetLoader.Mods;
using NeetModKit.IO;
using UnityEngine;

namespace Coop.Assets.Media;

/// <summary>
/// Client: the TV stations and tapes are the host's. The host's media folders stand in for this
/// game's own (<see cref="ModContent.Substitute"/>), made of the cache's copies: every file is listed
/// right away, so the channel numbers and tape items match the host's, and each one plays once it
/// has arrived (the mods load their folders again then). Leaving gives this game its own back.
/// </summary>
internal static class MediaClient
{
    private const float RefreshDelay = 1f;

    private static readonly HashSet<string> Dirty = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static float _refreshAt = -1f;

    public static void Apply(List<MediaFolderInfo> listing)
    {
        var owner = CoopMod.Instance;
        if (owner == null)
            return;
        int wanted = 0;
        foreach (string name in MediaHost.FolderNames)
        {
            var folders = new List<ModContent.Folder>();
            foreach (var info in listing)
            {
                if (string.Equals(info.FolderName, name, StringComparison.OrdinalIgnoreCase))
                    folders.Add(Build(info, ref wanted));
            }
            ModContent.Substitute(owner, name, folders);
        }
        if (wanted > 0)
            CoopMod.Log.Info($"Files: the host's TV and tapes need {wanted} file(s) this game doesn't have yet; getting them.");
    }

    /// <summary>This game's own folders again (left the session).</summary>
    public static void Release()
    {
        Dirty.Clear();
        _refreshAt = -1f;
        if (CoopMod.Instance == null)
            return;
        foreach (string name in MediaHost.FolderNames)
            ModContent.Substitute(CoopMod.Instance, name, null);
    }

    public static void Tick()
    {
        if (_refreshAt < 0f || Time.unscaledTime < _refreshAt)
            return;
        _refreshAt = -1f;
        foreach (string name in Dirty)
            ModContent.MarkDirty(name);
        Dirty.Clear();
    }

    private static ModContent.Folder Build(MediaFolderInfo info, ref int wanted)
    {
        // Where the folder would be here, so messages name it like a local one.
        var mod = ModRegistry.Find(info.ModId) ?? CoopMod.Instance.Info;
        string path = Path.Combine(mod.Directory, info.FolderName);
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in info.Files)
        {
            var id = file.Value;
            if (!id.IsValid)
            {
                // Too big for the host to send: listed (so the lists match) but never there.
                files[file.Key] = Path.Combine(FileCache.Folder, "unshared", file.Key);
                continue;
            }
            string local = LocalFiles.FindToUse(id);
            files[file.Key] = local ?? FileCache.PathOf(id);
            if (local != null)
                continue;
            if (!Downloads.IsGetting(id.FileName))
                wanted++;
            string folderName = info.FolderName;
            Downloads.Want(id, Describe(info.FolderName, file.Key), _ => Refresh(folderName));
        }
        foreach (var text in info.Texts)
        {
            string stored = StoreText(text.Value);
            if (stored != null)
                files[text.Key] = stored;
        }
        return new ModContent.Folder(mod, path, files);
    }

    private static void Refresh(string folderName)
    {
        Dirty.Add(folderName);
        if (_refreshAt < 0f)
            _refreshAt = Time.unscaledTime + RefreshDelay;
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
            CoopMod.Log.Warn($"Files: couldn't keep the host's list file: {e.Message}");
            return null;
        }
    }

    private static string Describe(string folderName, string file) =>
        (folderName == "tv" ? "the TV station " : folderName == "mixtapes" ? "the mixtape " : "") + file;
}
