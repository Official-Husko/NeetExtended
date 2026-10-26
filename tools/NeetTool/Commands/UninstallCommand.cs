using NeetTool.Cli;
using NeetTool.Game;

namespace NeetTool.Commands;

public sealed class UninstallCommand : ICommand
{
    public string Name => "uninstall";
    public string Usage => "uninstall [--game DIR] [--remove-mods]";
    public string Summary => "Remove the loader and undo player and boot changes (mods stay unless --remove-mods)";

    public Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        if (GameProcess.IsRunning())
            throw new InvalidOperationException("Close the game first.");

        // The 32-bit backup lives in the loader's folder, so put it back before anything is deleted.
        var player = new PlayerSwap(game);
        if (player.Current == PlayerArch.X64)
        {
            if (player.HasBackup)
            {
                foreach (string step in player.To32Bit())
                    Out.Info("  " + step);
            }
            else
            {
                Out.Warn("The game runs a 64-bit player without a backup of the original. Use Steam's \"Verify integrity of game files\".");
            }
        }

        new BootConfig(game).Restore();
        Out.Info("  Restored boot.config");

        foreach (string step in new LoaderInstaller(game).Uninstall(args.Has("remove-mods")))
            Out.Info("  " + step);
        Out.Ok("Game is back to vanilla.");
        return Task.FromResult(0);
    }
}
