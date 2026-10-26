using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using PhoneExpansion.Calls;

namespace PhoneExpansion.Rules;

/// <summary>
/// A test like "the player has at least $20 and it's night 2 or later". In call files it's an
/// object where every key must hold: <c>{ "money": 20, "night": "&gt;= 2" }</c>. The keys are
/// the registered <see cref="PhoneConditions"/>, plus "any" (a list where one must hold) and
/// "not" (an object that must not hold).
/// </summary>
public sealed class Condition
{
    private readonly Func<PhoneCall, bool> _test;

    private Condition(Func<PhoneCall, bool> test)
    {
        _test = test;
    }

    /// <summary>A condition written in code. <paramref name="test"/> gets the call it's checked for (may be null).</summary>
    public static Condition Of(Func<PhoneCall, bool> test) => new Condition(test ?? throw new ArgumentNullException(nameof(test)));

    /// <summary>Parses the JSON form. Throws <see cref="FormatException"/> with a readable message on mistakes.</summary>
    public static Condition Parse(JToken json)
    {
        switch (json)
        {
            case null:
                throw new FormatException("A condition is missing.");
            case JObject obj:
                return new Condition(ParseAll(obj));
            case JArray list:
                return new Condition(ParseList(list, any: false));
            default:
                throw new FormatException($"A condition must be an object like {{ \"money\": 20 }}, not '{json}'.");
        }
    }

    /// <summary>True if the condition holds for <paramref name="call"/>. Errors count as false.</summary>
    public bool Test(PhoneCall call)
    {
        try
        {
            return _test(call);
        }
        catch (Exception e)
        {
            PhoneExpansionMod.Instance?.Log.Warn("A phone condition failed: " + e.Message);
            return false;
        }
    }

    private static Func<PhoneCall, bool> ParseAll(JObject obj)
    {
        var tests = new List<Func<PhoneCall, bool>>();
        foreach (var property in obj.Properties())
            tests.Add(ParseKey(property.Name, property.Value));

        return call =>
        {
            for (int i = 0; i < tests.Count; i++)
            {
                if (!tests[i](call))
                    return false;
            }
            return true;
        };
    }

    private static Func<PhoneCall, bool> ParseKey(string key, JToken value)
    {
        switch (key)
        {
            case "any":
                return ParseList(value as JArray ?? throw new FormatException("\"any\" needs a list of conditions."), any: true);
            case "all":
                return ParseList(value as JArray ?? throw new FormatException("\"all\" needs a list of conditions."), any: false);
            case "not":
                var inner = Parse(value);
                return call => !inner._test(call);
            default:
                return PhoneConditions.Compile(key, value);
        }
    }

    private static Func<PhoneCall, bool> ParseList(JArray list, bool any)
    {
        var tests = new List<Func<PhoneCall, bool>>();
        foreach (var item in list)
            tests.Add(Parse(item)._test);

        return call =>
        {
            foreach (var test in tests)
            {
                if (test(call) == any)
                    return any;
            }
            return !any;
        };
    }
}
