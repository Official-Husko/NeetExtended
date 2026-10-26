using NeetTool.Bridge;
using NeetTool.Cli;
using NeetTool.Game;

namespace NeetTool.Commands;

public sealed class LaunchCommand : ICommand
{
    public string Name => "launch";
    public string Usage => "launch [--fullscreen] [--direct] [--wait SECONDS]";
    public string Summary => "Start the game (windowed by default); --direct skips the Steam client";

    public async Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        if (GameProcess.IsRunning())
        {
            Out.Warn("The game is already running.");
            return 0;
        }

        var gameArgs = args.Has("fullscreen") ? Array.Empty<string>() : GameProcess.WindowedArgs;
        if (args.Has("direct"))
        {
            string tool = Steam.CompatToolName()
                ?? throw new InvalidOperationException("No Proton build is set for the game in Steam.");
            string proton = Steam.ProtonScript(tool)
                ?? throw new InvalidOperationException($"Couldn't find {tool} on disk.");
            GameProcess.LaunchViaProton(game, proton, gameArgs);
            Out.Info($"Started through {tool}");
        }
        else
        {
            GameProcess.LaunchViaSteam(gameArgs);
            Out.Info("Asked Steam to start the game");
        }

        int wait = args.GetInt("wait", 0);
        if (wait <= 0)
            return 0;

        var bridge = new BridgeClient(port: args.GetInt("port", BridgeClient.DefaultPort));
        var deadline = DateTime.UtcNow.AddSeconds(wait);
        while (DateTime.UtcNow < deadline)
        {
            if (await bridge.IsUpAsync())
            {
                Out.Ok("Bridge is up.");
                return 0;
            }
            await Task.Delay(1000);
        }
        Out.Error($"Bridge didn't come up within {wait}s. Check {game.LoaderLog}");
        return 1;
    }
}
