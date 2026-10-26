using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using PhoneExpansion.Calls;

namespace PhoneExpansion.Rules;

/// <summary>What an action gets: its values from the call file and the call it runs in.</summary>
public sealed class ActionArgs
{
    internal ActionArgs(JObject values, PhoneCall call)
    {
        Values = values ?? new JObject();
        Call = call;
    }

    /// <summary>Everything written next to "do" in the call file.</summary>
    public JObject Values { get; }

    /// <summary>The call the action runs in (null if it runs outside a modded call).</summary>
    public PhoneCall Call { get; }

    public bool Has(string key) => Values[key] != null && Values[key].Type != JTokenType.Null;

    public string Str(string key, string fallback = null) => Has(key) ? Values[key].ToString() : fallback;

    public float Num(string key, float fallback = 0f)
    {
        if (!Has(key))
            return fallback;
        var token = Values[key];
        if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            return token.Value<float>();
        if (float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            return parsed;
        throw new FormatException($"\"{key}\" should be a number, not '{token}'.");
    }

    public int Int(string key, int fallback = 0) => (int)Math.Round(Num(key, fallback));

    public bool Bool(string key, bool fallback = false)
    {
        if (!Has(key))
            return fallback;
        var token = Values[key];
        if (token.Type == JTokenType.Boolean)
            return token.Value<bool>();
        string text = token.ToString().Trim().ToLowerInvariant();
        return text == "true" || text == "on" || text == "yes" || text == "1";
    }

    /// <summary>A value that has to be there.</summary>
    public string Require(string key) =>
        Str(key) ?? throw new FormatException($"\"{key}\" is missing.");
}
