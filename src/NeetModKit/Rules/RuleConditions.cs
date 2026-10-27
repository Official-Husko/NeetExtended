using System;
using System.Collections.Generic;
using NeetLoader.Runtime;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Rules;

/// <summary>
/// The keys a <see cref="Condition"/> can use ("money", "night", "flag", ...), shared by every mod
/// that reads conditions from files (phone calls, emails). Mods can add their own; a condition
/// turns its value from the file into a test once, when the file loads, so mistakes show up right
/// away:
/// <code>
/// RuleConditions.Register(this, "raining", value => { bool want = value.Value&lt;bool&gt;(); return _ => Weather.Raining == want; },
///     "true while it rains");
/// </code>
/// </summary>
public static class RuleConditions
{
    public sealed class Info
    {
        public string Name;
        public string Help;
        public string Owner;
        internal Func<JToken, Func<IRuleSubject, bool>> Compile;
    }

    private static readonly Dictionary<string, Info> Known = new Dictionary<string, Info>(StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<Info> All => Known.Values;

    public static void Register(ITracker owner, string name, Func<JToken, Func<IRuleSubject, bool>> compile, string help)
    {
        if (Known.ContainsKey(name))
            throw new ArgumentException($"A condition called '{name}' already exists.");
        var info = new Info { Name = name, Help = help, Owner = (owner as NeetLoader.NeetMod)?.Info.Id, Compile = compile };
        Known[name] = info;
        owner.Track(Disposable.Create(() =>
        {
            if (Known.TryGetValue(name, out var current) && current == info)
            {
                Known.Remove(name);
                RuleRegistry.Raise();
            }
        }));
        RuleRegistry.Raise();
    }

    internal static Func<IRuleSubject, bool> Compile(string name, JToken value)
    {
        if (!Known.TryGetValue(name, out var info))
            throw new FormatException($"Unknown condition '{name}'. Known: {string.Join(", ", Known.Keys)}, any, all, not.");
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

    /// <summary>Runs a condition by name with a single value, for scripts: Check("money", "&gt;= 20").</summary>
    public static bool Check(string name, JToken value, IRuleSubject subject) => Compile(name, value)(subject);
}
