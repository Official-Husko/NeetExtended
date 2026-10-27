using System;
using NeetModKit.Rules;
using Newtonsoft.Json.Linq;

namespace EmailExpansion.Mail;

/// <summary>
/// The email's conditions ("sent", "read") and action ("email"). They join the shared ones, so a
/// phone call can send an email or check whether one was read.
/// </summary>
internal static class MailRules
{
    public static void Register(EmailExpansionMod mod)
    {
        RuleConditions.Register(mod, "sent", value => ByCount(value, MailMemory.TimesSent),
            "An email (by id) arrived: \"rent\" or { \"rent\": \">= 2\" }. Short ids mean an email from the same mod.");
        RuleConditions.Register(mod, "read", value => ByCount(value, id => MailMemory.WasRead(id) ? 1 : 0),
            "An email (by id) was opened: \"rent\". Short ids mean an email from the same mod.");
        RuleActions.Register(mod, "email", SendEmail, "email",
            "Sends an email (by id) now, or after \"after\" seconds of play. Survives saving. Short ids mean an email from the same mod.");
    }

    private static Func<IRuleSubject, bool> ByCount(JToken value, Func<string, int> count)
    {
        if (value.Type == JTokenType.String)
        {
            string id = value.Value<string>();
            return subject => count(Resolve(id, subject)) > 0;
        }
        var tests = NamedComparisons.Parse(value);
        return subject => tests.All(id => count(Resolve(id, subject)));
    }

    /// <summary>Short ids ("rent") mean an email from the same mod as whatever is being checked.</summary>
    private static string Resolve(string id, IRuleSubject subject) =>
        MailBook.Find(id, RuleContext.ModOf(subject))?.Id ?? id;

    private static void SendEmail(ActionArgs args)
    {
        string id = args.Require("email");
        var mail = MailBook.Find(id, RuleContext.ModOf(args.Subject)) ?? throw new ArgumentException($"No email '{id}'.");
        Mailer.Send(mail, args.Num("after"));
    }
}
