using System;
using System.Collections.Generic;
using NeetModKit.Game;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Rules;

/// <summary>The weather as content files see it: the <c>weather</c> condition and the <c>{{weather}}</c> text value.</summary>
internal static class WeatherRules
{
    public static void Register(NeetLoader.NeetMod kit)
    {
        RuleConditions.Register(kit, "weather", Compile,
            "The weather now is this kind (or one of a list): \"storm\", [\"drizzle\", \"rain\"]. Kinds: drizzle, rain, storm.");
        RuleConditions.Register(kit, "forecast", CompileForecast,
            "Whether the weather has a forecast: true or false. Or what the forecast says follows: \"storm\", [\"drizzle\", \"rain\"] (false without a forecast).");
        RuleTexts.Register(kit, "weather", ReadText,
            "The weather as a word players read: drizzle, rain or storm. {{weather:next}} is the forecast's word (the current one without a forecast), " +
            "{{weather:in}} how long until it changes (\"20 seconds\", \"3 minutes\"; \"a while\" without a forecast).");
    }

    private static Func<IRuleSubject, bool> CompileForecast(JToken value)
    {
        if (value.Type == JTokenType.Boolean)
        {
            bool want = value.Value<bool>();
            return _ => Sky.Weather.HasForecast == want;
        }
        var kinds = Kinds(value);
        return _ =>
        {
            var next = Sky.Weather.NextKind;
            return next.HasValue && kinds.Contains(next.Value);
        };
    }

    private static HashSet<WeatherKind> Kinds(JToken value)
    {
        var kinds = new HashSet<WeatherKind>();
        foreach (string name in Names(value))
        {
            if (!WeatherState.TryParse(name, out var kind))
                throw new FormatException($"'{name}' isn't a kind of weather. Use drizzle, rain or storm.");
            kinds.Add(kind);
        }
        return kinds;
    }

    /// <summary>A rough time for people: "20 seconds", x.</summary>
    internal static string Roughly(float seconds)
    {
        if (seconds < 0f)
            return "a while";
        if (seconds < 45f)
            return seconds < 10f ? "a few seconds" : (int)(Math.Round(seconds / 5f) * 5f) + " seconds";
        if (seconds < 90f)
            return "a minute";
        return (int)Math.Round(seconds / 60f) + " minutes";
    }

    private static Func<IRuleSubject, bool> Compile(JToken value)
    {
        var kinds = Kinds(value);
        return _ => kinds.Contains(Sky.Weather.Kind);
    }

    private static string ReadText(string arg)
    {
        var weather = Sky.Weather;
        if (arg.Equals("next", StringComparison.OrdinalIgnoreCase))
            return weather.NextName;
        if (arg.Equals("in", StringComparison.OrdinalIgnoreCase))
            return Roughly(weather.SecondsUntilNext);
        return weather.Name;
    }

    private static IEnumerable<string> Names(JToken value)
    {
        if (value is JArray list)
        {
            foreach (var item in list)
                yield return item.Value<string>();
        }
        else
        {
            yield return value.Value<string>();
        }
    }
}
