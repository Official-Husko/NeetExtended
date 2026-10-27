using System.Collections.Generic;
using System.Linq;
using NeetLoader.Mods;

namespace Coop.Session;

/// <summary>
/// Which mods both sides have to share. Items, calls, prices and doors come from mods, so a joiner
/// missing one (or with another version) would see a different house. Mods that only change your
/// own game (tools, menus, speed, the save format) may differ.
/// </summary>
internal static class ModList
{
    private static readonly HashSet<string> LocalOnly = new HashSet<string>
    {
        "DevBridge", "CheatMenu", "PerformanceFixes", "NoFeedback", "OpenSaves", "CoopSteam",
    };

    /// <summary>Whether a mod shapes the shared house (the host decides it), rather than only this game.</summary>
    public static bool IsShared(string modId) => !LocalOnly.Contains(modId);

    /// <summary>"Id@version" of every running mod that shapes the shared house, sorted.</summary>
    public static List<string> Shared() =>
        ModRegistry.Mods
            .Where(m => m.State == ModState.Loaded && !LocalOnly.Contains(m.Id))
            .Select(m => m.Id + "@" + m.Manifest.Version)
            .OrderBy(s => s, System.StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// "Id@version" of every installed mod that shapes the shared house, running or not: what a joiner
    /// could run. A mod that's off (or off by default, like the Randomizer) still counts, since the
    /// joiner loads what the host runs (<see cref="ModSync"/>).
    /// </summary>
    public static List<string> Available() =>
        ModRegistry.Mods
            .Where(m => m.State != ModState.Failed && !LocalOnly.Contains(m.Id))
            .Select(m => m.Id + "@" + m.Manifest.Version)
            .OrderBy(s => s, System.StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Null when the joiner can run every shared mod the host runs (<paramref name="joiner"/> is its
    /// <see cref="Available"/> list); otherwise a short reason for the joiner. Mods the joiner runs
    /// and the host doesn't are fine: the joiner unloads them for the session.
    /// </summary>
    public static string Mismatch(List<string> joiner)
    {
        var missing = Shared().Except(joiner).Select(Describe).ToList();
        if (missing.Count == 0)
            return null;
        return "Missing mods: the host runs " + Join(missing) + ". Install the same version to join.";
    }

    private static string Describe(string entry) => entry.Replace("@", " ");

    // Disconnect reasons travel in one packet, so long lists are cut.
    private static string Join(List<string> items) =>
        items.Count <= 3 ? string.Join(", ", items) : string.Join(", ", items.Take(3)) + $" and {items.Count - 3} more";
}
