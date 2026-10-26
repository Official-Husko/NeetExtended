using NeetTool.Cli;
using NeetTool.Game;

namespace NeetTool.Commands;

public sealed class LogsCommand : ICommand
{
    public string Name => "logs";
    public string Usage => "logs [--player] [--lines N]";
    public string Summary => "Print the end of the loader log (or Unity's Player.log with --player)";

    public Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        string? path = args.Has("player")
            ? Path.Combine(game.PersistentDataDir() ?? "", "Player.log")
            : game.LoaderLog;

        if (path is null || !File.Exists(path))
        {
            Out.Error($"No log at {path}");
            return Task.FromResult(1);
        }

        // Share read/write: the game keeps the file open.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var lines = reader.ReadToEnd().Split('\n');
        int count = args.GetInt("lines", 60);
        foreach (string line in lines.Skip(Math.Max(0, lines.Length - count)))
            Console.WriteLine(line);
        return Task.FromResult(0);
    }
}
