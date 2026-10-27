namespace NeetTool.Changelog;

/// <summary>
/// Suggests the next version from what changed since the last release: breaking changes bump the
/// major version (the minor one before 1.0), new or removed things the minor, changes and fixes the
/// patch. A suggestion only; whoever releases decides.
/// </summary>
public static class BumpAdvisor
{
    public sealed record Advice(string? From, string Suggested, string Reason, int Commits,
        int Added, int Changed, int Fixed, int Removed, int Breaking);

    public static Advice Suggest(string repo)
    {
        var last = Releases.LastTag(repo);
        var commits = CommitLog.Read(repo, last == null ? "HEAD" : $"{last.Value.Tag}..HEAD");
        int Count(ChangeKind kind) => commits.Sum(c => c.Lines.Count(l => l.Kind == kind));
        int added = Count(ChangeKind.Added), changed = Count(ChangeKind.Changed),
            fixedCount = Count(ChangeKind.Fixed), removed = Count(ChangeKind.Removed);
        int breaking = commits.Count(c => c.Breaking);

        if (last == null)
            return new Advice(null, Releases.UpcomingVersion(repo), "first release (the version in Directory.Build.props)",
                commits.Count, added, changed, fixedCount, removed, breaking);

        var v = last.Value.Version;
        (Version next, string reason) = breaking > 0
            ? v.Major == 0
                ? (new Version(0, v.Minor + 1, 0), "breaking changes (before 1.0 they bump the minor version)")
                : (new Version(v.Major + 1, 0, 0), "breaking changes")
            : added > 0 || removed > 0
                ? (new Version(v.Major, v.Minor + 1, 0), "things were added or removed")
                : changed > 0 || fixedCount > 0
                    ? (new Version(v.Major, v.Minor, v.Build + 1), "only changes and fixes")
                    : (v, "nothing for players since the last release; no release needed");

        return new Advice(last.Value.Tag[1..], next.ToString(3), reason, commits.Count, added, changed, fixedCount, removed, breaking);
    }
}
