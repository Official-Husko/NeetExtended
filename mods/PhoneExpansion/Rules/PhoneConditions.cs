using System;
using System.Collections.Generic;
using NeetLoader.Runtime;
using Newtonsoft.Json.Linq;
using PhoneExpansion.Calls;

namespace PhoneExpansion.Rules;

/// <summary>
/// The keys a <see cref="Condition"/> can use ("money", "night", "holding", ...). Mods can add
/// their own; a condition gets its value from the call file and turns it into a test once, when
/// the file loads, so mistakes are reported right away:
/// <code>
/// PhoneConditions.Register(this, "raining", value => { bool want = value.Value&lt;bool&gt;(); return call => Weather.Raining == want; },
///     "true while it rains");
/// </code>
/// </summary>
public static class PhoneConditions
{
    public sealed class Info
    {
        public string Name;
        public string Help;
        internal Func<JToken, Func<PhoneCall, bool>> Compile;
    }

    private static readonly Dictionary<string, Info> Known = new Dictionary<string, Info>(StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<Info> All => Known.Values;

    public static void Register(ITracker owner, string name, Func<JToken, Func<PhoneCall, bool>> compile, string help)
    {
        if (Known.ContainsKey(name))
            throw new ArgumentException($"A phone condition called '{name}' already exists.");
        var info = new Info { Name = name, Help = help, Compile = compile };
        Known[name] = info;
        owner.Track(Disposable.Create(() =>
        {
            if (Known.TryGetValue(name, out var current) && current == info)
                Known.Remove(name);
        }));
    }

    internal static Func<PhoneCall, bool> Compile(string name, JToken value)
    {
        if (!Known.TryGetValue(name, out var info))
            throw new FormatException($"Unknown condition '{name}'. Known: {string.Join(", ", Known.Keys)}, any, not.");
        try
        {
            return info.Compile(value);
        }
        catch (FormatException e)
        {
            throw new FormatException($"\"{name}\": {e.Message}");
        }
        catch (Exception e) when (e is InvalidCastException || e is ArgumentException)
        {
            throw new FormatException($"\"{name}\" doesn't take '{value}'.");
        }
    }

    /// <summary>Runs a condition by name with a single value, for scripts: PhoneCheck("money", "&gt;= 20").</summary>
    internal static bool Check(string name, JToken value, PhoneCall call) => Compile(name, value)(call);
}
