using System.Collections.Generic;
using Coop.Session;
using Coop.World;
using Coop.World.Spawning;

namespace Coop.Assets.Hooks;

/// <summary>
/// Which plain file paths in the house the game may read. Everything that goes into the house in a
/// session gets a shared path; a plain full path is a file on one machine:
/// - on a client it's the host's (from the host's save), and the client's disk has nothing to do
///   with it: it isn't read there (the host shares those when it starts hosting);
/// - on the host, one arriving with another game's item would be a file on the host's disk that
///   nobody here picked, so it's dropped from the item.
/// </summary>
internal static class LoadRules
{
    private static readonly HashSet<string> Told = new HashSet<string>();

    /// <summary>False when the game shouldn't read <paramref name="path"/> here.</summary>
    public static bool MayRead(string path)
    {
        if (!SharedPaths.IsLocalFile(path))
            return true;
        if (WorldSide.IsClient)
        {
            if (Told.Add(path))
                CoopMod.Log.Debug($"Files: not reading {path} here; it's a file on the host's machine.");
            return false;
        }
        return true;
    }

    /// <summary>Host: a plain path that came with another game's item (to be dropped).</summary>
    public static bool FromOtherGame(string path) =>
        CoopSession.IsHost && SpawnContext.IsApplying && SharedPaths.IsLocalFile(path);
}
