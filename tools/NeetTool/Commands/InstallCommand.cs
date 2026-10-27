using NeetTool.Cli;
using NeetTool.Game;

namespace NeetTool.Commands;

public sealed class InstallCommand : ICommand
{
    public string Name => "install";
    public string Usage => "install [--mods DIR] [--32bit|--64bit] [--incremental-gc|--no-incremental-gc]";
    public string Summary => "Hook the mod loader into the game and set up the 64-bit player (--32bit keeps the original)";

    public async Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        if (args.Has("64bit") && args.Has("32bit"))
            throw new ArgumentException("Pick one of --64bit and --32bit.");
        bool pickedPlayer = args.Has("64bit") || args.Has("32bit");
        bool boot = args.Has("incremental-gc") || args.Has("no-incremental-gc");
        bool running = GameProcess.IsRunning(game);
        if (running)
        {
            if (pickedPlayer || boot)
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

        if (args.Has("32bit"))
            Keep32Bit(game);
        else
            await SetUp64Bit(game, asked: args.Has("64bit"), running);

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

    // The choice is saved first: if the switch can't be done here, the next install still keeps 32-bit.
    private static void Keep32Bit(GameInstall game)
    {
        new PlayerChoice(game).Keep32Bit();
        foreach (string step in new PlayerSwap(game).To32Bit())
            Out.Info("  " + step);
        Out.Info("  Installing again keeps the 32-bit player; --64bit switches to 64-bit");
    }

    /// <summary>
    /// The 64-bit player is the default. A plain install leaves a game set to 32-bit alone, and if the
    /// download or the swap fails it carries on with the player the game has: the mods run on both.
    /// </summary>
    private static async Task SetUp64Bit(GameInstall game, bool asked, bool running)
    {
        var choice = new PlayerChoice(game);
        var player = new PlayerSwap(game);
        if (!asked && choice.Keeps32Bit)
        {
            Out.Info("  Kept the 32-bit player, as picked before (--64bit switches)");
            return;
        }
        if (player.Current == PlayerArch.X64)
        {
            choice.Clear();
            Out.Info("  The game already runs the 64-bit player");
            return;
        }
        if (running)
        {
            Out.Warn("The game keeps its 32-bit player for now. Install again with the game closed to switch to 64-bit.");
            return;
        }

        try
        {
            string playerDir = await UnityPlayerSource.GetAsync();
            foreach (string step in player.To64Bit(playerDir))
                Out.Info("  " + step);
            choice.Clear();
        }
        catch (Exception e) when (!asked && IsSetupFailure(e))
        {
            Out.Warn($"Couldn't set up the 64-bit player: {e.Message}");
            Out.Warn("The game keeps its 32-bit player for now and the mods work on it. Install again to retry, or pass --32bit to stay on it.");
        }
    }

    private static bool IsSetupFailure(Exception e) =>
        e is HttpRequestException or IOException or InvalidOperationException or InvalidDataException
            or UnauthorizedAccessException or OperationCanceledException;
}
