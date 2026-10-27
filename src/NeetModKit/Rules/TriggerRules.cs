using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Rules;

/// <summary>
/// When something from a content file happens by itself: a call ringing in, an email arriving.
/// Written as <c>true</c> or <c>{ "trigger": "random", "weight": 2, "when": {...}, "once": true,
/// "cooldown": 30, "delay": 20, "store": "BlackMarket" }</c>. Each kind of content says which
/// triggers it supports; <see cref="Triggers"/> decides what fires.
/// </summary>
public class TriggerRules
{
    /// <summary>Mixed in with the game's own random events (prank calls, spam).</summary>
    public const string Random = "random";

    /// <summary>After the player orders from a computer store.</summary>
    public const string Order = "order";

    /// <summary>After the house loads.</summary>
    public const string Arrive = "arrive";

    /// <summary>After the player sleeps and wakes up (a new day).</summary>
    public const string Wake = "wake";

    /// <summary>Only when something sets it off (an action, the mod menu, code).</summary>
    public const string Manual = "manual";

    public string Trigger = Random;

    /// <summary>How likely it's picked among the others that could fire (default 1).</summary>
    public float Weight = 1f;

    /// <summary>Only fires while this holds.</summary>
    public Condition When;

    /// <summary>Only ever fires once per save.</summary>
    public bool Once;

    /// <summary>Minutes of play before it can fire again.</summary>
    public float CooldownMinutes;

    /// <summary>For "order", "arrive" and "wake": seconds of play to wait.</summary>
    public float DelaySeconds = 20f;

    /// <summary>For "order": only after orders from this store (Normal, BlackMarket, OccultMarket). Null for any.</summary>
    public string Store;

    /// <summary>
    /// Reads the rules from a file. <paramref name="allowed"/> are the triggers this content knows;
    /// the first is the default. <paramref name="defaults"/> sets the other defaults (Once, DelaySeconds).
    /// Throws <see cref="FormatException"/> with what's wrong.
    /// </summary>
    public static T Parse<T>(JToken json, IReadOnlyList<string> allowed, T defaults) where T : TriggerRules, new()
    {
        var rules = new T
        {
            Trigger = allowed[0],
            Weight = defaults.Weight,
            Once = defaults.Once,
            CooldownMinutes = defaults.CooldownMinutes,
            DelaySeconds = defaults.DelaySeconds,
        };
        if (!(json is JObject obj))
            return rules; // true

        string trigger = obj.Value<string>("trigger");
        if (trigger != null)
        {
            rules.Trigger = trigger.ToLowerInvariant();
            if (!Contains(allowed, rules.Trigger))
                throw new FormatException($"trigger '{trigger}' isn't one of {string.Join(", ", allowed)}.");
        }
        rules.Weight = obj.Value<float?>("weight") ?? rules.Weight;
        rules.Once = obj.Value<bool?>("once") ?? rules.Once;
        rules.CooldownMinutes = obj.Value<float?>("cooldown") ?? rules.CooldownMinutes;
        rules.DelaySeconds = obj.Value<float?>("delay") ?? rules.DelaySeconds;
        rules.Store = obj.Value<string>("store");
        if (obj["when"] != null)
            rules.When = Condition.Parse(obj["when"]);
        return rules;
    }

    /// <summary>Whether a file's value turns triggering on: anything but missing, null or false.</summary>
    public static bool IsOn(JToken json) =>
        json != null && json.Type != JTokenType.Null && !(json.Type == JTokenType.Boolean && !json.Value<bool>());

    /// <summary>A short description for menus: "at random, once".</summary>
    public string Describe(string thing)
    {
        string text;
        switch (Trigger)
        {
            case Order: text = $"{thing} after " + (Store != null ? Store + " orders" : "store orders"); break;
            case Arrive: text = $"{thing} soon after you get home"; break;
            case Wake: text = $"{thing} when you wake up"; break;
            case Manual: text = $"{thing} when something else sets it up"; break;
            default: text = $"{thing} at random"; break;
        }
        return Once ? text + ", once" : text;
    }

    private static bool Contains(IReadOnlyList<string> list, string value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
                return true;
        }
        return false;
    }
}
