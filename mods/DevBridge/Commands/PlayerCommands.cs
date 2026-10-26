using System;
using System.Linq;
using DevBridge.Server;
using NeetModKit.Game;
using NeetModKit.Values;
using Newtonsoft.Json.Linq;

namespace DevBridge.Commands;

internal static class PlayerCommands
{
    [BridgeCommand("player.get", "Position, room, needs, held items and money.")]
    private static object Get(JObject p)
    {
        var player = GameRefs.Player ?? throw new InvalidOperationException("No player right now (not in the house?).");
        var abilities = GameRefs.PlayerAbilities;
        return new JObject
        {
            ["position"] = JsonValues.ToJson(player.transform.position),
            ["room"] = Rooms.At(player.transform.position).ToString(),
            ["stats"] = JObject.FromObject(PlayerControl.ReadStats()),
            ["godMode"] = PlayerControl.GodMode,
            ["held"] = abilities != null
                ? new JArray(abilities.pickups.Select(h => h != null ? (JToken)h.name : JValue.CreateNull()).ToArray())
                : new JArray(),
            ["money"] = Money.Get(),
        };
    }

    [BridgeCommand("player.set", "Set needs/stats by name.", Params = new[] { "stats: {\"Hunger\": 100, ...}" })]
    private static object Set(JObject p)
    {
        var stats = p["stats"] as JObject ?? p; // Also accept the stats as top-level keys.
        foreach (var pair in stats)
            PlayerControl.SetStat(pair.Key, pair.Value.Value<float>());
        return JObject.FromObject(PlayerControl.ReadStats());
    }

    [BridgeCommand("player.teleport", "Move the player to a room or a point.", Params = new[] { "room?: Location name", "x, y, z or pos?" })]
    private static object Teleport(JObject p)
    {
        if (p["room"] != null)
            PlayerControl.Teleport(Rooms.Parse(p.Str("room")));
        else
            PlayerControl.Teleport(p.Position() ?? throw new ArgumentException("Give a room or x/y/z."));
        return JsonValues.ToJson(GameRefs.Player.transform.position);
    }

    [BridgeCommand("player.god", "No death, needs frozen.", Params = new[] { "enabled: bool" })]
    private static object God(JObject p)
    {
        PlayerControl.SetGodMode(p.Bool("enabled"));
        return PlayerControl.GodMode;
    }

    [BridgeCommand("money.get", "Current money.")]
    private static object MoneyGet(JObject p) => Money.Get();

    [BridgeCommand("money.set", "Set money (untaxed unless taxed=true).", Params = new[] { "value", "taxed?: bool" })]
    private static object MoneySet(JObject p)
    {
        if (p.Bool("taxed", false))
            Money.Set(p.Float("value"));
        else
            Money.SetUntaxed(p.Float("value"));
        return Money.Get();
    }
}
