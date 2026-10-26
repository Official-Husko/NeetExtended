using NeetTool.Cli;
using NeetTool.Game;

namespace NeetTool.Commands;

public sealed class InstallCommand : ICommand
{
    public string Name => "install";
    public string Usage => "install [--mods DIR] [--64bit|--32bit] [--incremental-gc|--no-incremental-gc]";
    public string Summary => "Hook the mod loader into the game, optionally with mods and a 64-bit player";

    public async Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        bool swap = args.Has("64bit") || args.Has("32bit");
        bool boot = args.Has("incremental-gc") || args.Has("no-incremental-gc");
        if (GameProcess.IsRunning())
        {
            if (swap || boot)
                throw new InvalidOperationException("Close the game first: its player files can't be changed while it runs.");
            Out.Warn("The game is running. Changes apply after a restart.");
        }

        foreach (string step in new LoaderInstaller(game).Install())
            Out.Info("  " + step);

        if (args.Get("mods") is { } mods)
        {
            foreach (string step in ModPackage.Install(game, mods))
                Out.Info("  " + step);
        }

        var player = new PlayerSwap(game);
        if (args.Has("64bit"))
        {
            string playerDir = player.Current == PlayerArch.X64 ? "" : await UnityPlayerSource.GetAsync();
            foreach (string step in player.To64Bit(playerDir))
                Out.Info("  " + step);
        }
        else if (args.Has("32bit"))
        {
            foreach (string step in player.To32Bit())
                Out.Info("  " + step);
        }

        var bootConfig = new BootConfig(game);
        if (args.Has("incremental-gc"))
        {
            bootConfig.EnableIncrementalGc();
            Out.Info("  Turned on incremental garbage collection (boot.config)");
        }
        else if (args.Has("no-incremental-gc"))
        {
            bootConfig.Restore();
            Out.Info("  Turned off incremental garbage collection");
        }

        Out.Ok($"Installed into {game.Dir}");
        return 0;
    }
}
