using System.Linq;
using DevBridge.Server;
using NeetModKit.Game;
using NeetModKit.Values;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DevBridge.Commands;

internal static class WorldCommands
{
    [BridgeCommand("items.list", "Every spawnable item name.", Params = new[] { "filter?: substring" })]
    private static object Items(JObject p) => Filter(Spawning.ItemNames, p.Str("filter", null));

    [BridgeCommand("items.spawn", "Spawn an item near the player, in a room or at a point.",
        Params = new[] { "name", "room?", "x, y, z or pos?", "count?: default 1" })]
    private static object SpawnItem(JObject p)
    {
        string name = p.Str("name");
        int count = Mathf.Clamp(p.Int("count", 1), 1, 50);
        var spawned = new JArray();
        for (int i = 0; i < count; i++)
        {
            GameObject go;
            if (p["room"] != null)
                go = Spawning.ItemInRoom(name, Rooms.Parse(p.Str("room")));
            else if (p.Position() is Vector3 pos)
                go = Spawning.Item(name, pos);
            else
                go = Spawning.ItemNearPlayer(name);
            spawned.Add(JsonValues.Reference(go));
        }
        return spawned;
    }

    [BridgeCommand("npcs.list", "Every spawnable NPC name.", Params = new[] { "filter?: substring" })]
    private static object Npcs(JObject p) => Filter(Spawning.NpcNames, p.Str("filter", null));

    [BridgeCommand("npcs.spawn", "Spawn an NPC in a room or at a point.", Params = new[] { "name", "room?", "x, y, z or pos?" })]
    private static object SpawnNpc(JObject p)
    {
        string name = p.Str("name");
        var go = p["room"] != null
            ? Spawning.NpcInRoom(name, Rooms.Parse(p.Str("room")))
            : Spawning.Npc(name, p.Position() ?? Spawning.InFrontOfPlayer(1.5f));
        return JsonValues.Reference(go);
    }

    [BridgeCommand("fire.start", "Set a room on fire.", Params = new[] { "room" })]
    private static object FireStart(JObject p)
    {
        HouseSystems.StartFire(Rooms.Parse(p.Str("room")));
        return "burning";
    }

    [BridgeCommand("fire.stop", "Put out a room's fire.", Params = new[] { "room" })]
    private static object FireStop(JObject p)
    {
        HouseSystems.StopFire(Rooms.Parse(p.Str("room")));
        return "out";
    }

    [BridgeCommand("power.set", "Force power on/off for a room or the whole house.", Params = new[] { "on: bool", "room?: omit for the whole house" })]
    private static object Power(JObject p)
    {
        bool on = p.Bool("on");
        if (p["room"] != null)
            HouseSystems.SetPower(Rooms.Parse(p.Str("room")), on);
        else
            HouseSystems.SetHousePower(on);
        return on;
    }

    private static JArray Filter(System.Collections.Generic.IEnumerable<string> names, string filter) =>
        new JArray(names.Where(n => filter == null || n.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0).ToArray());
}
