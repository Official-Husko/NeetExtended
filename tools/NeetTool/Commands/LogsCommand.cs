using NeetTool.Cli;
using NeetTool.Game;

namespace NeetTool.Commands;

public sealed class LogsCommand : ICommand
{
    public string Name => "logs";
    public string Usage => "logs [--player | --errors] [--lines N]";
    public string Summary => "Print the end of the loader log, Unity's Player.log (--player) or the error report (--errors)";

    public Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        string? path = args.Has("player") ? Path.Combine(game.PersistentDataDir() ?? "", "Player.log")
            : args.Has("errors") ? Path.Combine(Path.GetDirectoryName(game.LoaderLog) ?? "", "errors.log")
            : game.LoaderLog;

        if (path is null || !File.Exists(path))
        {
            if (args.Has("errors"))
            {
                Console.WriteLine("No errors.log: nothing has gone wrong this session.");
                return Task.FromResult(0);
            }
            Out.Error($"No log at {path}");
            return Task.FromResult(1);
        }

        // Share read/write: the game keeps the file open.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var lines = reader.ReadToEnd().Split('\n');
        int count = args.GetInt("lines", args.Has("errors") ? int.MaxValue : 60);
        foreach (string line in lines.Skip(Math.Max(0, lines.Length - count)))
            Console.WriteLine(line);
        return Task.FromResult(0);
    }
}
