using System;
using Coop.Assets.Transfer;
using Coop.Session;

namespace Coop.Assets;

internal enum Resolved
{
    /// <summary>Not a shared path: the game reads it as it always does.</summary>
    NotShared,

    /// <summary>This game has the file; use the local path.</summary>
    Ready,

    /// <summary>A shared file this game doesn't have yet.</summary>
    Pending,
}

/// <summary>
/// What the game hooks use: turning shared paths into this game's copy (or fetching it), and
/// putting a file from this disk into the shared house (<see cref="Share"/>).
/// </summary>
internal static class SharedFiles
{
    public static Resolved Resolve(string path, out string local)
    {
        local = null;
        if (!SharedPaths.TryParse(path, out string hash, out string ext))
            return Resolved.NotShared;
        local = LocalFiles.FindToUse(hash, ext);
        return local != null ? Resolved.Ready : Resolved.Pending;
    }

    /// <summary>Calls <paramref name="ready"/> with this game's copy of a shared file once it's here (in a session).</summary>
    public static void WhenReady(string path, string label, Action<string> ready)
    {
        if (SharedPaths.TryParse(path, out string hash, out string ext))
            Downloads.Want(AssetId.OfPath(hash, ext), label, ready);
    }

    /// <summary>
    /// A file from this disk goes into the shared house (bought in a custom shop, a photo, a
    /// wallpaper): it's copied into the cache under its hash and the shared path comes back, which the
    /// item keeps from now on. On a client the host is told it can have it. Outside a session, or for
    /// a file that can't be shared (too big, wrong type, gone), the path comes back unchanged.
    /// </summary>
    public static string Share(string path, string label)
    {
        if (!CoopSession.IsActive || !SharedPaths.IsLocalFile(path))
            return path;
        var id = FileHasher.ImportNow(path, out string error);
        if (!id.IsValid)
        {
            CoopMod.Log.Info($"Files: {label} stays on this machine ({System.IO.Path.GetFileName(path)}): {error ?? "it can't be read"}.");
            return path;
        }
        FileLabels.Set(id, label);
        Hub.Offer(id);
        return FileCache.PathOf(id);
    }

    /// <summary>Like <see cref="Share"/>, in the background; <paramref name="done"/> gets the shared path, or null.</summary>
    public static void ShareLater(string path, Action<string> done)
    {
        if (!SharedPaths.IsLocalFile(path))
        {
            done(null);
            return;
        }
        FileHasher.Import(path, id =>
        {
            if (!id.IsValid || !CoopSession.IsActive)
            {
                done(null);
                return;
            }
            FileLabels.SetIfNew(id.FileName, System.IO.Path.GetFileName(path));
            Hub.Offer(id);
            done(FileCache.PathOf(id));
        });
    }

    /// <summary>Same content? (Two shared paths written by different games still name the same file.)</summary>
    public static bool SameFile(string a, string b) =>
        SharedPaths.TryParse(a, out string hashA, out string extA) && SharedPaths.TryParse(b, out string hashB, out string extB) &&
        hashA == hashB && extA == extB;
}
