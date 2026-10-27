using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NeetLoader.Runtime;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Rules;

/// <summary>
/// Things content files can make happen, shared by every mod that reads them (phone calls,
/// emails). In files: <c>{ "do": "deliver", "item": "Pizza" }</c>; from Ink:
/// <c>~ Act("deliver", "item=Pizza; after=30")</c> or, using the main value, <c>~ Act("deliver", "Pizza")</c>.
/// Mods register their own:
/// <code>
/// RuleActions.Register(this, "confetti", args => Confetti.Burst(args.Num("amount", 10)), "amount", "Throws confetti.");
/// </code>
/// </summary>
public static class RuleActions
{
    public sealed class Info
    {
        public string Name;
        public string MainValue;
        public string Help;
        public string Owner;
        internal Action<ActionArgs> Run;
    }

    private static readonly Dictionary<string, Info> Known = new Dictionary<string, Info>(StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<Info> All => Known.Values;

    /// <param name="mainValue">The value a bare argument fills, e.g. "item" for deliver.</param>
    public static void Register(ITracker owner, string name, Action<ActionArgs> run, string mainValue, string help)
    {
        if (Known.ContainsKey(name))
            throw new ArgumentException($"An action called '{name}' already exists.");
        var info = new Info { Name = name, MainValue = mainValue, Help = help, Owner = (owner as NeetLoader.NeetMod)?.Info.Id, Run = run };
        Known[name] = info;
        owner.Track(Disposable.Create(() =>
        {
            if (Known.TryGetValue(name, out var current) && current == info)
                Known.Remove(name);
        }));
    }

    public static bool Exists(string name) => name != null && Known.ContainsKey(name);

    /// <summary>Runs an action now. Problems are logged; they never stop what's playing.</summary>
    public static void Run(string name, JObject values, IRuleSubject subject)
    {
        if (!Known.TryGetValue(name ?? "", out var info))
        {
            KitMod.Instance?.Log.Warn($"Unknown action '{name}'.");
            return;
        }
        try
        {
            info.Run(new ActionArgs(values, subject));
        }
        catch (Exception e)
        {
            KitMod.Instance?.Log.Warn($"Action '{name}'{(subject != null ? " in " + subject.Id : "")} failed: {e.Message}");
        }
    }

    /// <summary>Runs <paramref name="action"/> after <paramref name="seconds"/> of game time (now if 0), on <paramref name="owner"/>'s coroutines.</summary>
    public static void Later(NeetLoader.NeetMod owner, float seconds, Action action)
    {
        if (seconds <= 0f)
            action();
        else
            owner.StartCoroutine(After(seconds, action));
    }

    private static IEnumerator After(float seconds, Action action)
    {
        yield return new UnityEngine.WaitForSeconds(seconds);
        try { action(); }
        catch (Exception e) { KitMod.Instance?.Log.Warn("A delayed action failed: " + e.Message); }
    }

    /// <summary>
    /// Turns the text form used from Ink into values: "key=value; key2=value2", or a single value
    /// for the action's main value.
    /// </summary>
    public static JObject ParseText(string name, string text)
    {
        var values = new JObject();
        if (string.IsNullOrWhiteSpace(text))
            return values;

        if (text.IndexOf('=') < 0)
        {
            string main = Known.TryGetValue(name ?? "", out var info) ? info.MainValue : null;
            values[main ?? "value"] = Literal(text.Trim());
            return values;
        }

        foreach (string part in text.Split(';'))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0)
                continue;
            values[part.Substring(0, eq).Trim()] = Literal(part.Substring(eq + 1).Trim());
        }
        return values;
    }

    /// <summary>A number stays a number (so Check("money", "20") means at least 20); anything else is text.</summary>
    public static JToken Literal(string text)
    {
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
            return number;
        if (bool.TryParse(text, out bool flag))
            return flag;
        return text;
    }
}
