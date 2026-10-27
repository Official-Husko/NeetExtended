using System;
using System.Collections.Generic;
using NeetLoader.Runtime;
using NeetModKit.Rules;

namespace EmailExpansion.Mail;

/// <summary>
/// Every modded email. Emails from files are added by the pack loader; mods can add their own:
/// <code>
/// InkScripts.Add(this, InkStory.Email, "hello.ink", "=== MyMod_Hello ===\nHi.\n-> DONE");
/// MailBook.Add(this, new MailItem { Id = "MyMod/hello", Subject = "hi", Knot = "MyMod_Hello" });
/// </code>
/// </summary>
public static class MailBook
{
    private static readonly List<MailItem> Mails = new List<MailItem>();

    public static IReadOnlyList<MailItem> All => Mails;

    /// <summary>Raised after emails are added or removed.</summary>
    public static event Action Changed;

    public static void Add(ITracker owner, MailItem mail)
    {
        if (string.IsNullOrEmpty(mail.Id))
            throw new ArgumentException("An email needs an id.");
        if (string.IsNullOrEmpty(mail.Knot))
            throw new ArgumentException($"Email '{mail.Id}' has no knot to show.");
        if (string.IsNullOrEmpty(mail.Subject))
            throw new ArgumentException($"Email '{mail.Id}' needs a subject for the inbox.");
        if (Find(mail.Id) != null)
            throw new ArgumentException($"There's already an email with the id '{mail.Id}'.");

        Mails.Add(mail);
        owner.Track(Disposable.Create(() =>
        {
            if (Mails.Remove(mail))
                Changed?.Invoke();
        }));
        Changed?.Invoke();
    }

    /// <summary>Finds an email by id. Short ids look in <paramref name="nearMod"/> first.</summary>
    public static MailItem Find(string id, string nearMod = null) => RuleIds.Find(Mails, mail => mail.Id, id, nearMod);

    /// <summary>The email behind an inbox entry's knot, or null for the game's own.</summary>
    public static MailItem ByKnot(string knot)
    {
        if (knot == null)
            return null;
        foreach (var mail in Mails)
        {
            if (mail.Knot == knot)
                return mail;
        }
        return null;
    }
}
