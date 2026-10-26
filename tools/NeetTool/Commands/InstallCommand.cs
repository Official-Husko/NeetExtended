using NeetTool.Cli;
using NeetTool.Game;

namespace NeetTool.Commands;

public sealed class InstallCommand : ICommand
{
    public string Name => "install";
    public string Usage => "install [--game DIR]";
    public string Summary => "Hook the mod loader into the game";

    public Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        if (GameProcess.IsRunning())
            Out.Warn("The game is running. Changes apply after a restart.");

        foreach (string step in new LoaderInstaller(game).Install())
            Out.Info("  " + step);
        Out.Ok($"Installed into {game.Dir}");
        return Task.FromResult(0);
    }
}
