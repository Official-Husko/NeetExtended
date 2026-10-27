using NeetTool.Changelog;
using NeetTool.Cli;
using NeetTool.Versions;

namespace NeetTool.Commands;

public sealed class VersionsCommand : ICommand
{
    public string Name => "versions";
    public string Usage => "versions [--check]";
    public string Summary => "Mod versions against the last release: which changed mods still need a new version";

    public Task<int> RunAsync(Args args)
    {
        string repo = Git.RepoRoot(Directory.GetCurrentDirectory());
        var last = Releases.LastTag(repo);
        var mods = ModVersions.Read(repo, last?.Tag);
        Out.Info(last == null ? "No release yet; every mod is new." : $"Compared with {last.Value.Tag}:");

        bool failed = false;
        foreach (var mod in mods)
        {
            string line = $"  {mod.Id,-24} {mod.Version,-10}";
            switch (mod.State)
            {
                case ModVersions.State.New:
                    Out.Info(line + "new since the last release");
                    break;
                case ModVersions.State.Bumped:
                    Out.Ok(line + $"was {mod.Released}, {mod.ChangedFiles} file(s) changed");
                    break;
                case ModVersions.State.NeedsBump:
                    Out.Warn(line + $"{mod.ChangedFiles} file(s) changed since {last?.Tag}, but still {mod.Version}: give it a new version (patch for fixes, minor for features)");
                    failed = true;
                    break;
                default:
                    Out.Info(line + "unchanged");
                    break;
            }
        }

        foreach (string problem in ModVersions.DependencyProblems(mods))
        {
            Out.Warn(problem);
            failed = true;
        }
        if (!failed)
            Out.Ok("Versions are in order.");
        return Task.FromResult(args.Has("check") && failed ? 1 : 0);
    }
}
