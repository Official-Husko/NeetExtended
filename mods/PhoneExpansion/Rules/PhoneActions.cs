using System;
using System.Collections.Generic;
using System.Globalization;
using NeetLoader.Runtime;
using Newtonsoft.Json.Linq;
using PhoneExpansion.Calls;

namespace PhoneExpansion.Rules;

/// <summary>
/// Things a call can make happen. In call files: <c>{ "do": "deliver", "item": "Pizza" }</c>; in Ink:
/// <c>~ PhoneAct("deliver", "item=Pizza; after=30")</c> or, using the main value,
/// <c>~ PhoneAct("deliver", "Pizza")</c>. Mods register their own:
/// <code>
/// PhoneActions.Register(this, "confetti", args => Confetti.Burst(args.Num("amount", 10)), "amount", "Throws confetti.");
/// </code>
/// </summary>
public static class PhoneActions
{
    public sealed class Info
    {
        public string Name;
        public string MainValue;
        public string Help;
        internal Action<ActionArgs> Run;
    }

    private static readonly Dictionary<string, Info> Known = new Dictionary<string, Info>(StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<Info> All => Known.Values;

    /// <param name="mainValue">The value a bare argument fills, e.g. "item" for deliver.</param>
    public static void Register(ITracker owner, string name, Action<ActionArgs> run, string mainValue, string help)
    {
        if (Known.ContainsKey(name))
            throw new ArgumentException($"A phone action called '{name}' already exists.");
        var info = new Info { Name = name, MainValue = mainValue, Help = help, Run = run };
        Known[name] = info;
        owner.Track(Disposable.Create(() =>
        {
            if (Known.TryGetValue(name, out var current) && current == info)
                Known.Remove(name);
        }));
    }

    public static bool Exists(string name) => name != null && Known.ContainsKey(name);

    /// <summary>Runs an action now. Problems are logged; they never stop the call.</summary>
    public static void Run(string name, JObject values, PhoneCall call)
    {
        if (!Known.TryGetValue(name ?? "", out var info))
        {
            PhoneExpansionMod.Instance?.Log.Warn($"Unknown phone action '{name}'.");
            return;
        }
        try
        {
            info.Run(new ActionArgs(values, call));
        }
        catch (Exception e)
        {
            PhoneExpansionMod.Instance?.Log.Warn($"Phone action '{name}' failed: {e.Message}");
        }
    }

    /// <summary>
    /// Turns the text form used from Ink into values: "key=value; key2=value2", or a single value
    /// for the action's main value.
    /// </summary>
    internal static JObject ParseText(string name, string text)
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

    private static JToken Literal(string text)
    {
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
            return number;
        if (bool.TryParse(text, out bool flag))
            return flag;
        return text;
    }
}
