using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Rules;

/// <summary>
/// A number test written in content files: "&lt; 25", "&gt;= 2", "= 0", "!= 3". A bare number means
/// "at least": <c>"money": 20</c> is the same as <c>"money": "&gt;= 20"</c>.
/// </summary>
public readonly struct Comparison
{
    private static readonly Regex Pattern = new Regex(@"^\s*(<=|>=|==|!=|<|>|=)?\s*(-?\d+(?:\.\d+)?)\s*$");

    private readonly string _op;
    private readonly float _value;

    private Comparison(string op, float value)
    {
        _op = op;
        _value = value;
    }

    public static Comparison Parse(JToken token)
    {
        if (token == null)
            throw new FormatException("A comparison is missing.");
        if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            return new Comparison(">=", token.Value<float>());
        return Parse(token.Value<string>());
    }

    public static Comparison Parse(string text)
    {
        var match = Pattern.Match(text ?? "");
        if (!match.Success)
            throw new FormatException($"'{text}' isn't a comparison. Write it like \"< 25\", \">= 2\" or \"= 0\".");
        string op = match.Groups[1].Success ? match.Groups[1].Value : "=";
        return new Comparison(op, float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    public bool Test(float value)
    {
        switch (_op)
        {
            case "<": return value < _value;
            case "<=": return value <= _value;
            case ">": return value > _value;
            case ">=": return value >= _value;
            case "!=": return Math.Abs(value - _value) > 0.0001f;
            default: return Math.Abs(value - _value) <= 0.0001f;
        }
    }

    public override string ToString() => _op + " " + _value.ToString(CultureInfo.InvariantCulture);
}
