using System;
using System.Linq;
using DevBridge.Server;
using NeetModKit.Game;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>The game's status effects (food, pills, diarrhea, ...): what exists, what the player has, adding one.</summary>
internal static class StatusCommands
{
    [BridgeCommand("status.effects", "Every status effect the game knows (HouseManager.statusEffectDict) with its type, duration and stats.")]
    private static object Effects(JObject p)
    {
        var house = GameRefs.House ?? throw new InvalidOperationException("Only in the house.");
        return new JArray(house.statusEffectDict.Select(pair => Describe(pair.Value, pair.Key)).ToArray());
    }

    [BridgeCommand("status.player", "The player's status effects right now, with their timers.")]
    private static object Player(JObject p)
    {
        var handler = Handler();
        return new JObject
        {
            ["active"] = new JArray(handler.effects.Select(e => Describe(e, e.name)).ToArray()),
            ["waitingToStart"] = handler.effectsDelayed?.Count ?? 0,
            ["queued"] = handler.effectsToAdd?.Count ?? 0,
        };
    }

    [BridgeCommand("status.add", "Give the player a status effect by name.", Params = new[] { "name" })]
    private static object Add(JObject p)
    {
        var house = GameRefs.House ?? throw new InvalidOperationException("Only in the house.");
        string name = p.Str("name");
        if (!house.statusEffectDict.TryGetValue(name, out var template))
            throw new ArgumentException($"No effect '{name}'. Known: {string.Join(", ", house.statusEffectDict.Keys)}");
        var added = Handler().AddEffect(template);
        return added != null ? Describe(added, added.name) : (JToken)"not added (doesn't stack, or the player lacks its stats)";
    }

    [BridgeCommand("status.clear", "End all of the player's status effects now.")]
    private static object Clear(JObject p)
    {
        var handler = Handler();
        int count = handler.effects.Count;
        for (int i = handler.effects.Count - 1; i >= 0; i--)
            handler.RemoveEffect(handler.effects[i]);
        return count;
    }

    private static StatusHandler Handler() =>
        (GameRefs.Player != null ? GameRefs.Player.GetComponent<StatusHandler>() : null)
        ?? throw new InvalidOperationException("No player.");

    private static JObject Describe(StatusEffect effect, string name) => new JObject
    {
        ["name"] = name,
        ["type"] = effect.statusType.ToString(),
        ["operation"] = effect.operationType.ToString(),
        ["stats"] = new JArray(effect.statType.Select(s => s.ToString()).ToArray()),
        ["size"] = effect.effectSize,
        ["duration"] = effect.duration,
        ["durationLeft"] = Math.Round(effect.durationTimer, 2),
        ["startDelay"] = effect.startDelay,
        ["tickEvery"] = effect.tickerFreq,
        ["cannotStack"] = effect.cannotStack,
        ["class"] = effect.GetType().Name,
    };
}
