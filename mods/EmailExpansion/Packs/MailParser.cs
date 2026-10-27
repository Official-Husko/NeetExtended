using System;
using System.Collections.Generic;
using System.Text;
using EmailExpansion.Mail;
using EmailExpansion.Reader;
using NeetModKit.IO;
using NeetModKit.Rules;
using Newtonsoft.Json.Linq;

namespace EmailExpansion.Packs;

/// <summary>
/// Reads an email file: one email object, a list of them, or { "emails": [...] }. Every email with a
/// "body" list becomes a generated knot in the email story; all knots of a file compile together.
/// </summary>
internal static class MailParser
{
    // Replies show as the reply in the email, not as a line of the next part.
    private static readonly StepWriterOptions Writing = new StepWriterOptions { SayChoices = false };

    public static void LoadFile(ContentFile file)
    {
        var mails = new List<MailItem>();
        var ink = new StringBuilder();
        var objects = file.Items("emails");
        for (int i = 0; i < objects.Count; i++)
        {
            try
            {
                mails.Add(Parse(objects[i], file, ink));
            }
            catch (Exception e) when (e is FormatException || e is InvalidCastException || e is ArgumentException)
            {
                file.Problem($"{ContentFile.Where("email", objects[i], i, objects.Count)}: {e.Message}");
            }
        }

        if (ink.Length > 0)
            file.AddInk(ink.ToString());

        foreach (var mail in mails)
        {
            try { MailBook.Add(file.Scope, mail); }
            catch (ArgumentException e) { file.Problem(e.Message); }
        }
    }

    private static MailItem Parse(JObject json, ContentFile file, StringBuilder ink)
    {
        string subject = json.Value<string>("subject");
        string id = json.Value<string>("id")
                    ?? (subject != null ? InkText.Identifier(subject).ToLowerInvariant() : null)
                    ?? throw new FormatException("needs an \"id\" or a \"subject\".");
        if (string.IsNullOrWhiteSpace(subject))
            throw new FormatException("needs a \"subject\" (what the inbox shows).");

        var mail = new MailItem
        {
            Id = file.ModId + "/" + id,
            Subject = subject,
            Source = file.Display,
        };

        if (json["body"] != null)
        {
            string knot = "Mail_" + InkText.Identifier(file.ModId) + "_" + InkText.Identifier(id);
            ink.Append(StepWriter.Write(knot, json["body"],
                step => RuleSteps.AddAction(file.Scope, step, () => MailReader.Active),
                when => RuleSteps.AddCondition(file.Scope, when, () => MailReader.Active),
                Writing, "body"));
            mail.Knot = knot;
        }
        else if (json["ink"] != null)
        {
            mail.Knot = json.Value<string>("ink");
        }
        else
        {
            throw new FormatException("needs \"body\" (what the email says) or \"ink\" (a knot from an .ink file).");
        }

        if (TriggerRules.IsOn(json["send"]))
            mail.Send = TriggerRules.Parse(json["send"], SendRules.Triggers, SendRules.Defaults);
        return mail;
    }
}
