using System;
using System.Collections.Generic;
using NeetModKit.Game;
using Newtonsoft.Json.Linq;
using PhoneExpansion.Calls;
using UnityEngine;

namespace PhoneExpansion.Rules;

/// <summary>The condition keys every call file can use.</summary>
internal static class BuiltinConditions
{
    public static void Register(PhoneExpansionMod mod)
    {
        PhoneConditions.Register(mod, "money", Compare(_ => Money.Get()),
            "Cash on hand: 20 (at least 20) or a comparison like \"< 5\".");
        PhoneConditions.Register(mod, "night", Compare(_ => EncounterSpawner.nightId),
            "Which night it is (1, 2, ...).");
        PhoneConditions.Register(mod, "minutes", Compare(_ => PhoneMemory.PlaySeconds / 60f),
            "Minutes of play in this save.");
        PhoneConditions.Register(mod, "times", Compare(call => PhoneMemory.TimesPlayed(call?.Id)),
            "How many times this call was played before.");
        PhoneConditions.Register(mod, "dialed", Compare(call => PhoneMemory.TimesDialed(call?.Number)),
            "How many times this call's number connected before (the game's own call included).");
        PhoneConditions.Register(mod, "chance", Chance,
            "A random chance from 0 to 1: 0.25 holds a quarter of the time.");
        PhoneConditions.Register(mod, "stat", Stat,
            "Player stats: { \"Hunger\": \"< 25\" } or \"Hunger < 25\". 0 is empty, 100 is full.");
        PhoneConditions.Register(mod, "holding", Holding,
            "The player holds this item (or one of a list) in either hand.");
        PhoneConditions.Register(mod, "room", InRoom,
            "The player is in this room (or one of a list).");
        PhoneConditions.Register(mod, "flag", Flag,
            "A flag is set (\"name\") or compares: { \"name\": \">= 2\" }.");
        PhoneConditions.Register(mod, "called", Called,
            "Another call (by id) was played: \"pizza\" or { \"pizza\": \">= 2\" }.");
    }

    private static Func<JToken, Func<PhoneCall, bool>> Compare(Func<PhoneCall, float> read) => value =>
    {
        var comparison = Comparison.Parse(value);
        return call => comparison.Test(read(call));
    };

    private static Func<PhoneCall, bool> Chance(JToken value)
    {
        float chance = value.Value<float>();
        return _ => UnityEngine.Random.value < chance;
    }

    private static Func<PhoneCall, bool> Stat(JToken value)
    {
        var tests = new List<KeyValuePair<string, Comparison>>();
        if (value is JObject obj)
        {
            foreach (var property in obj.Properties())
                tests.Add(new KeyValuePair<string, Comparison>(property.Name, Comparison.Parse(property.Value)));
        }
        else
        {
            // "Hunger < 25": the name, then the comparison.
            string text = value.Value<string>().Trim();
            int split = text.IndexOfAny(new[] { ' ', '<', '>', '=', '!' });
            if (split <= 0)
                throw new FormatException($"'{text}' should look like \"Hunger < 25\".");
            tests.Add(new KeyValuePair<string, Comparison>(text.Substring(0, split), Comparison.Parse(text.Substring(split))));
        }

        return _ =>
        {
            foreach (var test in tests)
            {
                if (!test.Value.Test(PlayerControl.FindStat(test.Key).GetVal()))
                    return false;
            }
            return true;
        };
    }

    private static Func<PhoneCall, bool> Holding(JToken value)
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

    private static Func<PhoneCall, bool> InRoom(JToken value)
    {
        var rooms = new HashSet<Location>();
        foreach (string name in Names(value))
            rooms.Add(Rooms.Parse(name));
        return _ => GameRefs.Player != null && rooms.Contains(Rooms.At(GameRefs.Player.transform.position));
    }

    private static Func<PhoneCall, bool> Flag(JToken value)
    {
        if (value.Type == JTokenType.String)
        {
            string name = value.Value<string>();
            return _ => PhoneMemory.Flag(name) != 0;
        }
        var tests = ByName(value);
        return _ => All(tests, name => PhoneMemory.Flag(name));
    }

    private static Func<PhoneCall, bool> Called(JToken value)
    {
        if (value.Type == JTokenType.String)
        {
            string id = value.Value<string>();
            return call => PhoneMemory.TimesPlayed(Resolve(id, call)) > 0;
        }
        var tests = ByName(value);
        return call => All(tests, id => PhoneMemory.TimesPlayed(Resolve(id, call)));
    }

    /// <summary>Short ids ("pizza") mean a call from the same mod as the call being checked.</summary>
    private static string Resolve(string id, PhoneCall call) =>
        id.IndexOf('/') >= 0 || call?.Id == null ? id : PhoneBook.Find(id, call.Id.Split('/')[0])?.Id ?? id;

    private static List<KeyValuePair<string, Comparison>> ByName(JToken value)
    {
        var tests = new List<KeyValuePair<string, Comparison>>();
        foreach (var property in ((JObject)value).Properties())
            tests.Add(new KeyValuePair<string, Comparison>(property.Name, Comparison.Parse(property.Value)));
        return tests;
    }

    private static bool All(List<KeyValuePair<string, Comparison>> tests, Func<string, float> read)
    {
        foreach (var test in tests)
        {
            if (!test.Value.Test(read(test.Key)))
                return false;
        }
        return true;
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
