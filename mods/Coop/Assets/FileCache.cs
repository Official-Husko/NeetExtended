using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeetLoader.Runtime;

namespace Coop.Assets;

/// <summary>
/// Mods/Coop/cache: files from the other games, and this game's own files that went into the shared
/// house, each named by its content ("hash.ext"), so it's sent once and kept across sessions. A
/// second game on the same PC (tests) has its own folder (cache-2), so it really gets its files sent.
///
/// When it's over the size setting, the files used longest ago go first; files used since the game
/// started are kept. Using a file touches its write time.
/// </summary>
internal static class FileCache
{
    public const string PartExt = ".part";

    private static DateTime _runStarted;

    public static string Folder { get; private set; }

    public static void Init(string modDirectory)
    {
        Folder = Path.Combine(modDirectory, "cache" + Instance.Suffix);
        Directory.CreateDirectory(Folder);
        _runStarted = DateTime.UtcNow - TimeSpan.FromSeconds(UnityEngine.Time.realtimeSinceStartup);
    }

    /// <summary>Where a file is (or would be) kept. The hash and extension are checked by whoever made the id.</summary>
    public static string PathOf(in AssetId id) => Path.Combine(Folder, id.FileName);

    public static string PartOf(in AssetId id) => Path.Combine(Folder, id.FileName + PartExt);

    /// <summary>The cached file if it's here with the right size, else null. Marks it used.</summary>
    public static string Find(in AssetId id)
    {
        if (!id.IsValid)
            return null;
        string path = PathOf(id);
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length != id.Size)
                return null;
            Touch(info);
            return path;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Same, by hash and extension only (a shared path doesn't carry the size).</summary>
    public static string Find(string hash, string ext)
    {
        string path = Path.Combine(Folder, hash + ext);
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                return null;
            Touch(info);
            return path;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Drops the oldest files until the cache fits its setting again.</summary>
    public static void Trim()
    {
        long cap = (long)CoopMod.Config.Value.CacheMb * 1024 * 1024;
        try
        {
            var files = new DirectoryInfo(Folder).GetFiles()
                .Where(f => AssetId.IsHash(Path.GetFileNameWithoutExtension(f.Name)))
                .OrderBy(f => f.LastWriteTimeUtc)
                .ToList();
            long total = files.Sum(f => f.Length);
            int removed = 0;
            foreach (var file in files)
            {
                if (total <= cap)
                    break;
                if (file.LastWriteTimeUtc >= _runStarted)
                    continue;
                total -= file.Length;
                file.Delete();
                removed++;
            }
            if (removed > 0)
                CoopMod.Log.Info($"Files: the cache was over {CoopMod.Config.Value.CacheMb} MB; removed the {removed} used longest ago.");
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            CoopMod.Log.Warn($"Files: couldn't tidy the cache: {e.Message}");
        }
    }

    /// <summary>Removes half-written files nothing is fetching any more (left by a crash or a quit).</summary>
    public static void RemoveStaleParts(ICollection<string> keep)
    {
        try
        {
            foreach (string part in Directory.GetFiles(Folder, "*" + PartExt))
            {
                if (!keep.Contains(part) && File.GetLastWriteTimeUtc(part) < _runStarted)
                    File.Delete(part);
            }
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
        }
    }

    private static void Touch(FileInfo info)
    {
        if (info.LastWriteTimeUtc < _runStarted)
            info.LastWriteTimeUtc = DateTime.UtcNow;
    }
}
