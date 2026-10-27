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

    /// <summary>"Id@version" of every running mod that shapes the shared house, sorted.</summary>
    public static List<string> Shared() =>
        ModRegistry.Mods
            .Where(m => m.State == ModState.Loaded && !LocalOnly.Contains(m.Id))
            .Select(m => m.Id + "@" + m.Manifest.Version)
            .OrderBy(s => s, System.StringComparer.Ordinal)
            .ToList();

    /// <summary>Null when the joiner's list matches the host's; otherwise a short reason for the joiner.</summary>
    public static string Mismatch(List<string> joiner)
    {
        var host = Shared();
        var missing = host.Except(joiner).Select(Describe).ToList();
        var extra = joiner.Except(host).Select(Describe).ToList();
        if (missing.Count == 0 && extra.Count == 0)
            return null;
        var parts = new List<string>();
        if (missing.Count > 0)
            parts.Add("the host has " + Join(missing));
        if (extra.Count > 0)
            parts.Add("you have " + Join(extra));
        return "Different mods: " + string.Join("; ", parts) + ".";
    }

    private static string Describe(string entry) => entry.Replace("@", " ");

    // Disconnect reasons travel in one packet, so long lists are cut.
    private static string Join(List<string> items) =>
        items.Count <= 3 ? string.Join(", ", items) : string.Join(", ", items.Take(3)) + $" and {items.Count - 3} more";
}
