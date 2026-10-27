using System;
using System.Collections.Generic;
using System.IO;

namespace Coop.Assets;

internal enum FileKind : byte
{
    Image,
    Audio,
    Video,

    /// <summary>A mod folder's list (stations.jsonc, tapes.jsonc), only ever read as JSON.</summary>
    List,
}

/// <summary>
/// The only files co-op sends or takes: pictures, songs and videos the game (or a mod) can load,
/// and the small JSON lists of mod media folders. Anything else is never offered and never written.
/// </summary>
internal static class FileTypes
{
    /// <summary>Lists are text; nothing a media folder needs is bigger.</summary>
    public const long MaxListBytes = 256 * 1024;

    private static readonly Dictionary<string, FileKind> Kinds = new Dictionary<string, FileKind>(StringComparer.Ordinal)
    {
        // What the custom shops, the face changer and the wallpaper picker take.
        [".png"] = FileKind.Image,
        [".jpg"] = FileKind.Image,
        [".jpeg"] = FileKind.Image,
        // The cassette shop's filter, and Mixtapes.
        [".ogg"] = FileKind.Audio,
        [".mp3"] = FileKind.Audio,
        [".wav"] = FileKind.Audio,
        [".aif"] = FileKind.Audio,
        [".aiff"] = FileKind.Audio,
        [".flac"] = FileKind.Audio,
        [".mod"] = FileKind.Audio,
        [".it"] = FileKind.Audio,
        [".s3m"] = FileKind.Audio,
        [".xm"] = FileKind.Audio,
        // Custom TV Stations.
        [".webm"] = FileKind.Video,
        [".mp4"] = FileKind.Video,
        [".m4v"] = FileKind.Video,
        [".mov"] = FileKind.Video,
        [".ogv"] = FileKind.Video,
        [".avi"] = FileKind.Video,
        [".wmv"] = FileKind.Video,
        [".mpg"] = FileKind.Video,
        [".mpeg"] = FileKind.Video,
        [".asf"] = FileKind.Video,
        [".dv"] = FileKind.Video,
        [".vp8"] = FileKind.Video,
        [".json"] = FileKind.List,
        [".jsonc"] = FileKind.List,
    };

    public static bool IsKnown(string ext) => ext != null && Kinds.ContainsKey(ext);

    public static bool TryKind(string ext, out FileKind kind)
    {
        kind = default;
        return ext != null && Kinds.TryGetValue(ext, out kind);
    }

    /// <summary>The lower-case extension of a path if it's a known type, else null.</summary>
    public static string ExtOf(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        string ext;
        try { ext = Path.GetExtension(path).ToLowerInvariant(); }
        catch (ArgumentException) { return null; }
        return IsKnown(ext) ? ext : null;
    }

    public static bool IsMedia(string ext) => TryKind(ext, out var kind) && kind != FileKind.List;

    /// <summary>The biggest file of this type this game sends or takes (the setting; lists are always small).</summary>
    public static long MaxBytes(string ext) =>
        TryKind(ext, out var kind) && kind == FileKind.List ? MaxListBytes : (long)CoopMod.Config.Value.MaxFileMb * 1024 * 1024;

    /// <summary>Not over this game's limit (or not known to be).</summary>
    public static bool Fits(in AssetId id) => id.IsValid && id.Size <= MaxBytes(id.Ext);
}

internal static class FileSizes
{
    public static string Describe(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / (1024f * 1024f):0.0} MB" : bytes >= 1024 ? $"{bytes / 1024} KB" : $"{bytes} B";
}
