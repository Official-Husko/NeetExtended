using System;
using System.Linq;
using DevBridge.Server;
using NeetLoader.Logging;
using NeetModKit.Console;
using Newtonsoft.Json.Linq;

namespace DevBridge.Commands;

internal static class LogCommands
{
    [BridgeCommand("log.tail", "Recent log lines from the game, mods and Unity.",
        Params = new[] { "after?: only lines after this sequence number", "max?: default 100", "level?: Debug, Info, Warning, Error" })]
    private static object Tail(JObject p)
    {
        var level = Enum.TryParse(p.Str("level", "Debug"), true, out LogLevel l) ? l : LogLevel.Debug;
        var lines = LogHub.Buffer.Read(p["after"]?.Value<long>() ?? -1, p.Int("max", 100), level);
        return new JObject
        {
            ["last"] = LogHub.Buffer.LastSequence,
            ["lines"] = new JArray(lines.Select(e => $"{e.Sequence} [{e.Level}] [{e.Source}] {e.Message}").ToArray()),
        };
    }

    [BridgeCommand("console.exec", "Run an in-game console command and return what it printed.", Params = new[] { "command: e.g. 'get Mug'" })]
    private static object Exec(JObject p)
    {
        long before = LogHub.Buffer.LastSequence;
        ConsoleCommands.Execute(p.Str("command"));
        var output = LogHub.Buffer.Read(before, 200).Select(e => e.Message).ToArray();
        return new JArray(output);
    }

    [BridgeCommand("console.list", "Every console command signature.")]
    private static object List(JObject p) => new JArray(ConsoleCommands.List().ToArray());
}
