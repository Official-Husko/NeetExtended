using System;
using System.Collections.Generic;
using System.IO;

namespace NeetModKit.IO;

/// <summary>
/// Content files are JSON with comments: ".jsonc" (the usual name, since nearly all of them have
/// comments) or ".json". When a folder has both "name.json" and "name.jsonc", only the .jsonc counts:
/// that's what an update of a pack that renamed its files leaves behind.
/// </summary>
public static class JsonFiles
{
    public const string Extension = ".jsonc";
    public const string PlainExtension = ".json";

    public static bool IsJson(string path) =>
        path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) || path.EndsWith(PlainExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>The JSON files among <paramref name="files"/>, in their order; <paramref name="skipped"/> gets each .json left out for its .jsonc twin.</summary>
    public static List<string> Pick(IEnumerable<string> files, List<string> skipped = null)
    {
        var all = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
            if (!IsJson(file))
                continue;
            all.Add(file);
            names.Add(file);
        }
        var result = new List<string>(all.Count);
        foreach (string file in all)
        {
            if (file.EndsWith(PlainExtension, StringComparison.OrdinalIgnoreCase) && names.Contains(file + "c"))
                skipped?.Add(file);
            else
                result.Add(file);
        }
        return result;
    }

    /// <summary>"folder/name.jsonc", else "folder/name.json", else null.</summary>
    public static string Find(string folder, string name)
    {
        string jsonc = Path.Combine(folder, name + Extension);
        if (File.Exists(jsonc))
            return jsonc;
        string json = Path.Combine(folder, name + PlainExtension);
        return File.Exists(json) ? json : null;
    }
}
