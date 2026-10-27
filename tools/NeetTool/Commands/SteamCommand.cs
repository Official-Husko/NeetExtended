using NeetTool.Cli;
using NeetTool.Game;

namespace NeetTool.Commands;

public sealed class SteamCommand : ICommand
{
    private const string ModFolder = "CoopSteam";

    public string Name => "steam";
    public string Usage => "steam [--game DIR] [--remove]";
    public string Summary => "Fetch steam_api.dll and steam_api64.dll for the Co-op over Steam mod (--remove deletes them again)";

    public async Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        string modDir = Path.Combine(game.ModsDir, ModFolder);
        var targets = SteamApiSource.Files.Select(f => Path.Combine(modDir, f.Name)).ToList();

        if (!Directory.Exists(modDir) || !File.Exists(Path.Combine(modDir, "mod.json")))
            throw new InvalidOperationException($"The {ModFolder} mod isn't installed in {game.ModsDir}. Run the installer first.");
        if (GameProcess.IsRunning(game) && targets.Any(File.Exists))
            throw new InvalidOperationException("Close the game first (it may have Steam's DLL loaded).");

        if (args.Has("remove"))
        {
            foreach (string target in targets.Where(File.Exists))
                File.Delete(target);
            Out.Ok("Removed Steam's DLLs. Co-op over Steam is switched off until you fetch them again.");
            return 0;
        }

        string cacheDir = await SteamApiSource.GetAsync();
        foreach (var file in SteamApiSource.Files)
            File.Copy(Path.Combine(cacheDir, file.Name), Path.Combine(modDir, file.Name), overwrite: true);
        Out.Ok($"Installed {string.Join(" and ", SteamApiSource.Files.Select(f => f.Name))} into {modDir}");
        Out.Info("  They're Valve's files (Steamworks SDK), kept out of the mod pack on purpose: the 32-bit game uses the first, the 64-bit game the second. Start Steam, then the game from Steam.");
        return 0;
    }
}
