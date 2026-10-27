using Coop.Session;

namespace Coop.Assets.Items;

/// <summary>
/// The computer is shared, so a wallpaper picked on it goes to everyone: the picked file goes into
/// the cache, the computer keeps its shared path (and saves it on the host), and the others load it.
/// </summary>
internal static class WallpaperShare
{
    private static string _picked;

    /// <summary>While the picker loads the file this player just picked (it's theirs, so it's read).</summary>
    public static bool Picking { get; private set; }

    public static void Begin(string[] paths)
    {
        Picking = true;
        _picked = null;
        if (!CoopSession.IsActive || paths == null || paths.Length == 0)
            return;
        paths[0] = SharedFiles.Share(paths[0], "the wallpaper");
        _picked = paths[0];
    }

    public static void End()
    {
        Picking = false;
        if (_picked != null && SharedPaths.IsShared(_picked))
            ItemPaths.AnnounceWallpaper(_picked);
        _picked = null;
    }
}
