using System.Text.RegularExpressions;

namespace NeetTool.Changelog;

/// <summary>A version and the git range holding its commits.</summary>
public sealed record ReleaseRange(string Version, string Range, bool Released, DateOnly? TagDate);

/// <summary>
/// Splits history into releases: every vX.Y.Z tag closes a release, and commits after the last
/// tag belong to the upcoming version from Directory.Build.props.
/// </summary>
public static partial class Releases
{
    /// <param name="releasing">
    /// A version being released right now, before its tag exists: the commits after the last tag
    /// become that release, dated today.
    /// </param>
    public static List<ReleaseRange> Resolve(string repo, string? releasing = null)
    {
        var tags = Git.Run(repo, "for-each-ref", "refs/tags/v*", "--format=%(refname:short)\u001f%(creatordate:short)")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Split('\u001f'))
            .Select(p => (Tag: p[0], Version: ParseVersion(p[0][1..]), Date: DateOnly.Parse(p[1])))
            .Where(t => t.Version != null)
            .OrderBy(t => t.Version)
            .ToList();

        var releases = new List<ReleaseRange>();
        string? previous = null;
        foreach (var t in tags)
        {
            releases.Add(new ReleaseRange(t.Tag[1..], previous == null ? t.Tag : $"{previous}..{t.Tag}", true, t.Date));
            previous = t.Tag;
        }

        string range = previous == null ? "HEAD" : $"{previous}..HEAD";
        if (releasing != null)
        {
            releases.Add(new ReleaseRange(releasing, range, true, DateOnly.FromDateTime(DateTime.Now)));
        }
        else
        {
            string upcoming = UpcomingVersion(repo);
            bool alreadyTagged = tags.Any(t => t.Tag == "v" + upcoming);
            releases.Add(new ReleaseRange(alreadyTagged ? "Unreleased" : upcoming, range, false, null));
        }

        releases.Reverse(); // Newest first.
        return releases;
    }

    /// <summary>The &lt;Version&gt; from Directory.Build.props.</summary>
    public static string UpcomingVersion(string repo)
    {
        string props = File.ReadAllText(Path.Combine(repo, "Directory.Build.props"));
        var m = VersionElement().Match(props);
        return m.Success ? m.Groups[1].Value.Trim() : "0.0.0";
    }

    /// <summary>The newest vX.Y.Z tag, or null before the first release.</summary>
    public static (string Tag, Version Version)? LastTag(string repo)
    {
        var last = Git.Run(repo, "tag", "--list", "v*")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => (Tag: t.Trim(), Version: ParseVersion(t.Trim()[1..])))
            .Where(t => t.Version != null)
            .OrderByDescending(t => t.Version)
            .FirstOrDefault();
        return last.Tag == null ? null : (last.Tag, last.Version!);
    }

    private static Version? ParseVersion(string text) => Version.TryParse(text, out var v) ? v : null;

    [GeneratedRegex("<Version>([^<]+)</Version>")]
    private static partial Regex VersionElement();
}
