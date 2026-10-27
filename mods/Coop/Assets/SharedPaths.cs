using System.IO;

namespace Coop.Assets;

/// <summary>
/// A shared path names a file by its content: its file name is "hash.ext" (a file in some game's
/// co-op cache, like ".../Mods/Coop/cache/3fa9...c1.png"). Items, boxes and the computer keep these
/// instead of a path on one player's disk, and every game reads the same bytes from its own cache,
/// whatever folder the path was written in. They also work as plain paths in the game that wrote them.
/// </summary>
internal static class SharedPaths
{
    public static bool TryParse(string path, out string hash, out string ext)
    {
        hash = null;
        ext = null;
        if (string.IsNullOrEmpty(path) || path.Length < 66)
            return false;
        int slash = path.LastIndexOfAny(Separators);
        int dot = path.LastIndexOf('.');
        if (dot <= slash || dot - slash - 1 != AssetId.HashBytes * 2)
            return false;
        string name = path.Substring(slash + 1, AssetId.HashBytes * 2);
        string extension = path.Substring(dot).ToLowerInvariant();
        if (!AssetId.IsHash(name) || !FileTypes.IsKnown(extension))
            return false;
        hash = name;
        ext = extension;
        return true;
    }

    public static bool IsShared(string path) => TryParse(path, out _, out _);

    /// <summary>A full path that isn't a shared one: a file on one player's disk.</summary>
    public static bool IsLocalFile(string path)
    {
        if (string.IsNullOrEmpty(path) || IsShared(path))
            return false;
        try
        {
            return Path.IsPathRooted(path);
        }
        catch (System.ArgumentException)
        {
            return false;
        }
    }

    private static readonly char[] Separators = { '/', '\\' };
}
