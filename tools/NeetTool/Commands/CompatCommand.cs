using NeetTool.Changelog;
using NeetTool.Cli;
using NeetTool.Compat;
using NeetTool.Game;

namespace NeetTool.Commands;

public sealed class CompatCommand : ICommand
{
    public string Name => "compat";
    public string Usage => "compat [--record] [--check] [--offline]";
    public string Summary => "Compare the installed game build with the one the mods were tested with";

    public async Task<int> RunAsync(Args args)
    {
        string repo = Git.RepoRoot(Directory.GetCurrentDirectory());
        var game = GameInstall.Locate(args.Get("game"));
        var installed = GameBuild.Read(game);

        if (args.Has("record"))
        {
            CompatRecord.FromBuild(installed, Releases.UpcomingVersion(repo)).Save(repo);
            Out.Ok($"Recorded build {installed.BuildId ?? "?"} as tested in {CompatRecord.RelativePath}");
            return 0;
        }

        var record = CompatRecord.Load(repo);
        string? latest = args.Has("offline") ? null : await SteamBuilds.LatestPublicAsync(Steam.AppId);

        Out.Info($"Installed:    build {installed.BuildId ?? "?"}, Unity {installed.UnityVersion ?? "?"}, game code {installed.CodeMvid}");
        Out.Info(record == null
            ? "Tested:       nothing recorded yet (neet compat --record)"
            : $"Tested:       build {record.BuildId ?? "?"} with {record.TestedWith} on {record.TestedOn}, game code {record.GameCode.Mvid}");
        if (!args.Has("offline"))
            Out.Info($"Latest:       {(latest == null ? "couldn't reach api.steamcmd.net" : "build " + latest)}");

        bool tested = record != null && installed.SameCode(record.ToBuild());
        if (tested)
            Out.Ok("The installed game is the tested build.");
        else
            Out.Warn("The installed game isn't the tested build. Test the mods, then run neet compat --record.");
        if (latest != null && record?.BuildId != null && latest != record.BuildId)
            Out.Warn($"Steam has a newer build ({latest}) than the tested one ({record.BuildId}).");

        return args.Has("check") && !tested ? 1 : 0;
    }
}
