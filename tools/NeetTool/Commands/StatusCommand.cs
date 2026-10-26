using NeetTool.Bridge;
using NeetTool.Cli;
using NeetTool.Game;

namespace NeetTool.Commands;

public sealed class StatusCommand : ICommand
{
    public string Name => "status";
    public string Usage => "status [--game DIR]";
    public string Summary => "Show install state, installed mods and whether the game is up";

    public async Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        var installer = new LoaderInstaller(game);

        Out.Info($"Game:        {game.Dir}");
        Out.Info($"Loader DLLs: {(installer.FilesPresent ? "present" : "missing")}");
        Out.Info($"Registered:  {(installer.ManifestsPatched ? "yes" : "no")}");
        Out.Info($"Proton tool: {Steam.CompatToolName() ?? "(Steam default)"}");
        Out.Info($"Save folder: {game.PersistentDataDir() ?? "(unknown)"}");

        if (Directory.Exists(game.ModsDir))
        {
            var mods = Directory.GetDirectories(game.ModsDir)
                .Where(d => File.Exists(Path.Combine(d, "mod.json")))
                .Select(Path.GetFileName);
            Out.Info($"Mods:        {string.Join(", ", mods)}");
        }

        Out.Info($"Running:     {(GameProcess.IsRunning() ? "yes" : "no")}");
        Out.Info($"Bridge:      {(await new BridgeClient(port: args.GetInt("port", BridgeClient.DefaultPort)).IsUpAsync() ? "up" : "down")}");
        return 0;
    }
}
