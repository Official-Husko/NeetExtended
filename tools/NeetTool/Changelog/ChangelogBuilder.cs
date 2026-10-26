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
        ["items"] = "Items",
        ["cardexp"] = "Card Expansion",
        ["opensaves"] = "Open Saves",
        ["nofeedback"] = "No Feedback",
        ["pumpkin"] = "Pumpkin Smash",
        ["cheats"] = "Cheat Menu",
        ["openhouse"] = "Open House",
        ["saves"] = "Saves",
        ["apps"] = "Computer apps",
        ["ui"] = "Interface",
        ["fixes"] = "Game fixes",
    };

    private static readonly ChangeKind[] Sections = Enum.GetValues<ChangeKind>();

    public static JsonObject Build(string repo)
    {
        var releases = new JsonArray();
        foreach (var release in Releases.Resolve(repo))
        {
            var commits = CommitLog.Read(repo, release.Range);
            if (commits.Count == 0)
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

    private static JsonArray Lines(List<CommitEntry> commits, ChangeKind kind)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var array = new JsonArray();
        foreach (var commit in commits)
        {
            foreach (var line in commit.Lines)
            {
                if (line.Kind != kind || !seen.Add(line.Text))
                    continue;
                var item = new JsonObject { ["text"] = line.Text };
                if (commit.Scope != null)
                    item["area"] = AreaNames.TryGetValue(commit.Scope, out string? name) ? name : Capitalize(commit.Scope);
                array.Add(item);
            }
        }
        return array;
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
