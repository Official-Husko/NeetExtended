using NeetTool.Cli;
using NeetTool.Game;

namespace NeetTool.Commands;

public sealed class UninstallCommand : ICommand
{
    public string Name => "uninstall";
    public string Usage => "uninstall [--game DIR] [--remove-mods]";
    public string Summary => "Remove the loader (mods stay unless --remove-mods)";

    public Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        foreach (string step in new LoaderInstaller(game).Uninstall(args.Has("remove-mods")))
            Out.Info("  " + step);
        Out.Ok("Game is back to vanilla.");
        return Task.FromResult(0);
    }
}
