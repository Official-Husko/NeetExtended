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
        RuleTexts.Register(kit, "weather", ReadText,
            "The weather as a word players read: drizzle, rain or storm. {{weather:next}} is the forecast's word (the current one without a forecast).");
    }

    private static Func<IRuleSubject, bool> Compile(JToken value)
    {
        var kinds = new HashSet<WeatherKind>();
        foreach (string name in Names(value))
        {
            if (!WeatherState.TryParse(name, out var kind))
                throw new FormatException($"'{name}' isn't a kind of weather. Use drizzle, rain or storm.");
            kinds.Add(kind);
        }
        return _ => kinds.Contains(Sky.Weather.Kind);
    }

    private static string ReadText(string arg)
    {
        var weather = Sky.Weather;
        return arg.Equals("next", StringComparison.OrdinalIgnoreCase) ? weather.NextName : weather.Name;
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
