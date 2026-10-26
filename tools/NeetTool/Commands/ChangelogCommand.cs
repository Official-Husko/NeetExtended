using NeetTool.Changelog;
using NeetTool.Cli;

namespace NeetTool.Commands;

public sealed class ChangelogCommand : ICommand
{
    public string Name => "changelog";
    public string Usage => "changelog [--write] [--out FILE]";
    public string Summary => "Build changelog.json from Changelog-* commit trailers";

    public Task<int> RunAsync(Args args)
    {
        string repo = Git.RepoRoot(Directory.GetCurrentDirectory());
        string json = JsonOut.Format(ChangelogBuilder.Build(repo)) + "\n";

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
