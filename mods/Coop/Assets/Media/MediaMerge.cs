using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Coop.Assets.Media;

/// <summary>
/// Everyone's media folders as one list for the session: the host's first, as they are, then each
/// player's in slot order. Most players have the same mods, so a later folder leaves out the files
/// an earlier folder of its kind already has (by content), and its list file's entries for them; a
/// list file that's the same as an earlier one goes, and so does a folder with nothing new left.
/// Every game builds its TV and tapes from the same list, so channel numbers and tapes match.
/// </summary>
internal static class MediaMerge
{
    private static readonly JsonLoadSettings Json = new JsonLoadSettings { CommentHandling = CommentHandling.Ignore };

    /// <param name="bySlot">Each player's own folders, host (slot 0) first, then by slot.</param>
    public static List<MediaFolderInfo> Merge(IEnumerable<KeyValuePair<byte, List<MediaFolderInfo>>> bySlot)
    {
        var merged = new List<MediaFolderInfo>();
        var ids = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var paths = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var texts = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        bool first = true;
        foreach (var player in bySlot)
        {
            foreach (var folder in player.Value)
            {
                var kept = first ? Copy(folder, player.Key) : Trim(folder, player.Key, Seen(ids, folder.FolderName),
                    Seen(paths, folder.ModId + "/" + folder.FolderName), Seen(texts, folder.FolderName));
                if (kept == null)
                    continue;
                Remember(kept, Seen(ids, folder.FolderName), Seen(paths, folder.ModId + "/" + folder.FolderName), Seen(texts, folder.FolderName));
                merged.Add(kept);
            }
            first = false;
        }
        return merged;
    }

    private static MediaFolderInfo Copy(MediaFolderInfo folder, byte owner)
    {
        var copy = new MediaFolderInfo { FolderName = folder.FolderName, ModId = folder.ModId, Owner = owner };
        copy.Files.AddRange(folder.Files);
        copy.Texts.AddRange(folder.Texts);
        return copy;
    }

    // What's new in a folder, or null when nothing is.
    private static MediaFolderInfo Trim(MediaFolderInfo folder, byte owner, HashSet<string> seenIds, HashSet<string> seenPaths, HashSet<string> seenTexts)
    {
        var kept = new MediaFolderInfo { FolderName = folder.FolderName, ModId = folder.ModId, Owner = owner };
        var dropped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in folder.Files)
        {
            // Not hashed yet or too big to share: the same place in the same mod is taken to be the same file.
            bool known = file.Value.IsValid ? seenIds.Contains(file.Value.FileName) : seenPaths.Contains(file.Key);
            if (known)
                dropped.Add(Normalize(file.Key));
            else
                kept.Files.Add(file);
        }
        foreach (var text in folder.Texts)
        {
            if (seenTexts.Contains(text.Value))
                continue;
            kept.Texts.Add(new KeyValuePair<string, string>(text.Key, dropped.Count > 0 ? WithoutEntries(text.Value, dropped) : text.Value));
        }
        return kept.Files.Count > 0 || kept.Texts.Count > 0 ? kept : null;
    }

    private static void Remember(MediaFolderInfo folder, HashSet<string> seenIds, HashSet<string> seenPaths, HashSet<string> seenTexts)
    {
        foreach (var file in folder.Files)
        {
            if (file.Value.IsValid)
                seenIds.Add(file.Value.FileName);
            seenPaths.Add(file.Key);
        }
        foreach (var text in folder.Texts)
            seenTexts.Add(text.Value);
    }

    // A list file without its entries for files that were left out ({ "stations": [ { "file": ... } ] }).
    private static string WithoutEntries(string text, HashSet<string> dropped)
    {
        JObject root;
        try
        {
            root = JObject.Parse(text, Json);
        }
        catch (JsonException)
        {
            return text; // Broken lists are reported by the mod reading them, the same in every game.
        }
        foreach (var property in root.Properties())
        {
            if (!(property.Value is JArray entries))
                continue;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i] is JObject entry && entry.Value<string>("file") is string file && dropped.Contains(Normalize(file)))
                    entries.RemoveAt(i);
            }
        }
        return root.ToString(Formatting.None);
    }

    private static string Normalize(string path)
    {
        path = path.Replace('\\', '/');
        while (path.StartsWith("./", StringComparison.Ordinal))
            path = path.Substring(2);
        return path;
    }

    private static HashSet<string> Seen(Dictionary<string, HashSet<string>> sets, string key)
    {
        if (!sets.TryGetValue(key, out var set))
            sets[key] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return set;
    }
}
