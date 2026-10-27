using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Coop.Assets;

/// <summary>
/// Remembers the hash of each file this game has read, by path, size and write time, in the cache
/// folder (hashes.json), so unchanged files (TV videos, songs) aren't read again every session.
/// Used from the hashing thread and the main thread.
/// </summary>
internal static class HashIndex
{
    private const string FileName = "hashes.json";
    // Entries for files that are gone are dropped when the index loads; this caps the rest.
    private const int MaxEntries = 4000;

    private static readonly object Lock = new object();
    private static Dictionary<string, string> _hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static bool _changed;

    private static string IndexPath => Path.Combine(FileCache.Folder, FileName);

    public static void Load()
    {
        try
        {
            if (!File.Exists(IndexPath))
                return;
            var loaded = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(IndexPath));
            var kept = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (loaded != null)
            {
                foreach (var pair in loaded)
                {
                    if (kept.Count < MaxEntries && AssetId.IsHash(pair.Value) && File.Exists(PathOfKey(pair.Key)))
                        kept[pair.Key] = pair.Value;
                }
            }
            lock (Lock)
                _hashes = kept;
        }
        catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException)
        {
            CoopMod.Log.Warn($"Files: couldn't read {FileName}, hashing again as needed: {e.Message}");
        }
    }

    public static string Find(FileInfo file)
    {
        lock (Lock)
            return _hashes.TryGetValue(Key(file), out string hash) ? hash : null;
    }

    public static void Remember(FileInfo file, string hash)
    {
        lock (Lock)
        {
            if (_hashes.Count >= MaxEntries)
                _hashes.Clear();
            _hashes[Key(file)] = hash;
            _changed = true;
        }
    }

    public static void SaveIfChanged()
    {
        string json;
        lock (Lock)
        {
            if (!_changed)
                return;
            _changed = false;
            json = JsonConvert.SerializeObject(_hashes, Formatting.Indented);
        }
        try
        {
            File.WriteAllText(IndexPath, json);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            CoopMod.Log.Warn($"Files: couldn't write {FileName}: {e.Message}");
        }
    }

    // "size|ticks|path": the path last, since it may hold any character.
    private static string Key(FileInfo file) => file.Length + "|" + file.LastWriteTimeUtc.Ticks + "|" + file.FullName;

    private static string PathOfKey(string key)
    {
        int second = key.IndexOf('|', key.IndexOf('|') + 1);
        return second >= 0 ? key.Substring(second + 1) : key;
    }
}
