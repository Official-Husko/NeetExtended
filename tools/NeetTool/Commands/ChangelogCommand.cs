using NeetTool.Changelog;
using NeetTool.Cli;

namespace NeetTool.Commands;

public sealed class ChangelogCommand : ICommand
{
    public string Name => "changelog";
    public string Usage => "changelog [--write] [--out FILE] [--release X.Y.Z] [--notes X.Y.Z] [--suggest]";
    public string Summary => "Build changelog.json from Changelog-* commit trailers, release notes and version advice";

    public Task<int> RunAsync(Args args)
    {
        string repo = Git.RepoRoot(Directory.GetCurrentDirectory());

        if (args.Has("suggest"))
        {
            var advice = BumpAdvisor.Suggest(repo);
            Console.WriteLine($"Since {(advice.From == null ? "the start" : "v" + advice.From)}: {advice.Commits} commits, " +
                              $"{advice.Added} added, {advice.Changed} changed, {advice.Fixed} fixed, {advice.Removed} removed, " +
                              $"{advice.Breaking} breaking");
            Console.WriteLine($"Suggested: {advice.Suggested} ({advice.Reason})");
            return Task.FromResult(0);
        }

        // --release treats the commits since the last tag as that version, released today.
        var changelog = ChangelogBuilder.Build(repo, args.Get("release"));

        if (args.Get("notes") is { } version)
        {
            Console.Write(ReleaseNotes.Markdown(changelog, version));
            return Task.FromResult(0);
        }

        string json = JsonOut.Format(changelog) + "\n";
        if (!args.Has("write"))
        {
            Console.Write(json);
            return Task.FromResult(0);
        }

        string path = args.Get("out") ?? Path.Combine(repo, "changelog.json");
        File.WriteAllText(path, json);
        Out.Ok($"Wrote {path}");
        return Task.FromResult(0);
    }
}
