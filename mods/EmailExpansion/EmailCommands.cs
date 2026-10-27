using System.Text;
using EmailExpansion.Mail;
using EmailExpansion.Packs;
using NeetModKit.Console;
using UnityEngine;

namespace EmailExpansion;

/// <summary>Console commands (backquote opens the console), handy for testing emails.</summary>
internal static class EmailCommands
{
    public static void Register(EmailExpansionMod mod)
    {
        ConsoleCommands.Add(mod, "emailList", "Lists every modded email.", List);
        ConsoleCommands.Add<string>(mod, "emailSend", "Sends a modded email by id.", Send);
        ConsoleCommands.Add(mod, "emailReload", "Loads every email file again.", MailPacks.Reload);
    }

    private static void List()
    {
        var sb = new StringBuilder();
        foreach (var mail in MailBook.All)
        {
            sb.Append(mail.Id).Append("  \"").Append(mail.Subject).Append('"');
            if (mail.Send != null)
                sb.Append("  arrives: ").Append(mail.Send.Trigger);
            sb.Append("  sent ").Append(MailMemory.TimesSent(mail.Id));
            if (MailMemory.WasRead(mail.Id))
                sb.Append(", read");
            sb.Append('\n');
        }
        Debug.Log(sb.Length > 0 ? sb.ToString() : "No modded emails.");
    }

    private static void Send(string id)
    {
        var mail = MailBook.Find(id);
        if (mail == null)
        {
            Debug.LogWarning($"No email '{id}'. Try emailList.");
            return;
        }
        Mailer.Send(mail);
    }
}
