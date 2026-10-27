using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Rules;

/// <summary>
/// Several named numbers that all have to pass their <see cref="Comparison"/>, written in content
/// files as <c>{ "visits": "&gt;= 2", "Hunger": "&lt; 25" }</c>. What a name means is up to the reader:
/// a flag, a stat, how often a call was played.
/// </summary>
public sealed class NamedComparisons
{
    private readonly List<KeyValuePair<string, Comparison>> _tests = new List<KeyValuePair<string, Comparison>>();

    public static NamedComparisons Parse(JToken value)
    {
        if (!(value is JObject obj))
            throw new FormatException($"expected {{ \"name\": comparison, ... }}, not '{value}'.");
        var result = new NamedComparisons();
        foreach (var property in obj.Properties())
            result.Add(property.Name, Comparison.Parse(property.Value));
        return result;
    }

    public void Add(string name, Comparison comparison) =>
        _tests.Add(new KeyValuePair<string, Comparison>(name, comparison));

    /// <summary>True when every name's value (from <paramref name="read"/>) passes its comparison.</summary>
    public bool All(Func<string, float> read)
    {
        foreach (var test in _tests)
        {
            if (!test.Value.Test(read(test.Key)))
                return false;
        }
        return true;
    }
}
