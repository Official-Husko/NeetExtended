using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace NeetModKit.IO;

/// <summary>One media file from a mod's folder, with its entry from the folder's list file if it has one.</summary>
public sealed class MediaFile
{
    /// <summary>Full path.</summary>
    public string Path;

    /// <summary>"ModId/folder/file.ext", for messages.</summary>
    public string Source;

    /// <summary>The file's entry in the list (names, volume, price, ...), or null for files the list doesn't mention.</summary>
    public JObject Entry;

    /// <summary>The file name without extension, a default name for menus.</summary>
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
}

/// <summary>
/// Reads a folder of media files (videos for the TV, songs for mixtapes) from a <see cref="ModContent"/>
/// folder: every file of the given types, sub folders included. An optional list file in the folder,
/// like <c>{ "stations": [ { "file": "a.webm", "name": "A" } ] }</c>, sets up the files it names; they
/// come first in its order, the rest follow by file name.
/// </summary>
public static class MediaFolder
{
    private static readonly JsonLoadSettings Json = new JsonLoadSettings { CommentHandling = CommentHandling.Ignore };

    /// <param name="extensions">Lower-case extensions with the dot: ".mp4", ".ogg".</param>
    /// <param name="listFile">The list's file name ("stations.json"); it doesn't have to exist.</param>
    /// <param name="listKey">The array in the list ("stations").</param>
    /// <param name="list">The whole list, for anything else in it, or null.</param>
    public static List<MediaFile> Read(ModContent.Folder folder, ICollection<string> extensions, string listFile, string listKey,
        List<string> problems, out JObject list)
    {
        var files = Directory.GetFiles(folder.Path, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<MediaFile>();
        list = ReadList(folder, listFile, problems);
        if (list?[listKey] is JArray entries)
            AddListed(folder, listFile, entries, files, result, problems);

        foreach (string file in files)
            result.Add(new MediaFile { Path = file, Source = folder.Display(file) });
        return result;
    }

    private static JObject ReadList(ModContent.Folder folder, string listFile, List<string> problems)
    {
        string path = System.IO.Path.Combine(folder.Path, listFile);
        if (!File.Exists(path))
            return null;
        try
        {
            return JObject.Parse(File.ReadAllText(path), Json);
        }
        catch (Exception e)
        {
            problems.Add($"{folder.Display(path)}: {e.Message}");
            return null;
        }
    }

    /// <summary>Files named in the list, in its order. They're taken off <paramref name="files"/>.</summary>
    private static void AddListed(ModContent.Folder folder, string listFile, JArray entries, List<string> files,
        List<MediaFile> result, List<string> problems)
    {
        string where = folder.Display(System.IO.Path.Combine(folder.Path, listFile));
        for (int i = 0; i < entries.Count; i++)
        {
            if (!(entries[i] is JObject entry) || entry.Value<string>("file") == null)
            {
                problems.Add($"{where}, entry {i + 1}: needs a \"file\".");
                continue;
            }

            string name = entry.Value<string>("file");
            string file = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder.Path, name));
            int index = files.FindIndex(f => string.Equals(System.IO.Path.GetFullPath(f), file, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                problems.Add($"{where}, entry {i + 1}: there's no file \"{name}\" here (or it isn't a type this folder takes).");
                continue;
            }
            files.RemoveAt(index);
            result.Add(new MediaFile { Path = file, Source = folder.Display(file), Entry = entry });
        }
    }
}
