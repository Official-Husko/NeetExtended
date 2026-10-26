using System;
using System.Globalization;

namespace NeetModKit.Conversations;

/// <summary>Reads the arguments Ink passes to a function (ints, floats, strings or bools) forgivingly.</summary>
public static class InkValues
{
    public static string Str(object[] args, int index)
    {
        if (index >= args.Length || args[index] == null)
            return "";
        return args[index] is float f
            ? f.ToString(CultureInfo.InvariantCulture)
            : Convert.ToString(args[index], CultureInfo.InvariantCulture);
    }

    public static float Num(object[] args, int index)
    {
        if (index >= args.Length)
            return 0f;
        switch (args[index])
        {
            case int i: return i;
            case float f: return f;
            case bool b: return b ? 1f : 0f;
            case string s when float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed): return parsed;
            default: return 0f;
        }
    }

    public static int Int(object[] args, int index) => (int)Math.Round(Num(args, index));

    public static bool Bool(object[] args, int index) => Num(args, index) != 0f;
}
