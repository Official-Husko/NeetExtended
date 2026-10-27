using System.Text;
using NeetModKit.Console;
using NeetModKit.Game;
using PhoneExpansion.Calls;
using PhoneExpansion.Packs;
using UnityEngine;

namespace PhoneExpansion;

/// <summary>Console commands (backquote opens the console), handy for testing calls.</summary>
internal static class PhoneCommands
{
    public static void Register(PhoneExpansionMod mod)
    {
        ConsoleCommands.Add(mod, "phoneCalls", "Lists every modded call.", List);
        ConsoleCommands.Add<string>(mod, "phoneRing", "Rings a modded call by id.", Ring);
        ConsoleCommands.Add<string>(mod, "phoneDial", "Dials a number on the phone nearest to you.", Dial);
        ConsoleCommands.Add(mod, "phoneHangUp", "Ends the modded call in progress.", CallRouter.HangUpActive);
        ConsoleCommands.Add(mod, "phoneReload", "Loads every call file again.", PackLoader.Reload);
    }

    private static void List()
    {
        var sb = new StringBuilder();
        foreach (var call in PhoneBook.All)
        {
            sb.Append(call.Id);
            if (call.Number != null)
                sb.Append("  ").Append(call.DisplayNumber);
            if (call.Incoming != null)
                sb.Append("  rings: ").Append(call.Incoming.Trigger);
            sb.Append("  heard ").Append(PhoneMemory.TimesPlayed(call.Id)).Append('\n');
        }
        Debug.Log(sb.Length > 0 ? sb.ToString() : "No modded calls.");
    }

    private static void Ring(string id)
    {
        if (!PhoneRing.Ring(id))
            Debug.LogWarning($"No call '{id}'. Try phoneCalls.");
    }

    private static void Dial(string number)
    {
        var player = GameRefs.Player;
        var phone = player != null ? PhoneLine.Nearest(player.transform.position) : null;
        if (phone == null)
        {
            Debug.LogWarning("No phone here.");
            return;
        }
        string digits = PhoneBook.Digits(number);
        CallerScreen.Forget(phone);
        phone.screenText.text = digits.Length > 3 ? digits.Substring(0, 3) + "-" + digits.Substring(3) : digits;
        phone.Call();
    }
}
