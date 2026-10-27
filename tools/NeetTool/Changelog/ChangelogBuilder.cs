using System.Text.Json.Nodes;

namespace NeetTool.Changelog;

/// <summary>Builds the changelog.json document from git history.</summary>
public static class ChangelogBuilder
{
    /// <summary>Commit scopes shown to players under a friendlier name.</summary>
    private static readonly Dictionary<string, string> AreaNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["loader"] = "Mod loader",
        ["kit"] = "Framework",
        ["menu"] = "Mod menu",
        ["bridge"] = "Dev bridge",
        ["tool"] = "Command-line tool",
        ["mcp"] = "Command-line tool",
        ["cards"] = "Card game",
        ["coop"] = "Co-op",
        ["coopsteam"] = "Co-op over Steam",
        ["items"] = "Items",
        ["cardexp"] = "Card Expansion",
        ["opensaves"] = "Open Saves",
        ["nofeedback"] = "No Feedback",
        ["pumpkin"] = "Pumpkin Smash",
        ["cheats"] = "Cheat Menu",
        ["openhouse"] = "Open House",
        ["phone"] = "Phone Expansion",
        ["email"] = "Email Expansion",
        ["patches"] = "Unofficial NEET Patches",
        ["tv"] = "Custom TV Stations",
        ["mixtapes"] = "Mixtapes",
        ["randomizer"] = "Randomizer",
        ["perf"] = "Performance Fixes",
        ["updates"] = "Update Checker",
        ["weather"] = "Weather Expansion",
        ["saves"] = "Saves",
        ["apps"] = "Computer apps",
        ["ui"] = "Interface",
        ["fixes"] = "Game fixes",
    };

    private static readonly ChangeKind[] Sections = Enum.GetValues<ChangeKind>();

    /// <param name="releasing">A version being released right now; see <see cref="Releases.Resolve"/>.</param>
    public static JsonObject Build(string repo, string? releasing = null)
    {
        var releases = new JsonArray();
        foreach (var release in Releases.Resolve(repo, releasing))
        {
            var commits = CommitLog.Read(repo, release.Range);
            // Releases without anything for players (only tooling or release commits) are left out.
            if (commits.All(c => c.Lines.Count == 0))
                continue;

            var entry = new JsonObject
            {
                ["version"] = release.Version,
                ["date"] = (release.TagDate ?? commits.Max(c => c.Date)).ToString("yyyy-MM-dd"),
                ["released"] = release.Released,
            };
            foreach (var kind in Sections)
                entry[kind.ToString().ToLowerInvariant()] = Lines(commits, kind);
            releases.Add(entry);
        }

        return new JsonObject { ["releases"] = releases };
    }

    // A mod's lines stay together, the mods in the order their first line comes up; otherwise a release
    // with many lines for one mod has the other mods' lines scattered between them.
    private static JsonArray Lines(List<CommitEntry> commits, ChangeKind kind)
    {
        var items = Collect(commits, kind);
        return new JsonArray(items
            .GroupBy(item => item["area"]?.GetValue<string>() ?? "")
            .SelectMany(area => area)
            .ToArray<JsonNode?>());
    }

    private static List<JsonObject> Collect(List<CommitEntry> commits, ChangeKind kind)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var array = new List<JsonObject>();
        foreach (var commit in commits)
        {
            foreach (var line in commit.Lines)
            {
                if (line.Kind != kind || !seen.Add(line.Text))
                    continue;
                string? area = commit.Scope == null ? null
                    : AreaNames.TryGetValue(commit.Scope, out string? name) ? name : Capitalize(commit.Scope);
                var item = new JsonObject { ["text"] = WithoutArea(line.Text, area) };
                if (area != null)
                    item["area"] = area;
                array.Add(item);
            }
        }
        return array;
    }

    /// <summary>
    /// A new mod's line often starts with its own name ("Mixtapes: your own music..."). The area is
    /// shown in front of every line already, so the name is dropped there.
    /// </summary>
    private static string WithoutArea(string text, string? area)
    {
        if (area == null || !text.StartsWith(area + ":", StringComparison.OrdinalIgnoreCase))
            return text;
        string rest = text[(area.Length + 1)..].TrimStart();
        return rest.Length == 0 ? text : Capitalize(rest);
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
