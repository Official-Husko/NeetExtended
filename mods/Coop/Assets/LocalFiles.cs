using System;
using System.Collections.Generic;
using System.IO;

namespace Coop.Assets;

/// <summary>
/// The files this game has on its own disk to give the others, by content ("hash.ext"): files it
/// offered where they are (its face, its mods' videos and songs) and everything in its cache. Only
/// these are ever read for another game; a path from the network never is.
/// </summary>
internal static class LocalFiles
{
    private struct Source
    {
        public string Path;
        public long Size;
        public long Ticks;
    }

    private static readonly Dictionary<string, Source> Offered = new Dictionary<string, Source>(StringComparer.Ordinal);

    /// <summary>Makes a file on this disk available under its id (it was just hashed).</summary>
    public static void Add(in AssetId id, string path)
    {
        if (!id.IsValid)
            return;
        try
        {
            var info = new FileInfo(path);
            if (info.Exists && info.Length == id.Size)
                Offered[id.FileName] = new Source { Path = info.FullName, Size = info.Length, Ticks = info.LastWriteTimeUtc.Ticks };
        }
        catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Where this game has the file "hash.ext", or null. An offered file that changed since is dropped.</summary>
    public static string Find(string hash, string ext)
    {
        string key = hash + ext;
        if (Offered.TryGetValue(key, out var source))
        {
            try
            {
                var info = new FileInfo(source.Path);
                if (info.Exists && info.Length == source.Size && info.LastWriteTimeUtc.Ticks == source.Ticks)
                    return source.Path;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
            }
            Offered.Remove(key);
        }
        return FileCache.Find(hash, ext);
    }

    public static string Find(in AssetId id) => id.IsValid ? Find(id.Hash, id.Ext) : null;

    /// <summary>Tests: this game uses only files it was sent (the other game on this PC has the same disk).</summary>
    public static bool IgnoreOwnForTests { get; set; }

    /// <summary>This game's copy to show or play: like <see cref="Find(string, string)"/>, minus its own files while testing.</summary>
    public static string FindToUse(string hash, string ext) => IgnoreOwnForTests ? FileCache.Find(hash, ext) : Find(hash, ext);

    public static string FindToUse(in AssetId id) => id.IsValid ? FindToUse(id.Hash, id.Ext) : null;

    public static int OfferedCount => Offered.Count;

    public static void Clear() => Offered.Clear();
}
