using System.Text.Json.Nodes;
using NeetTool.Bridge;
using NeetTool.Game;

namespace NeetTool.Mcp;

/// <summary>Tools that act on this machine (process control, files) instead of inside the game.</summary>
public static class LocalTools
{
    public static IEnumerable<McpTool> All(BridgeClient bridge, Func<GameInstall> game, int? instance = null)
    {
        yield return new McpTool("game_status",
            "Is the game running, is the bridge reachable, is the loader installed.",
            Schema.Object(),
            async (_, ct) =>
            {
                var g = game();
                var installer = new LoaderInstaller(g);
                var status = new JsonObject
                {
                    ["gameDir"] = g.Dir,
                    ["running"] = GameProcess.IsRunning(),
                    ["bridge"] = await bridge.IsUpAsync(ct),
                    ["loaderInstalled"] = installer.FilesPresent && installer.ManifestsPatched,
                };
                return ToolResult.Text(BridgeClient.Pretty(status));
            });

        yield return new McpTool("game_launch",
            "Start the game windowed and wait until the bridge answers.",
            Schema.Object()
                .Bool("direct", "Run through Proton directly instead of Steam")
                .Int("waitSeconds", "How long to wait for the bridge (default 90)"),
            async (args, ct) =>
            {
                var g = game();
                if (!GameProcess.IsRunning(instance ?? 1))
                    GameProcess.Launch(g, instance ?? 1, args["direct"]?.GetValue<bool>() == true, GameProcess.WindowedArgs);

                int wait = args["waitSeconds"]?.GetValue<int>() ?? 90;
                var deadline = DateTime.UtcNow.AddSeconds(wait);
                while (DateTime.UtcNow < deadline)
                {
                    if (await bridge.IsUpAsync(ct))
                        return ToolResult.Text("Game is up and the bridge is answering.");
                    await Task.Delay(1000, ct);
                }
                return ToolResult.Error($"Bridge didn't answer within {wait}s. Check the log with game_logs.");
            });

        yield return new McpTool("game_stop",
            "Close the game. Asks it to quit first, kills it if that fails.",
            Schema.Object(),
            async (_, ct) =>
            {
                try { await bridge.CallAsync("game.quit", timeout: TimeSpan.FromSeconds(3), ct: ct); }
                catch (Exception e) when (e is BridgeException or OperationCanceledException or IOException) { }

                for (int i = 0; i < 20 && GameProcess.IsRunning(instance); i++)
                    await Task.Delay(500, ct);
                int killed = GameProcess.IsRunning(instance) ? GameProcess.Kill(instance) : 0;
                return ToolResult.Text(killed > 0 ? $"Killed {killed} process(es)." : "Game closed.");
            });

        yield return new McpTool("game_logs",
            "Tail of the loader log file, or Unity's Player.log. Works even when the game is closed.",
            Schema.Object()
                .Bool("player", "Read Player.log instead of the loader log")
                .Int("lines", "Number of lines (default 80)"),
            (args, _) =>
            {
                var g = game();
                string path = args["player"]?.GetValue<bool>() == true
                    ? g.PlayerLogFile(instance ?? 1)
                    : g.LoaderLogFile(instance ?? 1);
                if (!File.Exists(path))
                    return Task.FromResult(ToolResult.Error($"No log at {path}"));

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var lines = reader.ReadToEnd().Split('\n');
                int count = args["lines"]?.GetValue<int>() ?? 80;
                return Task.FromResult(ToolResult.Text(string.Join('\n', lines.Skip(Math.Max(0, lines.Length - count)))));
            });

        yield return new McpTool("screenshot",
            "Capture the game window and return it as an image.",
            Schema.Object().Num("scale", "Resize factor, 0.1 to 1 (default 0.5)"),
            async (args, ct) =>
            {
                var p = new JsonObject
                {
                    ["scale"] = args["scale"]?.GetValue<double>() ?? 0.5,
                    ["format"] = "jpg",
                };
                var result = await bridge.CallAsync("screenshot", p, ct: ct);
                return new ToolResult()
                    .AddImage(result!["data"]!.GetValue<string>(), result["mimeType"]!.GetValue<string>())
                    .AddText($"{result["width"]}x{result["height"]}, scene {result["scene"]}");
            });
    }
}
