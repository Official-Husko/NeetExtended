using NeetTool.Cli;
using NeetTool.Game;

namespace NeetTool.Commands;

public sealed class SteamCommand : ICommand
{
    private const string ModFolder = "CoopSteam";

    public string Name => "steam";
    public string Usage => "steam [--game DIR] [--remove]";
    public string Summary => "Fetch steam_api.dll for the Co-op over Steam mod (--remove deletes it again)";

    public async Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        string modDir = Path.Combine(game.ModsDir, ModFolder);
        string target = Path.Combine(modDir, SteamApiSource.FileName);

        if (!Directory.Exists(modDir) || !File.Exists(Path.Combine(modDir, "mod.json")))
            throw new InvalidOperationException($"The {ModFolder} mod isn't installed in {game.ModsDir}. Run the installer first.");
        if (GameProcess.IsRunning() && File.Exists(target))
            throw new InvalidOperationException("Close the game first (it has steam_api.dll loaded).");

        if (args.Has("remove"))
        {
            if (File.Exists(target))
                File.Delete(target);
            Out.Ok("Removed steam_api.dll. Co-op over Steam is switched off until you fetch it again.");
            return 0;
        }

        string cached = await SteamApiSource.GetAsync();
        File.Copy(cached, target, overwrite: true);
        Out.Ok($"Installed steam_api.dll into {modDir}");
        Out.Info("  It's Valve's file (Steamworks SDK), kept out of the mod pack on purpose. Start Steam, then the game from Steam.");
        return 0;
    }
}
