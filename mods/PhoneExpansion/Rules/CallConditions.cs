using System;
using NeetModKit.Rules;
using Newtonsoft.Json.Linq;
using PhoneExpansion.Calls;

namespace PhoneExpansion.Rules;

/// <summary>
/// Conditions only the phone knows about. They join the shared ones (money, flag, day, ...), so
/// emails and other content can check them too.
/// </summary>
internal static class CallConditions
{
    public static void Register(PhoneExpansionMod mod)
    {
        RuleConditions.Register(mod, "dialed", Dialed,
            "How many times this call's number connected before (the game's own call included).");
        RuleConditions.Register(mod, "called", Called,
            "A call (by id) was played: \"pizza\" or { \"pizza\": \">= 2\" }. Short ids mean a call from the same mod.");
    }

    private static Func<IRuleSubject, bool> Dialed(JToken value)
    {
        var comparison = Comparison.Parse(value);
        return subject => comparison.Test(PhoneMemory.TimesDialed((subject as PhoneCall)?.Number));
    }

    private static Func<IRuleSubject, bool> Called(JToken value)
    {
        if (value.Type == JTokenType.String)
        {
            string id = value.Value<string>();
            return subject => PhoneMemory.TimesPlayed(Resolve(id, subject)) > 0;
        }
        var tests = NamedComparisons.Parse(value);
        return subject => tests.All(id => PhoneMemory.TimesPlayed(Resolve(id, subject)));
    }

    /// <summary>Short ids ("pizza") mean a call from the same mod as whatever is being checked.</summary>
    private static string Resolve(string id, IRuleSubject subject)
    {
        string mod = RuleContext.ModOf(subject);
        return id.IndexOf('/') >= 0 || mod == null ? id : PhoneBook.Find(id, mod)?.Id ?? id;
    }
}
