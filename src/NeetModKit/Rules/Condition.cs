using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Rules;

/// <summary>
/// A test like "the player has at least $20 and it's night 2 or later". In content files it's an
/// object where every key must hold: <c>{ "money": 20, "night": "&gt;= 2" }</c>. The keys are the
/// registered <see cref="RuleConditions"/>, plus "any" (a list where one must hold), "all" and
/// "not" (an object that must not hold).
/// </summary>
public sealed class Condition
{
    private readonly Func<IRuleSubject, bool> _test;

    private Condition(Func<IRuleSubject, bool> test)
    {
        _test = test;
    }

    /// <summary>A condition written in code. <paramref name="test"/> gets the subject it's checked for (may be null).</summary>
    public static Condition Of(Func<IRuleSubject, bool> test) => new Condition(test ?? throw new ArgumentNullException(nameof(test)));

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

    /// <summary>True if the condition holds for <paramref name="subject"/>. Errors count as false.</summary>
    public bool Test(IRuleSubject subject)
    {
        try
        {
            return _test(subject);
        }
        catch (Exception e)
        {
            KitMod.Instance?.Log.Warn("A condition failed: " + e.Message);
            return false;
        }
    }

    private static Func<IRuleSubject, bool> ParseAll(JObject obj)
    {
        var tests = new List<Func<IRuleSubject, bool>>();
        foreach (var property in obj.Properties())
            tests.Add(ParseKey(property.Name, property.Value));

        return subject =>
        {
            for (int i = 0; i < tests.Count; i++)
            {
                if (!tests[i](subject))
                    return false;
            }
            return true;
        };
    }

    private static Func<IRuleSubject, bool> ParseKey(string key, JToken value)
    {
        switch (key)
        {
            case "any":
                return ParseList(value as JArray ?? throw new FormatException("\"any\" needs a list of conditions."), any: true);
            case "all":
                return ParseList(value as JArray ?? throw new FormatException("\"all\" needs a list of conditions."), any: false);
            case "not":
                var inner = Parse(value);
                return subject => !inner._test(subject);
            default:
                return RuleConditions.Compile(key, value);
        }
    }

    private static Func<IRuleSubject, bool> ParseList(JArray list, bool any)
    {
        var tests = new List<Func<IRuleSubject, bool>>();
        foreach (var item in list)
            tests.Add(Parse(item)._test);

        return subject =>
        {
            foreach (var test in tests)
            {
                if (test(subject) == any)
                    return any;
            }
            return !any;
        };
    }
}
