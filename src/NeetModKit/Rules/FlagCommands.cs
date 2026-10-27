using System.Text;
using NeetModKit.Console;
using UnityEngine;

namespace NeetModKit.Rules;

/// <summary>Console commands (backquote opens the console) for looking at and setting flags while testing.</summary>
internal static class FlagCommands
{
    public static void Register(NeetLoader.NeetMod kit)
    {
        ConsoleCommands.Add(kit, "flags", "Shows the flags calls, emails and other mods have set, and the day.", Show);
        ConsoleCommands.Add<string, int>(kit, "setFlag", "Sets a flag (0 clears it).", ModFlags.Set);
    }

    private static void Show()
    {
        var sb = new StringBuilder();
        sb.Append("Day ").Append(ModFlags.Day).Append(", ").Append((int)(ModFlags.PlaySeconds / 60f)).Append(" minutes played\n");
        foreach (var pair in ModFlags.All)
            sb.Append(pair.Key).Append(" = ").Append(pair.Value).Append('\n');
        if (ModFlags.All.Count == 0)
            sb.Append("No flags set.");
        Debug.Log(sb.ToString());
    }
}
