using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using NeetLoader.Runtime;

namespace NeetModKit.Rules;

/// <summary>
/// Values content files can put in their text: <c>"Call back in {{hitmanWait}} minutes."</c>,
/// <c>"Whack {{player:1}}"</c>. A value is a name, optionally with an argument after a colon (letters,
/// digits, <c>_ . -</c>), and is read when the line is shown. Mods register their own:
/// <code>
/// RuleTexts.Register(this, "weather", _ => Weather.Name, "today's weather");
/// </code>
/// Hand-written Ink uses <c>{RuleText("weather", "")}</c>.
/// </summary>
public static class RuleTexts
{
    public sealed class Info
    {
        public string Name;
        public string Help;
        public string Owner;
        internal Func<string, string> Read;
    }

    private static readonly Regex Placeholder = new Regex(@"\{\{([A-Za-z][A-Za-z0-9_]*)(?::([A-Za-z0-9_.\-]*))?\}\}");
    private static readonly Dictionary<string, Info> Known = new Dictionary<string, Info>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Failed = new HashSet<string>();

    public static IEnumerable<Info> All => Known.Values;

    /// <param name="read">Gets the argument ("" without one) and returns the text to show.</param>
    public static void Register(ITracker owner, string name, Func<string, string> read, string help)
    {
        if (Known.ContainsKey(name))
            throw new ArgumentException($"A text value called '{name}' already exists.");
        var info = new Info { Name = name, Help = help, Owner = (owner as NeetLoader.NeetMod)?.Info.Id, Read = read };
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

    public static bool Exists(string name) => name != null && Known.ContainsKey(name);

    /// <summary>A value's text now; problems are logged once and show as "?".</summary>
    public static string Read(string name, string arg)
    {
        if (!Known.TryGetValue(name ?? "", out var info))
            return "?";
        try
        {
            return info.Read(arg ?? "") ?? "";
        }
        catch (Exception e)
        {
            if (Failed.Add(name + ":" + arg))
                KitMod.Instance?.Log.Warn($"The text value {name} failed: {e.Message}");
            return "?";
        }
    }

    public static bool HasValues(string text) => text != null && Placeholder.IsMatch(text);

    /// <summary>
    /// Plain text as Ink (see <see cref="InkText.Escape"/>), with each <c>{{name}}</c> or
    /// <c>{{name:arg}}</c> as a call that reads it. Unknown names throw a FormatException.
    /// </summary>
    public static string ToInk(string text)
    {
        if (!HasValues(text))
            return InkText.Escape(text ?? "");
        var sb = new StringBuilder(text.Length + 32);
        int at = 0;
        foreach (Match m in Placeholder.Matches(text))
        {
            string name = m.Groups[1].Value;
            if (!Known.ContainsKey(name))
                throw new FormatException($"There's no text value called '{name}'. Known: {string.Join(", ", Known.Keys)}");
            sb.Append(EscapeKeepingSpaces(text.Substring(at, m.Index - at)));
            sb.Append("{RuleText(\"").Append(name).Append("\", \"").Append(m.Groups[2].Value).Append("\")}");
            at = m.Index + m.Length;
        }
        sb.Append(EscapeKeepingSpaces(text.Substring(at)));
        return sb.ToString().Trim();
    }

    // InkText.Escape trims, which would eat the spaces around a value.
    private static string EscapeKeepingSpaces(string part)
    {
        if (part.Length == 0)
            return part;
        string escaped = InkText.Escape(part);
        if (char.IsWhiteSpace(part[0]))
            escaped = " " + escaped;
        if (part.Length > 1 && char.IsWhiteSpace(part[part.Length - 1]))
            escaped += " ";
        return escaped;
    }
}
