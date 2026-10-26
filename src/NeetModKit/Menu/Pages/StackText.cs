using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NeetModKit.Menu.Pages;

/// <summary>
/// Makes error details readable on screen: drops the IL offsets and assembly hashes Mono adds,
/// names Harmony's patched copies after the method they patch, and colours lines so the game's and
/// mods' own frames stand out from Unity and .NET internals. The copied report keeps the raw text.
/// </summary>
internal static class StackText
{
    private static readonly Regex Offsets = new Regex(@"\s*\[0x[0-9a-f]+\] in <[0-9a-f]+>:\d+|\s*\(at <[0-9a-f]+>:\d+\)|\s*in <[0-9a-f]+>:\d+");
    private static readonly Regex PatchedCopy = new Regex(@"\(wrapper dynamic-method\) |MonoMod\.Utils\.DynamicMethodDefinition\.|_Patch\d+(?=\s*\()");
    private static readonly Regex Arguments = new Regex(@"\s*\(([^()]*)\)\s*$");

    public static string Format(string details, Color exceptionColor)
    {
        if (string.IsNullOrEmpty(details))
            return "";
        var sb = new StringBuilder();
        foreach (string raw in details.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0)
                continue;
            if (sb.Length > 0)
                sb.Append('\n');

            if (!line.StartsWith("at ") && !line.StartsWith("(wrapper"))
            {
                // Exception names, messages and the lines mods add ("Night 1, player in Kitchen.").
                sb.Append(MenuPalette.Paint(line, line.Contains("Exception") ? exceptionColor : MenuPalette.Text));
                continue;
            }
            sb.Append("    ").Append(Frame(line));
        }
        return sb.ToString();
    }

    private static string Frame(string line)
    {
        bool patched = line.Contains("DynamicMethodDefinition");
        line = Offsets.Replace(line, "");
        line = PatchedCopy.Replace(line, "");
        line = Arguments.Replace(line, "()");
        if (line.StartsWith("at "))
            line = line.Substring(3);
        if (patched)
            line += " (patched)";

        bool engine = line.StartsWith("UnityEngine.") || line.StartsWith("System.") || line.StartsWith("Mono") ||
                      line.StartsWith("HarmonyLib.") || line.StartsWith("(wrapper");
        return MenuPalette.Paint(line, engine ? MenuPalette.Muted : MenuPalette.Text);
    }
}
