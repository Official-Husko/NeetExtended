using System;
using System.Collections.Generic;
using NeetModKit.Game;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Rules;

/// <summary>The condition keys every content file can use.</summary>
internal static class BuiltinConditions
{
    public static void Register(NeetLoader.NeetMod kit)
    {
        RuleConditions.Register(kit, "money", Compare(_ => Money.Get()),
            "Cash on hand: 20 (at least 20) or a comparison like \"< 5\".");
        RuleConditions.Register(kit, "night", Compare(_ => EncounterSpawner.nightId),
            "The game's night counter (EncounterSpawner.nightId). The game never moves it on; use \"day\".");
        RuleConditions.Register(kit, "day", Compare(_ => ModFlags.Day),
            "Which day it is: 1 at the start, one more each time the player sleeps and wakes up.");
        RuleConditions.Register(kit, "minutes", Compare(_ => ModFlags.PlaySeconds / 60f),
            "Minutes of play in this save.");
        RuleConditions.Register(kit, "times", Compare(subject => subject?.Times ?? 0),
            "How many times this happened before (a call played, an email sent).");
        RuleConditions.Register(kit, "chance", Chance,
            "A random chance from 0 to 1: 0.25 holds a quarter of the time.");
        RuleConditions.Register(kit, "stat", Stat,
            "Player stats: { \"Hunger\": \"< 25\" } or \"Hunger < 25\". 0 is empty, 100 is full.");
        RuleConditions.Register(kit, "holding", Holding,
            "The player holds this item (or one of a list) in either hand.");
        RuleConditions.Register(kit, "room", InRoom,
            "The player is in this room (or one of a list).");
        RuleConditions.Register(kit, "flag", Flag,
            "A flag is set (\"name\") or compares: { \"name\": \">= 2\" }.");
    }

    private static Func<JToken, Func<IRuleSubject, bool>> Compare(Func<IRuleSubject, float> read) => value =>
    {
        var comparison = Comparison.Parse(value);
        return subject => comparison.Test(read(subject));
    };

    private static Func<IRuleSubject, bool> Chance(JToken value)
    {
        float chance = value.Value<float>();
        return _ => UnityEngine.Random.value < chance;
    }

    private static Func<IRuleSubject, bool> Stat(JToken value)
    {
        NamedComparisons tests;
        if (value is JObject)
        {
            tests = NamedComparisons.Parse(value);
        }
        else
        {
            // "Hunger < 25": the name, then the comparison.
            string text = value.Value<string>().Trim();
            int split = text.IndexOfAny(new[] { ' ', '<', '>', '=', '!' });
            if (split <= 0)
                throw new FormatException($"'{text}' should look like \"Hunger < 25\".");
            tests = new NamedComparisons();
            tests.Add(text.Substring(0, split), Comparison.Parse(text.Substring(split)));
        }
        return _ => tests.All(name => PlayerControl.FindStat(name).GetVal());
    }

    private static Func<IRuleSubject, bool> Holding(JToken value)
    {
        var names = Names(value);
        return _ =>
        {
            var abilities = GameRefs.PlayerAbilities;
            if (abilities == null)
                return false;
            foreach (var held in abilities.pickups)
            {
                if (held != null && names.Contains(CleanName(held.name)))
                    return true;
            }
            return false;
        };
    }

    private static Func<IRuleSubject, bool> InRoom(JToken value)
    {
        var rooms = new HashSet<Location>();
        foreach (string name in Names(value))
            rooms.Add(Rooms.Parse(name));
        return _ => GameRefs.Player != null && rooms.Contains(Rooms.At(GameRefs.Player.transform.position));
    }

    private static Func<IRuleSubject, bool> Flag(JToken value)
    {
        if (value.Type == JTokenType.String)
        {
            string name = value.Value<string>();
            return _ => ModFlags.Get(name) != 0;
        }
        var tests = NamedComparisons.Parse(value);
        return _ => tests.All(name => ModFlags.Get(name));
    }

    private static HashSet<string> Names(JToken value)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (value is JArray list)
        {
            foreach (var item in list)
                names.Add(item.Value<string>());
        }
        else
        {
            names.Add(value.Value<string>());
        }
        return names;
    }

    private static string CleanName(string name)
    {
        int clone = name.IndexOf("(Clone)", StringComparison.Ordinal);
        return (clone >= 0 ? name.Substring(0, clone) : name).Trim();
    }
}
