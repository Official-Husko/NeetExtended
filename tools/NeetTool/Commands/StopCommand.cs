using NeetTool.Bridge;
using NeetTool.Cli;
using NeetTool.Game;

namespace NeetTool.Commands;

public sealed class StopCommand : ICommand
{
    public string Name => "stop";
    public string Usage => "stop [--force]";
    public string Summary => "Close the game (asks nicely through the bridge, kills it with --force)";

    public async Task<int> RunAsync(Args args)
    {
        if (!args.Has("force"))
        {
            try
            {
                await new BridgeClient(port: args.GetInt("port", BridgeClient.DefaultPort))
                    .CallAsync("game.quit", timeout: TimeSpan.FromSeconds(3));
                for (int i = 0; i < 20 && GameProcess.IsRunning(); i++)
                    await Task.Delay(500);
            }
            catch (Exception e) when (e is BridgeException or OperationCanceledException or IOException)
            {
                // Fall through to killing it.
            }
        }

        if (GameProcess.IsRunning())
            Out.Info($"Killed {GameProcess.Kill()} process(es)");
        Out.Ok("Game stopped.");
        return 0;
    }
}
