using System.Text.Json.Nodes;
using NeetTool.Bridge;

namespace NeetTool.Mcp;

/// <summary>
/// Tools that forward straight to a DevBridge method. Each entry is one line: tool name,
/// bridge method, description and input schema. Anything not listed is still reachable
/// through bridge_call.
/// </summary>
public static class BridgeTools
{
    public static IEnumerable<McpTool> All(BridgeClient bridge)
    {
        McpTool Forward(string name, string method, string description, Schema schema) =>
            new(name, description, schema, async (args, ct) =>
                ToolResult.Text(BridgeClient.Pretty(await bridge.CallAsync(method, args, ct: ct))));

        yield return new McpTool("bridge_call",
            "Run any DevBridge method in the game. Use bridge_help to list them.",
            Schema.Object()
                .Str("method", "Bridge method name, e.g. player.get", required: true)
                .Obj("params", "Parameters object for the method"),
            async (args, ct) =>
            {
                string method = args["method"]!.GetValue<string>();
                var p = args["params"]?.AsObject().DeepClone().AsObject() ?? new JsonObject();
                return ToolResult.Text(BridgeClient.Pretty(await bridge.CallAsync(method, p, ct: ct)));
            });

        yield return Forward("bridge_help", "help",
            "List every DevBridge method with its parameters.", Schema.Object());

        yield return Forward("game_info", "game.info",
            "Current scene, loaded mods, fps, time scale and whether the house is ready.", Schema.Object());

        yield return Forward("game_save", "game.save", "Save the game now (same as the autosave).", Schema.Object());

        yield return Forward("game_timescale", "game.timescale",
            "Set Time.timeScale (0 pauses, 1 is normal).",
            Schema.Object().Num("value", "New time scale", required: true));

        yield return Forward("scene_load", "scene.load",
            "Load a scene by name: MainMenu, Tutorial, NEET_House, DreamZone, Nightmare1.",
            Schema.Object().Str("name", "Scene name", required: true));

        yield return Forward("console_exec", "console.exec",
            "Run an in-game console command (IngameDebugConsole) and return what it logged.",
            Schema.Object().Str("command", "Command line, e.g. 'get Mug'", required: true));

        yield return Forward("log_tail", "log.tail",
            "Recent log lines from the game, mods and Unity. Pass 'after' (a sequence number) to get only new lines.",
            Schema.Object()
                .Int("after", "Only return lines after this sequence number")
                .Int("max", "Maximum lines (default 100)")
                .Str("level", "Minimum level: Debug, Info, Warning, Error"));

        yield return Forward("player_get", "player.get",
            "Player position, room, every need/stat, held items and money.", Schema.Object());

        yield return Forward("player_set", "player.set",
            "Set player stats. Pass stat names as keys, e.g. {\"hunger\":100,\"energy\":50}.",
            Schema.Object().Any("stats", "Object of stat name -> value", required: true));

        yield return Forward("player_teleport", "player.teleport",
            "Move the player to a room (by Location name) or to exact coordinates.",
            Schema.Object().Str("room", "Location name, e.g. Kitchen").Num("x", "X").Num("y", "Y").Num("z", "Z"));

        yield return Forward("player_god", "player.god",
            "Toggle god mode: no damage, needs frozen.",
            Schema.Object().Bool("enabled", "On or off", required: true));

        yield return Forward("money_set", "money.set",
            "Set computer money. Bypasses the 12% tax unless taxed=true.",
            Schema.Object().Num("value", "Money", required: true).Bool("taxed", "Go through the normal setter"));

        yield return Forward("items_list", "items.list",
            "Every spawnable item name (PickupBank). Optional substring filter.",
            Schema.Object().Str("filter", "Case-insensitive substring"));

        yield return Forward("item_spawn", "items.spawn",
            "Spawn an item in front of the player or in a room.",
            Schema.Object()
                .Str("name", "Item prefab name from items_list", required: true)
                .Str("room", "Optional Location name")
                .Int("count", "How many (default 1)"));

        yield return Forward("rooms_list", "rooms.list", "Every Location with its centre point.", Schema.Object());

        yield return Forward("scene_find", "obj.find",
            "Find GameObjects by name substring and/or component type.",
            Schema.Object()
                .Str("name", "Name substring")
                .Str("component", "Component type name, e.g. Door")
                .Bool("includeInactive", "Include inactive objects")
                .Int("max", "Maximum results (default 50)"));

        yield return Forward("scene_tree", "obj.tree",
            "Hierarchy under an object (or the scene roots).",
            Schema.Object().Str("target", "Path or #instanceId").Int("depth", "Levels to expand (default 2)"));

        yield return Forward("object_inspect", "obj.inspect",
            "Fields and properties of a component on an object.",
            Schema.Object()
                .Str("target", "Path or #instanceId", required: true)
                .Str("component", "Component type name (omit to list components)"));

        yield return Forward("object_set", "obj.set",
            "Set a field or property on a component.",
            Schema.Object()
                .Str("target", "Path or #instanceId", required: true)
                .Str("component", "Component type name", required: true)
                .Str("member", "Field or property name", required: true)
                .Any("value", "New value (numbers, strings, bools, [x,y,z] vectors)", required: true));

        yield return Forward("object_call", "obj.call",
            "Call a method on a component.",
            Schema.Object()
                .Str("target", "Path or #instanceId", required: true)
                .Str("component", "Component type name", required: true)
                .Str("method", "Method name", required: true)
                .Arr("args", "Arguments"));

        yield return Forward("static_call", "type.call",
            "Call a static method on any loaded type, e.g. Locator.GetRoomCenter.",
            Schema.Object()
                .Str("type", "Type name", required: true)
                .Str("method", "Method name", required: true)
                .Arr("args", "Arguments"));

        yield return Forward("cards_get", "cards.get",
            "Anime card game state: crystals, unlocked count and every card.", Schema.Object());
    }
}
