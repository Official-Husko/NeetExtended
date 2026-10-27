using System.Collections.Generic;
using EmailExpansion.Mail;
using EmailExpansion.Packs;
using NeetModKit.Events;
using NeetModKit.Menu;
using NeetModKit.Ui.Toolkit;
using UnityEngine.UI;

namespace EmailExpansion.Menu;

/// <summary>Mod menu tab listing every modded email, with a button to send each one.</summary>
internal sealed class EmailPage : MenuPage
{
    private Text _status;

    public override string Title => "Email";
    public override int Order => 31;

    public override void Build(UiBuilder ui)
    {
        ui.Text(MenuPalette.Paint(
            "Emails added by mods. They show up in the computer's inbox; send one to try it. " +
            "Email files live in each mod's email folder and load again as soon as you save them.", MenuPalette.Muted));

        var top = ui.Row(8f, 26f);
        top.Button("RELOAD EMAILS", () =>
        {
            MailPacks.Reload();
            Rebuild();
        }, 190f);
        _status = top.Text("");

        var problems = MailPacks.Problems();
        if (problems.Count > 0)
        {
            var panel = ui.Panel();
            panel.SubHeader(MenuPalette.Paint($"PROBLEMS ({problems.Count})", MenuPalette.Bad));
            foreach (string problem in problems)
                panel.Text(MenuPalette.Paint(problem, MenuPalette.Bad));
        }

        var list = ui.Scroll(out _);
        if (MailBook.All.Count == 0)
            list.Text("No emails loaded.");
        foreach (var mail in MailBook.All)
            BuildMail(list.Panel(), mail);
    }

    private void BuildMail(UiBuilder panel, MailItem mail)
    {
        var top = panel.Row(10f, 26f);
        top.SubHeader(mail.Subject.ToUpperInvariant());
        top.Text(MenuPalette.Paint(mail.Source ?? mail.Id, MenuPalette.Muted));
        panel.Text(Describe(mail));
        panel.Row(8f, 26f).Button("SEND NOW", () => Report(Send(mail)), 140f);
    }

    private static string Describe(MailItem mail)
    {
        var parts = new List<string>();
        parts.Add(mail.Send != null ? mail.Send.Describe("Arrives") : "Arrives when something sends it");
        int sent = MailMemory.TimesSent(mail.Id);
        parts.Add(sent == 0 ? "not sent yet" : sent == 1 ? "sent once" : $"sent {sent} times");
        if (MailMemory.WasRead(mail.Id))
            parts.Add("read");
        return string.Join(". ", parts) + ".";
    }

    private static string Send(MailItem mail)
    {
        if (!GameEvents.IsHouseReady)
            return "Emails only arrive in the house.";
        Mailer.Send(mail);
        return $"Sent: {mail.Subject}. Check the computer.";
    }

    private void Report(string message)
    {
        if (_status != null)
            _status.text = message;
    }
}
