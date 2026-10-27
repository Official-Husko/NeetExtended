using System;
using System.Collections.Generic;

namespace Coop.Assets;

/// <summary>
/// What a file is called, by content, for the transfer notices: "clip.webm", "a photo",
/// "Bob's face". Files only known by their hash are "a file".
/// </summary>
internal static class FileLabels
{
    private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal);

    public static void Set(in AssetId id, string label)
    {
        if (id.IsValid && !string.IsNullOrEmpty(label))
            Labels[id.FileName] = label;
    }

    /// <summary>Only if it has no name yet (a file's own name beats "a file for Bob").</summary>
    public static void SetIfNew(string key, string label)
    {
        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(label) && !Labels.ContainsKey(key))
            Labels[key] = label;
    }

    public static string Of(string key) => key != null && Labels.TryGetValue(key, out string label) ? label : "a file";

    public static void Clear() => Labels.Clear();
}
